namespace EcommerceApi.Api.Contracts.Orders
{
    public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
}