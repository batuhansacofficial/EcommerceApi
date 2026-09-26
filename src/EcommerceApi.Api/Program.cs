using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using EcommerceApi.Api.Options;
using EcommerceApi.Api.Security;
using EcommerceApi.Api.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) && builder.Configuration["DATABASE_URL"] is { } databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var credentials = uri.UserInfo.Split(':', 2);
    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.Trim('/'), Username = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = Enum.Parse<SslMode>(builder.Configuration["Database:SslMode"] ?? "VerifyFull", true),
        Timeout = 15, MaxPoolSize = 20
    }.ConnectionString;
}
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Database configuration is required.");
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
if (jwtOptions is null || string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience) || string.IsNullOrWhiteSpace(jwtOptions.SecretKey) ||
    jwtOptions.SecretKey.Length < 32 || jwtOptions.ExpirationMinutes is < 1 or > 60 ||
    (!development && (jwtOptions.SecretKey.Contains("development-secret", StringComparison.OrdinalIgnoreCase) ||
        jwtOptions.Issuer == "EcommerceApi" || jwtOptions.Audience == "EcommerceApi" ||
        connectionString.Contains("ecommerce_password", StringComparison.OrdinalIgnoreCase))))
    throw new InvalidOperationException("Configure a unique JWT issuer, audience, 32+ character key and 1-60 minute lifetime.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddDataProtection().SetApplicationName("MiraStorefront").PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddControllers(options => options.Filters.Add<BrowserAntiforgeryFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = development ? "mira-csrf" : "__Host-mira-csrf";
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication("SessionOrBearer")
    .AddPolicyScheme("SessionOrBearer", null, options => options.ForwardDefaultSelector = context =>
        context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme : BrowserSecurity.Scheme)
    .AddCookie(BrowserSecurity.Scheme, options =>
    {
        options.Cookie.Name = development ? "mira-session" : "__Host-mira-session";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = BrowserSecurity.ValidateAsync;
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true,
        ValidIssuer = jwtOptions.Issuer, ValidAudience = jwtOptions.Audience, ClockSkew = TimeSpan.FromSeconds(15),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<AccountLoginLimiter>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many attempts. Try again later." }, ct);
    };
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(address));
    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});
builder.Services.AddOpenApi();
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
var app = builder.Build();

// Production migrations are an explicit release step, never part of serving requests.
if (args.Contains("--migrate") || development)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    if (development || args.Contains("--seed-demo"))
    {
        if (!development && !app.Configuration.GetValue<bool>("PublicDemo:Enabled"))
            throw new InvalidOperationException("Production demo seeding requires PublicDemo:Enabled=true.");
        await DemoDataSeeder.SeedAsync(db, app.Configuration, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
    }
    if (args.Contains("--migrate")) return;
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!development)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }
    if (context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/api/cart") ||
        context.Request.Path.StartsWithSegments("/api/orders")) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
if (development) app.MapOpenApi();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (ApplicationDbContext db, CancellationToken ct) =>
{
    try { return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any()
        ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503); }
    catch { return Results.StatusCode(503); }
});
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
