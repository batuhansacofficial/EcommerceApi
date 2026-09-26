using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EcommerceApi.Api.Data;

public static class CartTransaction
{
    public static async Task<IDbContextTransaction> BeginAsync(ApplicationDbContext db, Guid userId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // All cart writes and checkout take this lock before reading the cart.
            // Different users remain independent; product locks are acquired in UUID order.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM users WHERE \"Id\" = {userId} FOR UPDATE", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
}
