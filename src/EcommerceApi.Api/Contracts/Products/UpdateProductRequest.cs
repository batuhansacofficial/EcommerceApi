using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Products
{
    public sealed record UpdateProductRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    string Description,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string Sku,

    [Range(0.01, 1_000_000)]
    decimal Price,

    [Range(0, int.MaxValue)]
    int StockQuantity,

    bool IsActive);
}