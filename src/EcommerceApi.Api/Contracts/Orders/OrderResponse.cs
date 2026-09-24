namespace EcommerceApi.Api.Contracts.Orders
{
    public sealed record OrderResponse(
    Guid Id,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);
}
