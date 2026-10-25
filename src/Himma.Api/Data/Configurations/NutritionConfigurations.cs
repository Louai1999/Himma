using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Himma.Api.Data.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(80);
        b.Property(x => x.ServingLabel).HasMaxLength(40);
        b.Property(x => x.ServingLabelEn).HasMaxLength(40);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class CustomFoodConfiguration : IEntityTypeConfiguration<CustomFood>
{
    public void Configure(EntityTypeBuilder<CustomFood> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MealEntryConfiguration : IEntityTypeConfiguration<MealEntry>
{
    public void Configure(EntityTypeBuilder<MealEntry> b)
    {
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.UserId, x.Date });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WaterEntryConfiguration : IEntityTypeConfiguration<WaterEntry>
{
    public void Configure(EntityTypeBuilder<WaterEntry> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WeightEntryConfiguration : IEntityTypeConfiguration<WeightEntry>
{
    public void Configure(EntityTypeBuilder<WeightEntry> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WaterReminderSettingConfiguration : IEntityTypeConfiguration<WaterReminderSetting>
{
    public void Configure(EntityTypeBuilder<WaterReminderSetting> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<WaterReminderSetting>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StepEntryConfiguration : IEntityTypeConfiguration<StepEntry>
{
    public void Configure(EntityTypeBuilder<StepEntry> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
