using GestorCampo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorCampo.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Zone).HasMaxLength(100);
        builder.Property(u => u.IdNumber).HasMaxLength(20);
        builder.Property(u => u.EmployeeCode).HasMaxLength(50);
        builder.Property(u => u.Address).HasMaxLength(500);
        builder.Property(u => u.EmailVerificationToken).HasMaxLength(200);
        builder.Property(u => u.PasswordResetToken).HasMaxLength(200);

        builder.HasOne(u => u.Supervisor)
            .WithMany()
            .HasForeignKey(u => u.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
