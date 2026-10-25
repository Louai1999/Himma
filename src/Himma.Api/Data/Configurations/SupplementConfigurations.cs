using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Himma.Api.Data.Configurations;

public sealed class SupplementConfiguration : IEntityTypeConfiguration<Supplement>
{
    public void Configure(EntityTypeBuilder<Supplement> b)
    {
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Times).HasMaxLength(64).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(120);
        b.HasIndex(x => x.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplementIntakeConfiguration : IEntityTypeConfiguration<SupplementIntake>
{
    public void Configure(EntityTypeBuilder<SupplementIntake> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date });
        b.HasIndex(x => new { x.SupplementId, x.Date });
        // Account purge deletes intakes before the user (AccountLifecycleService), so the two cascade paths
        // (user, supplement) never race on PostgreSQL.
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Supplement>().WithMany().HasForeignKey(x => x.SupplementId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<MealEntry>().WithMany().HasForeignKey(x => x.MealEntryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> b)
    {
        b.Property(x => x.Endpoint).HasMaxLength(600).IsRequired();
        b.Property(x => x.P256dh).HasMaxLength(200).IsRequired();
        b.Property(x => x.Auth).HasMaxLength(100).IsRequired();
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.HasIndex(x => x.Endpoint).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(64);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
    }
}
