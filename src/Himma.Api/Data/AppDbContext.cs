using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Himma.Api.Data;

/// <summary>
/// Shared model for every database provider. Each provider has its own small subclass
/// (SqliteAppDbContext, PostgresAppDbContext) so each keeps its own migrations folder.
/// The rest of the app only ever asks for AppDbContext.
/// </summary>
public abstract class AppDbContext : IdentityDbContext<ApplicationUser>
{
    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<CustomFood> CustomFoods => Set<CustomFood>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<WaterEntry> WaterEntries => Set<WaterEntry>();
    public DbSet<WeightEntry> WeightEntries => Set<WeightEntry>();
    public DbSet<WaterReminderSetting> WaterReminderSettings => Set<WaterReminderSetting>();
    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();
    public DbSet<Supplement> Supplements => Set<Supplement>();
    public DbSet<SupplementIntake> SupplementIntakes => Set<SupplementIntake>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ExerciseDefinition> Exercises => Set<ExerciseDefinition>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutPlanDay> WorkoutPlanDays => Set<WorkoutPlanDay>();
    public DbSet<WorkoutPlanItem> WorkoutPlanItems => Set<WorkoutPlanItem>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<WorkoutSetLog> WorkoutSetLogs => Set<WorkoutSetLog>();
    public DbSet<CoachMessage> CoachMessages => Set<CoachMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTime is UTC. SQLite does not store the "kind", so mark values as UTC when read;
        // PostgreSQL (timestamptz) refuses non-UTC values, so convert before writing.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // Store enums as readable strings.
        configurationBuilder.Properties<Gender>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ActivityLevel>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Goal>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<MealType>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<MealSource>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<MuscleGroup>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<BodyRegion>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<WorkoutPlace>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<DayKey>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<CoachRole>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<SupplementUnit>().HaveConversion<string>().HaveMaxLength(16);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDb => toDb.Kind == DateTimeKind.Local ? toDb.ToUniversalTime() : DateTime.SpecifyKind(toDb, DateTimeKind.Utc),
        fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));
}
