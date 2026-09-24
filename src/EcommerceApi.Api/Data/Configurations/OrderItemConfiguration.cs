using EcommerceApi.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceApi.Api.Data.Configurations
{
    public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("order_items");

            builder.HasKey(orderItem => orderItem.Id);

            builder.Property(orderItem => orderItem.ProductName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(orderItem => orderItem.ProductSku)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(orderItem => orderItem.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(orderItem => orderItem.Quantity)
                .IsRequired();

            builder.Property(orderItem => orderItem.LineTotal)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasOne(orderItem => orderItem.Product)
                .WithMany()
                .HasForeignKey(orderItem => orderItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}