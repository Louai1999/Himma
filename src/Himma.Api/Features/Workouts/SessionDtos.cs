using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Workouts;

public sealed class StartSessionRequest
{
    public string Date { get; init; } = string.Empty;

    /// <summary>The plan day being followed; null for a free workout.</summary>
    public int? PlanDayId { get; init; }
}

/// <summary>Replaces all set logs of the session with this list.</summary>
public sealed class SaveSetsRequest
{
    public List<SetLogRequest> Sets { get; init; } = [];
}

public sealed class SetLogRequest
{
    public int? ExerciseId { get; init; }
    public int? SetNumber { get; init; }
    public int? Reps { get; init; }
    public double? WeightKg { get; init; }
    public bool? Completed { get; init; }
}

public sealed record SetLogDto(int ExerciseId, int SetNumber, int Reps, double? WeightKg, bool Completed)
{
    public static SetLogDto From(WorkoutSetLog l) => new(l.ExerciseId, l.SetNumber, l.Reps, l.WeightKg, l.Completed);
}

/// <summary>What the plan asks for, and what the user did last time, so they know which weight to pick.</summary>
public sealed record SessionExerciseDto(
    ExerciseDto Exercise,
    int PlannedSets,
    int PlannedReps,
    double? PlannedWeightKg,
    int? RestSeconds,
    string? Notes,
    LastPerformanceDto? Last);

public sealed record SessionDto(
    int Id,
    DateOnly Date,
    int? PlanDayId,
    DayKey? DayKey,
    string? PlanName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<SessionExerciseDto> Exercises,
    IReadOnlyList<SetLogDto> Sets);

public sealed record WorkoutHistoryItemDto(
    int Id,
    DateOnly Date,
    DayKey? DayKey,
    string? PlanName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int ExerciseCount,
    int CompletedSets,
    double TotalVolumeKg);

public sealed class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
    }
}

public sealed class SaveSetsRequestValidator : AbstractValidator<SaveSetsRequest>
{
    public const int MaxSets = 200;

    public SaveSetsRequestValidator()
    {
        RuleFor(x => x.Sets).Cascade(CascadeMode.Stop).NotNull()
            .Must(s => s.Count <= MaxSets).WithMessage($"{MaxSets} جولة كحد أقصى.")
            .Must(s => s.Select(x => (x.ExerciseId, x.SetNumber)).Distinct().Count() == s.Count)
            .WithMessage("كل جولة تُسجّل مرة واحدة فقط لكل تمرين.");

        RuleForEach(x => x.Sets).ChildRules(set =>
        {
            set.RuleFor(x => x.ExerciseId).NotNull().WithMessage("التمرين مطلوب.");
            set.RuleFor(x => x.SetNumber).NotNull().WithMessage("رقم الجولة مطلوب.")
                .InclusiveBetween(1, 20).WithMessage("رقم الجولة بين 1 و 20.");
            set.RuleFor(x => x.Reps).NotNull().WithMessage("عدد التكرارات مطلوب.")
                .InclusiveBetween(1, 100).WithMessage("عدد التكرارات بين 1 و 100.");
            set.RuleFor(x => x.WeightKg).InclusiveBetween(0, 500).When(x => x.WeightKg is not null)
                .WithMessage("الوزن بين 0 و 500 كغ.");
            set.RuleFor(x => x.Completed).NotNull().WithMessage("حدد هل اكتملت الجولة.");
        });
    }
}
