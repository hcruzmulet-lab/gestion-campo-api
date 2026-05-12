using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class TrackingPointConfiguration : IEntityTypeConfiguration<TrackingPoint>
{
    public void Configure(EntityTypeBuilder<TrackingPoint> builder)
    {
        builder.ToTable("tracking_points");
        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.Vendor)
            .WithMany()
            .HasForeignKey(t => t.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.VendorId, t.CapturedAt });
        // No soft-delete filter — append-only log
    }
}
