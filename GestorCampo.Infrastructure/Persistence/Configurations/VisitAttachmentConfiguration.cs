using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class VisitAttachmentConfiguration : IEntityTypeConfiguration<VisitAttachment>
{
    public void Configure(EntityTypeBuilder<VisitAttachment> builder)
    {
        builder.ToTable("visit_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(128).IsRequired();
        builder.HasIndex(a => a.VisitId);
        builder.HasOne(a => a.Visit)
               .WithMany(v => v.Attachments)
               .HasForeignKey(a => a.VisitId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
