using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits");
        builder.HasKey(v => v.Id);

        // Notes and Comment are free-form text; no length cap to support
        // long observations / pegado de texto largo desde mobile.
        builder.Property(v => v.Notes).HasColumnType("text");
        builder.Property(v => v.Comment).HasColumnType("text");
        builder.Property(v => v.Result).HasMaxLength(500);
        builder.Property(v => v.Status).HasConversion<int>();

        builder.HasOne(v => v.Client)
            .WithMany()
            .HasForeignKey(v => v.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Vendor)
            .WithMany()
            .HasForeignKey(v => v.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.PlannedBy)
            .WithMany()
            .HasForeignKey(v => v.PlannedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
