namespace EcommerceApi.Api.Contracts.Cart
{
    public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
}