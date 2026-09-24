using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Products
{
    public sealed record ProductSearchRequest(
    [StringLength(100)] string? Search,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? IsActive,
    [Range(1, 100000)] int Page = 1,
    [Range(1, 100)] int PageSize = 24,
    [RegularExpression("^(featured|low|high|new)$")] string Sort = "featured");
}
