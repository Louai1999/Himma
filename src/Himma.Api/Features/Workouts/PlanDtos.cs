using Himma.Api.Domain;
using Himma.Api.Domain.Entities;

namespace Himma.Api.Features.Workouts;

public sealed class PlanRequest
{
    public string Name { get; init; } = string.Empty;
    public WorkoutPlace? Place { get; init; }
}

/// <summary>Replaces the whole day: rest flag, focus, and the ordered list of exercises.</summary>
public sealed class PlanDayRequest
{
    public bool? IsRest { get; init; }
    public List<MuscleGroup> Focus { get; init; } = [];
    public List<PlanItemRequest> Items { get; init; } = [];
}

public sealed class PlanItemRequest
{
    public int? ExerciseId { get; init; }
    public int? Sets { get; init; }
    public int? Reps { get; init; }
    public double? WeightKg { get; init; }
    public int? RestSeconds { get; init; }
    public string? Notes { get; init; }
}

/// <summary>The heaviest completed set from the most recent session that included the exercise.</summary>
public sealed record LastPerformanceDto(DateOnly Date, double? WeightKg, int Reps);

public sealed record PlanSummaryDto(
    int Id,
    string Name,
    WorkoutPlace Place,
    bool IsActive,
    string? BasedOnTemplateKey,
    DateTime CreatedAtUtc,
    int TrainingDays);

public sealed record PlanItemDto(
    int Id,
    int Order,
    ExerciseDto Exercise,
    int Sets,
    int Reps,
    double? WeightKg,
    int? RestSeconds,
    string? Notes,
    LastPerformanceDto? Last);

public sealed record PlanDayDto(int Id, DayKey DayKey, bool IsRest, IReadOnlyList<MuscleGroup> Focus, IReadOnlyList<PlanItemDto> Items);

public sealed record PlanDetailDto(
    int Id,
    string Name,
    WorkoutPlace Place,
    bool IsActive,
    string? BasedOnTemplateKey,
    DateTime CreatedAtUtc,
    IReadOnlyList<PlanDayDto> Days)
{
    public static PlanDetailDto From(WorkoutPlan plan, IReadOnlyDictionary<int, LastPerformanceDto> last) =>
        new(plan.Id, plan.Name, plan.Place, plan.IsActive, plan.BasedOnTemplateKey, plan.CreatedAtUtc,
            plan.Days.OrderBy(d => d.DayKey).Select(d => DayFrom(d, last)).ToList());

    public static PlanDayDto DayFrom(WorkoutPlanDay day, IReadOnlyDictionary<int, LastPerformanceDto> last) =>
        new(day.Id, day.DayKey, day.IsRest, FocusCodec.Decode(day.Focus),
            day.Items.OrderBy(i => i.Order).Select(i => new PlanItemDto(
                i.Id, i.Order, ExerciseDto.From(i.Exercise!), i.Sets, i.Reps, i.WeightKg, i.RestSeconds, i.Notes,
                last.GetValueOrDefault(i.ExerciseId))).ToList());
}

public sealed record TemplateItemDto(ExerciseDto Exercise, int Sets, int Reps, int RestSeconds);

public sealed record TemplateDayDto(DayKey DayKey, bool IsRest, IReadOnlyList<MuscleGroup> Focus, IReadOnlyList<TemplateItemDto> Items);

public sealed record TemplateDto(string Key, string Name, string Description, WorkoutPlace Place, IReadOnlyList<TemplateDayDto> Days);
