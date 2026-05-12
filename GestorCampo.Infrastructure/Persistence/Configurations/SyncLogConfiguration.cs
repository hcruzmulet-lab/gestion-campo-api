using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> builder)
    {
        builder.ToTable("sync_logs");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Adapter).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Entity).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Error).HasMaxLength(2000);

        builder.HasIndex(s => new { s.Adapter, s.StartedAt });
        // No soft-delete filter — append-only log
    }
}
