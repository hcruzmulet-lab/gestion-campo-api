using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Code).HasMaxLength(10).IsRequired();
        builder.Property(o => o.Purpose).HasMaxLength(50).IsRequired();
        builder.HasIndex(o => new { o.UserId, o.Purpose });
    }
}
