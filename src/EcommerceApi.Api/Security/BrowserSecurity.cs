using System.Security.Claims;
using System.Threading.RateLimiting;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Security;

public static class BrowserSecurity
{
    public const string Scheme = "BrowserSession";
    public const string SessionClaim = "session_id";

    public static async Task SignInAsync(HttpContext context, ApplicationDbContext db, User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);
        var session = new BrowserSession { Id = Guid.NewGuid(), UserId = user.Id, ExpiresAtUtc = expires };
        db.BrowserSessions.Add(session);
        await db.SaveChangesAsync(context.RequestAborted);
        await db.BrowserSessions.Where(s => s.ExpiresAtUtc < DateTime.UtcNow)
            .ExecuteDeleteAsync(context.RequestAborted);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(SessionClaim, session.Id.ToString())
        }, Scheme);
        await context.SignInAsync(Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = expires });
    }

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(SessionClaim), out var sessionId) ||
            !Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.RejectPrincipal();
            return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var valid = await db.BrowserSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId &&
            s.ExpiresAtUtc > DateTime.UtcNow, context.HttpContext.RequestAborted);
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (!valid || role != context.Principal?.FindFirstValue(ClaimTypes.Role)) context.RejectPrincipal();
    }
}

// Browser writes require CSRF tokens, including login and logout. Explicit bearer clients
// do not authenticate through ambient cookies and retain the existing API contract.
public sealed class BrowserAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
        if (!request.Path.StartsWithSegments("/api/auth/session") &&
            context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new { message = "Invalid CSRF token. Refresh and try again." });
        }
    }
}

public sealed class AccountLoginLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(
        email => RateLimitPartition.GetFixedWindowLimiter(email, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8, Window = TimeSpan.FromMinutes(5), QueueLimit = 0
        }));

    public async Task<bool> AllowAsync(string email, CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(email, 1, cancellationToken);
        return lease.IsAcquired;
    }
    public void Dispose() => limiter.Dispose();
}
