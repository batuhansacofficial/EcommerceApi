using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Cart
{
    public sealed record UpdateCartItemRequest(
    [Range(1, 100)]
    int Quantity);
}