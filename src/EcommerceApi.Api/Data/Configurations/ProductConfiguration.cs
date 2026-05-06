using EcommerceApi.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceApi.Api.Data.Configurations
{
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products");

            builder.HasKey(product => product.Id);

            builder.Property(product => product.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(product => product.Description)
                .HasMaxLength(2000)
                .IsRequired();

            builder.Property(product => product.Sku)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(product => product.Sku)
                .IsUnique();

            builder.Property(product => product.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(product => product.StockQuantity)
                .IsRequired();

            builder.Property(product => product.IsActive)
                .IsRequired();

            builder.Property(product => product.CreatedAtUtc)
                .IsRequired();
        }
    }
}