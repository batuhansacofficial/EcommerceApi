using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EcommerceApi.Api.Contracts.Cart;
using EcommerceApi.Api.Contracts.Orders;
using EcommerceApi.Api.Contracts.Products;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using EcommerceApi.Api.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class IntegrationSuite
{
    private static string connection = "";
    private static WebApplicationFactory<Program> app = null!;
    private static int passed;
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Run(string name, Func<Task> action) { await action(); Console.WriteLine($"PASS {name}"); passed++; }
    private static async Task Status(HttpResponseMessage response, HttpStatusCode expected)
    {
        Check(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    private static async Task<(HttpClient Client, User User)> Client(string role = "Customer")
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.test", FirstName = "Integration", LastName = "Test", Role = role };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Integration-Test-Password-2026!");
        await using var db = Db(); db.Users.Add(user); await db.SaveChangesAsync();
        var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var scope = app.Services.CreateScope();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateAccessToken(user));
        return (client, user);
    }
    private static async Task<Product> Product(int stock = 10, Guid? id = null, string? name = null)
    {
        var product = new Product { Id = id ?? Guid.NewGuid(), Sku = Guid.NewGuid().ToString("N"), Name = name ?? "Integration product", Description = "Integration test", Price = 100m, StockQuantity = stock };
        await using var db = Db(); db.Products.Add(product); await db.SaveChangesAsync(); return product;
    }
    private static async Task Add(HttpClient client, Product product, int quantity = 1) =>
        await Status(await client.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity }), HttpStatusCode.OK);
    private static Task<HttpResponseMessage> Checkout(HttpClient client, string key, decimal total = 100m)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(new { expectedTotal = total }) };
        request.Headers.Add("Idempotency-Key", key); return client.SendAsync(request);
    }
    private static async Task Csrf(HttpClient client)
    {
        var data = await client.GetFromJsonAsync<JsonElement>("/api/auth/session/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", data.GetProperty("token").GetString());
    }

    public static async Task Main()
    {
        connection = Environment.GetEnvironmentVariable("TEST_DATABASE") ?? throw new Exception("TEST_DATABASE must point to a disposable PostgreSQL database ending in _test.");
        Check(new NpgsqlConnectionStringBuilder(connection).Database?.EndsWith("_test", StringComparison.Ordinal) == true, "Refusing to test outside a *_test database.");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "mira-integration");
        Environment.SetEnvironmentVariable("Jwt__Audience", "mira-integration");
        Environment.SetEnvironmentVariable("Jwt__SecretKey", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "15");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Error");
        await using (var db = Db()) await db.Database.MigrateAsync();
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "src", "EcommerceApi.Api")))
            repository = repository.Parent;
        var contentRoot = Path.Combine(repository?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "src", "EcommerceApi.Api");
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot).UseEnvironment("Production"));
        using var publicClient = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        await Run("migrations, readiness and production headers", async () =>
        {
            var response = await publicClient.GetAsync("/health/ready"); await Status(response, HttpStatusCode.OK);
            Check(response.Headers.Contains("Content-Security-Policy") && response.Headers.Contains("Strict-Transport-Security"), "Missing production headers");
            await using var db = Db(); Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Pending migrations");
        });
        await Run("seed is repeatable and preserves existing stock", async () =>
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DemoSeed:Enabled"] = "true" }).Build();
            await using var db = Db(); await DemoDataSeeder.SeedAsync(db, config, new PasswordHasher<User>());
            var item = await db.Products.SingleAsync(p => p.Sku == "MIRA-KNIT"); item.StockQuantity = 6; await db.SaveChangesAsync();
            await DemoDataSeeder.SeedAsync(db, config, new PasswordHasher<User>());
            Check(await db.Products.CountAsync(p => p.Sku.StartsWith("MIRA-")) == 4 && item.StockQuantity == 6, "Seed duplicated or reset data");
        });
        await Run("concurrent customers cannot oversell", async () =>
        {
            var a = await Client(); var b = await Client(); var p = await Product(1);
            await Add(a.Client,p); await Add(b.Client,p);
            var results = await Task.WhenAll(Checkout(a.Client,Guid.NewGuid().ToString()),Checkout(b.Client,Guid.NewGuid().ToString()));
            Check(results.Count(r=>r.StatusCode==HttpStatusCode.OK)==1 && results.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1, "Oversell responses incorrect");
            await using var db=Db(); Check((await db.Products.FindAsync(p.Id))!.StockQuantity==0, "Stock incorrect");
            Check(await db.OrderItems.CountAsync(i=>i.ProductId==p.Id)==1, "Duplicate inventory sold");
        });
        await Run("eight concurrent retries create one order", async () =>
        {
            var a=await Client(); var p=await Product(20); await Add(a.Client,p); var key=Guid.NewGuid().ToString();
            var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Checkout(a.Client,key)));
            foreach(var result in results) await Status(result,HttpStatusCode.OK);
            var orders=await Task.WhenAll(results.Select(r=>r.Content.ReadFromJsonAsync<OrderResponse>()));
            Check(orders.Select(o=>o!.Id).Distinct().Count()==1,"Different retry orders");
            await using var db=Db(); Check((await db.Products.FindAsync(p.Id))!.StockQuantity==19,"Repeated stock decrement");
            Check(!await db.CartItems.AnyAsync(c=>c.UserId==a.User.Id),"Cart not cleared");
            await Status(await Checkout(a.Client,key,200),HttpStatusCode.Conflict);
        });
        await Run("different checkout keys still consume a cart once", async () =>
        {
            var a=await Client(); var p=await Product(); await Add(a.Client,p);
            var results=await Task.WhenAll(Checkout(a.Client,Guid.NewGuid().ToString()),Checkout(a.Client,Guid.NewGuid().ToString()));
            Check(results.Count(r=>r.StatusCode==HttpStatusCode.OK)==1 && results.Count(r=>r.StatusCode==HttpStatusCode.BadRequest)==1,"Cart consumed twice");
        });
        await Run("late stock conflict rolls back earlier product decrements", async () =>
        {
            var a=await Client(); var tail=Guid.NewGuid().ToString("N")[20..];
            var first=await Product(10,Guid.Parse($"00000000-0000-0000-0000-{tail}"));
            var last=await Product(10,Guid.Parse($"ffffffff-ffff-ffff-ffff-{tail}"));
            await Add(a.Client,first); await Add(a.Client,last);
            await using(var db=Db()) await db.Products.Where(p=>p.Id==last.Id).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.StockQuantity,0));
            await Status(await Checkout(a.Client,Guid.NewGuid().ToString(),200),HttpStatusCode.Conflict);
            await using(var db=Db()) { Check((await db.Products.FindAsync(first.Id))!.StockQuantity==10,"Partial stock decrement committed"); Check(await db.CartItems.CountAsync(c=>c.UserId==a.User.Id)==2,"Failed cart lost"); }
        });
        await Run("admin stock edit cannot overwrite a completed checkout", async () =>
        {
            var a=await Client();var admin=await Client("Admin");var p=await Product();await Add(a.Client,p);
            var update=new { p.Name,p.Description,p.Sku,p.Price,stockQuantity=20,isActive=true,version=p.Version };
            var checkoutTask=Checkout(a.Client,Guid.NewGuid().ToString());
            var updateTask=admin.Client.PutAsJsonAsync($"/api/products/{p.Id}",update);
            await Status(await checkoutTask,HttpStatusCode.OK);
            var response=await updateTask; Check(response.IsSuccessStatusCode||response.StatusCode==HttpStatusCode.Conflict,"Unexpected admin result");
            await using var db=Db();var current=(await db.Products.FindAsync(p.Id))!;
            Check(current.StockQuantity==(response.IsSuccessStatusCode?19:9),"Admin overwrote sold stock");
            await Status(await admin.Client.PutAsJsonAsync($"/api/products/{p.Id}",update),HttpStatusCode.Conflict);
        });
        await Run("price changes need reconfirmation", async () =>
        {
            var a=await Client();var p=await Product();await Add(a.Client,p);
            await Status(await Checkout(a.Client,Guid.NewGuid().ToString(),99),HttpStatusCode.Conflict);
            await using var db=Db();Check((await db.Products.FindAsync(p.Id))!.StockQuantity==10,"Price conflict modified stock");
        });
        await Run("cart additions serialize without duplicates or lost updates", async () =>
        {
            var a=await Client();var p=await Product(100);
            await Task.WhenAll(Enumerable.Range(0,12).Select(_=>Add(a.Client,p)));
            var cart=await a.Client.GetFromJsonAsync<CartResponse>("/api/cart");
            Check(cart!.Items.Count==1&&cart.Items.Single().Quantity==12,"Lost cart update");
        });
        await Run("archive preserves order snapshots and authorization", async () =>
        {
            var a=await Client();var b=await Client();var admin=await Client("Admin");var p=await Product();await Add(a.Client,p);
            var order=(await (await Checkout(a.Client,Guid.NewGuid().ToString())).Content.ReadFromJsonAsync<OrderResponse>())!;
            await Status(await a.Client.DeleteAsync($"/api/products/{p.Id}"),HttpStatusCode.Forbidden);
            await Status(await admin.Client.DeleteAsync($"/api/products/{p.Id}"),HttpStatusCode.NoContent);
            await Status(await publicClient.GetAsync($"/api/products/{p.Id}"),HttpStatusCode.NotFound);
            await Status(await publicClient.GetAsync("/api/products?isActive=false"),HttpStatusCode.Forbidden);
            await Status(await b.Client.GetAsync($"/api/orders/{order.Id}"),HttpStatusCode.NotFound);
            var snapshot=await a.Client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
            Check(snapshot!.Items.Single().ProductName==p.Name,"Order snapshot lost");
        });
        await Run("bounded catalog and literal wildcard search", async () =>
        {
            await Product(name:"Literal%_Marker");
            var response=await publicClient.GetAsync("/api/products?pageSize=1");await Status(response,HttpStatusCode.OK);
            Check((await response.Content.ReadFromJsonAsync<ProductResponse[]>())!.Length==1&&response.Headers.Contains("X-Total-Count"),"Pagination failed");
            await Status(await publicClient.GetAsync("/api/products?pageSize=101"),HttpStatusCode.BadRequest);
            var found=await publicClient.GetFromJsonAsync<ProductResponse[]>("/api/products?search=%25_");
            Check(found is { Length: >0 } && found.All(p=>p.Name.Contains("%_")),"Wildcard not escaped");
        });
        await Run("browser cookie, CSRF, session restoration and logout revocation", async () =>
        {
            using var browser=app.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
            var registration=new {email=$"{Guid.NewGuid():N}@example.test",password="Secure-browser-password-2026!",firstName="Browser",lastName="Test"};
            await Status(await browser.PostAsJsonAsync("/api/auth/session/register",registration),HttpStatusCode.BadRequest);
            await Csrf(browser);
            var response=await browser.PostAsJsonAsync("/api/auth/session/register",registration);await Status(response,HttpStatusCode.OK);
            var body=await response.Content.ReadFromJsonAsync<JsonElement>(); Check(body.GetProperty("accessToken").GetString()=="","Browser received bearer token");
            var cookie=response.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("__Host-mira-session="));
            Check(cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&cookie.Contains("secure",StringComparison.OrdinalIgnoreCase)&&cookie.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase),"Cookie flags missing");
            await Status(await browser.GetAsync("/api/auth/me"),HttpStatusCode.OK);
            browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            await Status(await browser.PostAsync("/api/auth/session/logout",null),HttpStatusCode.BadRequest);
            await Csrf(browser);await Status(await browser.PostAsync("/api/auth/session/logout",null),HttpStatusCode.NoContent);
            using var stolen=app.CreateClient(new(){BaseAddress=new Uri("https://localhost"),HandleCookies=false});
            stolen.DefaultRequestHeaders.Add("Cookie",cookie.Split(';')[0]);
            await Status(await stolen.GetAsync("/api/auth/me"),HttpStatusCode.Unauthorized);
        });
        await Run("account and IP throttling return retry hints", async () =>
        {
            var target=$"{Guid.NewGuid():N}@example.test";HttpResponseMessage? response=null;
            for(var i=0;i<9;i++) response=await publicClient.PostAsJsonAsync("/api/auth/login",new{email=target,password="wrong-password"});
            await Status(response!,HttpStatusCode.TooManyRequests);Check(response!.Headers.RetryAfter is not null,"No Retry-After");
            for(var i=0;i<25;i++) response=await publicClient.PostAsJsonAsync("/api/auth/login",new{email=$"{Guid.NewGuid():N}@example.test",password="wrong-password"});
            await Status(response!,HttpStatusCode.TooManyRequests);
        });
        Console.WriteLine($"{passed} PostgreSQL integration scenarios passed.");
        await app.DisposeAsync();
    }
}
