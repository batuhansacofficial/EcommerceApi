using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Cart
{
    public sealed record AddCartItemRequest(
    [Required]
    Guid ProductId,

    [Range(1, 100)]
    int Quantity);
}
