namespace EcommerceApi.Api.Contracts.Cart
{
    public sealed record CartResponse(
    IReadOnlyList<CartItemResponse> Items,
    decimal Total);
}
