using EcommerceApi.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceApi.Api.Data.Configurations
{
    public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
    {
        public void Configure(EntityTypeBuilder<CartItem> builder)
        {
            builder.ToTable("cart_items");

            builder.HasKey(cartItem => cartItem.Id);

            builder.Property(cartItem => cartItem.Quantity)
                .IsRequired();

            builder.Property(cartItem => cartItem.CreatedAtUtc)
                .IsRequired();

            builder.HasOne(cartItem => cartItem.User)
                .WithMany()
                .HasForeignKey(cartItem => cartItem.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(cartItem => cartItem.Product)
                .WithMany()
                .HasForeignKey(cartItem => cartItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(cartItem => new
            {
                cartItem.UserId,
                cartItem.ProductId
            }).IsUnique();
        }
    }
}