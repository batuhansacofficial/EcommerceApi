namespace EcommerceApi.Api.Contracts.Products
{
    public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
}