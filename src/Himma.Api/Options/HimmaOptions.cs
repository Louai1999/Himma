namespace Himma.Api.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string Sqlite = "Sqlite";
    public const string Postgres = "Postgres";

    /// <summary>"Sqlite" (development) or "Postgres" (production).</summary>
    public string Provider { get; set; } = Sqlite;

    /// <summary>Apply pending migrations on startup. Only true in Development; production runs them explicitly.</summary>
    public bool AutoMigrate { get; set; }
}

/// <summary>Seeded admin account. Both values come from user-secrets or environment variables, never appsettings.</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string DisplayName { get; set; } = "مدير هِمّة";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Per IP, for /api/auth/*.</summary>
    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Per user (or IP when anonymous), for every /api call.</summary>
    public RateLimitRule Api { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Per user, for AI endpoints.</summary>
    public RateLimitRule Ai { get; set; } = new() { PermitLimit = 20, WindowSeconds = 3600 };
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

/// <summary>When a day counts as "on target". Percentages of the user's daily targets.</summary>
public sealed class AdherenceOptions
{
    public const string SectionName = "Adherence";

    /// <summary>Calories within ± this percent of the target.</summary>
    public double CalorieTolerancePercent { get; set; } = 10;

    /// <summary>Protein at least this percent of the target.</summary>
    public double ProteinMinPercent { get; set; } = 90;

    /// <summary>Water at least this percent of the target.</summary>
    public double WaterMinPercent { get; set; } = 80;
}

/// <summary>Soft-deleted accounts can be restored for GraceDays, then they are purged automatically.</summary>
public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";

    public int GraceDays { get; set; } = 30;

    /// <summary>How often the background job looks for accounts whose grace period has ended.</summary>
    public int PurgeIntervalMinutes { get; set; } = 60;
}
