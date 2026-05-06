namespace EcommerceApi.Api.Entities
{
    public sealed class Order
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        public List<OrderItem> Items { get; set; } = [];
    }
}