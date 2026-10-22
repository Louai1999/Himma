namespace Himma.Api.Domain.Entities;

/// <summary>System exercises have OwnerUserId = null and are read-only; users can clone them.</summary>
public class ExerciseDefinition
{
    public int Id { get; set; }
    public string? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MuscleGroup MuscleGroup { get; set; }
    public BodyRegion Region { get; set; }
    public WorkoutPlace Place { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public int DefaultSets { get; set; }
    public int DefaultReps { get; set; }
    public string? Instructions { get; set; }

    /// <summary>Soft delete: kept so plans and history that reference it stay intact.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class WorkoutPlan
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WorkoutPlace Place { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? BasedOnTemplateKey { get; set; }

    public List<WorkoutPlanDay> Days { get; set; } = [];
}

public class WorkoutPlanDay
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public DayKey DayKey { get; set; }
    public bool IsRest { get; set; }

    /// <summary>Comma-separated muscle groups, e.g. "chest,triceps".</summary>
    public string Focus { get; set; } = string.Empty;

    public WorkoutPlan? Plan { get; set; }
    public List<WorkoutPlanItem> Items { get; set; } = [];
}

public class WorkoutPlanItem
{
    public int Id { get; set; }
    public int PlanDayId { get; set; }
    public int ExerciseId { get; set; }
    public int Order { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public double? WeightKg { get; set; }
    public int? RestSeconds { get; set; }
    public string? Notes { get; set; }

    public ExerciseDefinition? Exercise { get; set; }
}

public class WorkoutSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int? PlanDayId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<WorkoutSetLog> SetLogs { get; set; } = [];
}

public class WorkoutSetLog
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int ExerciseId { get; set; }
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public double? WeightKg { get; set; }
    public bool Completed { get; set; }
}
