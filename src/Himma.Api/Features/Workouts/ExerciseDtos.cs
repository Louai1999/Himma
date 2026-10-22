using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;

namespace Himma.Api.Features.Workouts;

public sealed record ExerciseDto(
    int Id,
    string Name,
    MuscleGroup MuscleGroup,
    BodyRegion Region,
    WorkoutPlace Place,
    string Equipment,
    string IconKey,
    int DefaultSets,
    int DefaultReps,
    string? Instructions,
    bool IsSystem,
    bool IsArchived)
{
    public static ExerciseDto From(ExerciseDefinition e) =>
        new(e.Id, e.Name, e.MuscleGroup, e.Region, e.Place, e.Equipment, e.IconKey, e.DefaultSets, e.DefaultReps,
            e.Instructions, e.OwnerUserId is null, e.IsArchived);
}

public sealed class ExerciseRequest
{
    public string Name { get; init; } = string.Empty;
    public MuscleGroup? MuscleGroup { get; init; }
    public BodyRegion? Region { get; init; }
    public WorkoutPlace? Place { get; init; }
    public string? Equipment { get; init; }
    public string? IconKey { get; init; }
    public int? DefaultSets { get; init; }
    public int? DefaultReps { get; init; }
    public string? Instructions { get; init; }
}

public sealed class ExerciseRequestValidator : AbstractValidator<ExerciseRequest>
{
    public ExerciseRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage("اسم التمرين من 2 إلى 60 حرفاً.");
        RuleFor(x => x.MuscleGroup).NotNull().WithMessage("اختر العضلة.").IsInEnum();
        RuleFor(x => x.Region).NotNull().WithMessage("اختر منطقة العضلة.").IsInEnum();
        RuleFor(x => x.Place).NotNull().WithMessage("اختر مكان التمرين.").IsInEnum();
        RuleFor(x => x.Equipment).MaximumLength(60).WithMessage("الأدوات 60 حرفاً كحد أقصى.");
        RuleFor(x => x.IconKey)
            .Matches("^[a-z0-9-]{1,40}$").When(x => x.IconKey is not null)
            .WithMessage("رمز الأيقونة غير صالح.");
        RuleFor(x => x.DefaultSets).NotNull().WithMessage("عدد الجولات مطلوب.")
            .InclusiveBetween(1, 20).WithMessage("عدد الجولات بين 1 و 20.");
        RuleFor(x => x.DefaultReps).NotNull().WithMessage("عدد التكرارات مطلوب.")
            .InclusiveBetween(1, 100).WithMessage("عدد التكرارات بين 1 و 100.");
        RuleFor(x => x.Instructions).MaximumLength(1000).WithMessage("الشرح 1000 حرف كحد أقصى.");
    }
}
