using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Localization;

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
    /// <summary>System exercises come back in the request's language; user exercises as written.</summary>
    public static ExerciseDto From(ExerciseDefinition e) =>
        new(e.Id, Lang.Pick(e.Name, e.NameEn), e.MuscleGroup, e.Region, e.Place, Lang.Pick(e.Equipment, e.EquipmentEn), e.IconKey,
            e.DefaultSets, e.DefaultReps, Lang.PickOptional(e.Instructions, e.InstructionsEn), e.OwnerUserId is null, e.IsArchived);
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
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage(_ => Lang.T("اسم التمرين من 2 إلى 60 حرفاً.", "Exercise name must be 2 to 60 characters."));
        RuleFor(x => x.MuscleGroup).NotNull().WithMessage(_ => Lang.T("اختر العضلة.", "Choose the muscle.")).IsInEnum();
        RuleFor(x => x.Region).NotNull().WithMessage(_ => Lang.T("اختر منطقة العضلة.", "Choose the muscle region.")).IsInEnum();
        RuleFor(x => x.Place).NotNull().WithMessage(_ => Lang.T("اختر مكان التمرين.", "Choose where you train.")).IsInEnum();
        RuleFor(x => x.Equipment).MaximumLength(60).WithMessage(_ => Lang.T("الأدوات 60 حرفاً كحد أقصى.", "Equipment can be at most 60 characters."));
        RuleFor(x => x.IconKey)
            .Matches("^[a-z0-9-]{1,40}$").When(x => x.IconKey is not null)
            .WithMessage(_ => Lang.T("رمز الأيقونة غير صالح.", "The icon key is not valid."));
        RuleFor(x => x.DefaultSets).NotNull().WithMessage(_ => Lang.T("عدد الجولات مطلوب.", "Number of sets is required."))
            .InclusiveBetween(1, 20).WithMessage(_ => Lang.T("عدد الجولات بين 1 و 20.", "Sets must be between 1 and 20."));
        RuleFor(x => x.DefaultReps).NotNull().WithMessage(_ => Lang.T("عدد التكرارات مطلوب.", "Number of reps is required."))
            .InclusiveBetween(1, 100).WithMessage(_ => Lang.T("عدد التكرارات بين 1 و 100.", "Reps must be between 1 and 100."));
        RuleFor(x => x.Instructions).MaximumLength(1000).WithMessage(_ => Lang.T("الشرح 1000 حرف كحد أقصى.", "Instructions can be at most 1000 characters."));
    }
}
