using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Himma.Api.Data.Configurations;

// Delete rules:
// - Deleting a user cascades to their exercises, plans (→ days → items) and sessions (→ set logs).
// - Exercises are never hard-deleted while referenced (they are archived instead), so references
//   to them use NoAction. NoAction is checked at the end of the statement, which lets a user purge
//   remove the plan items and the user's own exercises in the same cascade.

public sealed class ExerciseDefinitionConfiguration : IEntityTypeConfiguration<ExerciseDefinition>
{
    public void Configure(EntityTypeBuilder<ExerciseDefinition> b)
    {
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Equipment).HasMaxLength(60).IsRequired();
        b.Property(x => x.IconKey).HasMaxLength(40).IsRequired();
        b.Property(x => x.Instructions).HasMaxLength(1000);
        b.HasIndex(x => new { x.OwnerUserId, x.Place, x.MuscleGroup });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlan>
{
    public void Configure(EntityTypeBuilder<WorkoutPlan> b)
    {
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.BasedOnTemplateKey).HasMaxLength(40);
        b.HasIndex(x => x.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Days).WithOne(d => d.Plan).HasForeignKey(d => d.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutPlanDayConfiguration : IEntityTypeConfiguration<WorkoutPlanDay>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanDay> b)
    {
        b.Property(x => x.Focus).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.PlanId, x.DayKey }).IsUnique();
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.PlanDayId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutPlanItemConfiguration : IEntityTypeConfiguration<WorkoutPlanItem>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanItem> b)
    {
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> b)
    {
        b.HasIndex(x => new { x.UserId, x.Date });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // Deleting a plan keeps the workout history; the session just loses its link to the plan day.
        b.HasOne<WorkoutPlanDay>().WithMany().HasForeignKey(x => x.PlanDayId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.SetLogs).WithOne().HasForeignKey(l => l.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutSetLogConfiguration : IEntityTypeConfiguration<WorkoutSetLog>
{
    public void Configure(EntityTypeBuilder<WorkoutSetLog> b)
    {
        b.HasIndex(x => new { x.SessionId, x.ExerciseId, x.SetNumber }).IsUnique();
        b.HasOne<ExerciseDefinition>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.NoAction);
    }
}
