namespace EcommerceApi.Api.Entities;

public sealed class BrowserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
