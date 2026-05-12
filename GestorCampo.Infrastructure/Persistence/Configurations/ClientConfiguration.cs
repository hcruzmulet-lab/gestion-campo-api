using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(300).IsRequired();
        builder.Property(c => c.TaxId).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.TaxId).IsUnique();
        builder.Property(c => c.Address).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.ExternalId).HasMaxLength(100);
        builder.Property(c => c.Source).HasMaxLength(50);

        builder.HasOne(c => c.AssignedVendor)
            .WithMany()
            .HasForeignKey(c => c.AssignedVendorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
