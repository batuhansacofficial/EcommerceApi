using EcommerceApi.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceApi.Api.Data.Configurations
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");

            builder.HasKey(user => user.Id);

            builder.Property(user => user.Email)
                .HasMaxLength(320)
                .IsRequired();

            builder.HasIndex(user => user.Email)
                .IsUnique();

            builder.Property(user => user.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(user => user.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(user => user.LastName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(user => user.Role)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(user => user.CreatedAtUtc)
                .IsRequired();
        }
    }
}