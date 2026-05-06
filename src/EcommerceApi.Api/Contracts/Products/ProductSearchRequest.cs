namespace EcommerceApi.Api.Contracts.Products
{
    public sealed record ProductSearchRequest(
    string? Search,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? IsActive);
}