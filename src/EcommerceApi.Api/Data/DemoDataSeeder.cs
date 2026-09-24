using EcommerceApi.Api.Entities;
using EcommerceApi.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Data;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher)
    {
        if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
        {
            return;
        }

        var products = new[]
        {
            new Product { Name = "The Relaxed Overshirt", Description = "A softly structured layer in washed olive cotton.", Sku = "MIRA-OVERSHIRT", Price = 120, StockQuantity = 12 },
            new Product { Name = "The Tailored Trouser", Description = "An easy wide leg silhouette in ivory linen.", Sku = "MIRA-TROUSERS", Price = 140, StockQuantity = 9 },
            new Product { Name = "The Cashmere Knit", Description = "A timeless knit with a relaxed drape.", Sku = "MIRA-KNIT", Price = 180, StockQuantity = 7 },
            new Product { Name = "The Everyday Tote", Description = "A spacious carryall in deep olive leather.", Sku = "MIRA-TOTE", Price = 220, StockQuantity = 5 }
        };

        var existingSkus = await db.Products
            .Select(product => product.Sku)
            .ToListAsync();

        foreach (var product in products.Where(product => !existingSkus.Contains(product.Sku)))
        {
            product.Id = Guid.NewGuid();
            product.CreatedAtUtc = DateTime.UtcNow;
            db.Products.Add(product);
        }

        var adminEmail = configuration["AdminBootstrap:Email"]?.Trim().ToLowerInvariant();
        var adminPassword = configuration["AdminBootstrap:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword) &&
            !await db.Users.AnyAsync(user => user.Email == adminEmail))
        {
            if (adminPassword.Length < 16)
                throw new InvalidOperationException("Admin bootstrap password must contain at least 16 characters.");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                FirstName = "Demo",
                LastName = "Admin",
                Role = UserRoles.Admin,
                CreatedAtUtc = DateTime.UtcNow
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
            db.Users.Add(admin);
        }

        await db.SaveChangesAsync();
    }
}
