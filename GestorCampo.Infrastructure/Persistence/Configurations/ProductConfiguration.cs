using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Price).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.ExternalId).HasMaxLength(100);
        builder.Property(p => p.Source).HasMaxLength(50);
    }
}
