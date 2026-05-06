using EcommerceApi.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceApi.Api.Data.Configurations
{
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders");

            builder.HasKey(order => order.Id);

            builder.Property(order => order.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(order => order.Status)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(order => order.CreatedAtUtc)
                .IsRequired();

            builder.HasOne(order => order.User)
                .WithMany()
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(order => order.Items)
                .WithOne(orderItem => orderItem.Order)
                .HasForeignKey(orderItem => orderItem.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}