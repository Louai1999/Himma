using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Himma.Api.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.DisplayName).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.CreatedAtUtc);
        b.HasIndex(x => x.DeletionScheduledForUtc);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(256);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.FamilyId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CoachMessageConfiguration : IEntityTypeConfiguration<CoachMessage>
{
    public void Configure(EntityTypeBuilder<CoachMessage> b)
    {
        b.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.ActorUserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ActorRole).HasMaxLength(32).IsRequired();
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.TargetUserId).HasMaxLength(64);
        b.Property(x => x.Details).HasMaxLength(4000).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => x.CreatedAtUtc);
        b.HasIndex(x => x.ActorUserId);
        b.HasIndex(x => x.TargetUserId);
        // No foreign keys on purpose: the audit trail must outlive purged accounts.
    }
}
