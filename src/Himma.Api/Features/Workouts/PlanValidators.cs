using FluentValidation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Workouts;

public sealed class PlanRequestValidator : AbstractValidator<PlanRequest>
{
    public PlanRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage(_ => Lang.T("اسم الخطة من 2 إلى 60 حرفاً.", "Plan name must be 2 to 60 characters."));
        RuleFor(x => x.Place).NotNull().WithMessage(_ => Lang.T("اختر مكان التمرين.", "Choose where you train.")).IsInEnum();
    }
}

public sealed class PlanDayRequestValidator : AbstractValidator<PlanDayRequest>
{
    public const int MaxItemsPerDay = 20;

    public PlanDayRequestValidator()
    {
        RuleFor(x => x.IsRest).NotNull().WithMessage(_ => Lang.T("حدد هل هو يوم راحة.", "Choose whether it is a rest day."));
        RuleFor(x => x.Focus).Cascade(CascadeMode.Stop).NotNull()
            .Must(f => f.Count <= 9).WithMessage(_ => Lang.T("اختر 9 عضلات كحد أقصى.", "Choose at most 9 muscles."));
        RuleForEach(x => x.Focus).IsInEnum();
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotNull()
            .Must(items => items.Count <= MaxItemsPerDay).WithMessage(_ => Lang.T($"{MaxItemsPerDay} تمريناً كحد أقصى في اليوم.", $"At most {MaxItemsPerDay} exercises per day."));

        When(x => x.IsRest == true, () =>
        {
            RuleFor(x => x.Items).Empty().WithMessage(_ => Lang.T("يوم الراحة لا يحتوي تمارين.", "A rest day has no exercises."));
            RuleFor(x => x.Focus).Empty().WithMessage(_ => Lang.T("يوم الراحة لا يحتوي عضلات.", "A rest day has no muscles."));
        });

        RuleForEach(x => x.Items).SetValidator(new PlanItemRequestValidator());
    }
}

public sealed class PlanItemRequestValidator : AbstractValidator<PlanItemRequest>
{
    public PlanItemRequestValidator()
    {
        RuleFor(x => x.ExerciseId).NotNull().WithMessage(_ => Lang.T("اختر التمرين.", "Choose the exercise."));
        RuleFor(x => x.Sets).NotNull().WithMessage(_ => Lang.T("عدد الجولات مطلوب.", "Number of sets is required."))
            .InclusiveBetween(1, 20).WithMessage(_ => Lang.T("عدد الجولات بين 1 و 20.", "Sets must be between 1 and 20."));
        RuleFor(x => x.Reps).NotNull().WithMessage(_ => Lang.T("عدد التكرارات مطلوب.", "Number of reps is required."))
            .InclusiveBetween(1, 100).WithMessage(_ => Lang.T("عدد التكرارات بين 1 و 100.", "Reps must be between 1 and 100."));
        RuleFor(x => x.WeightKg).InclusiveBetween(0, 500).When(x => x.WeightKg is not null)
            .WithMessage(_ => Lang.T("الوزن بين 0 و 500 كغ.", "Weight must be between 0 and 500 kg."));
        RuleFor(x => x.RestSeconds).InclusiveBetween(0, 600).When(x => x.RestSeconds is not null)
            .WithMessage(_ => Lang.T("وقت الراحة بين 0 و 600 ثانية.", "Rest must be between 0 and 600 seconds."));
        RuleFor(x => x.Notes).MaximumLength(300).WithMessage(_ => Lang.T("الملاحظات 300 حرف كحد أقصى.", "Notes can be at most 300 characters."));
    }
}
