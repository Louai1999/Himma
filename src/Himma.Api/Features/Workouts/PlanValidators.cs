using FluentValidation;

namespace Himma.Api.Features.Workouts;

public sealed class PlanRequestValidator : AbstractValidator<PlanRequest>
{
    public PlanRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage("اسم الخطة من 2 إلى 60 حرفاً.");
        RuleFor(x => x.Place).NotNull().WithMessage("اختر مكان التمرين.").IsInEnum();
    }
}

public sealed class PlanDayRequestValidator : AbstractValidator<PlanDayRequest>
{
    public const int MaxItemsPerDay = 20;

    public PlanDayRequestValidator()
    {
        RuleFor(x => x.IsRest).NotNull().WithMessage("حدد هل هو يوم راحة.");
        RuleFor(x => x.Focus).Cascade(CascadeMode.Stop).NotNull()
            .Must(f => f.Count <= 9).WithMessage("اختر 9 عضلات كحد أقصى.");
        RuleForEach(x => x.Focus).IsInEnum();
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotNull()
            .Must(items => items.Count <= MaxItemsPerDay).WithMessage($"{MaxItemsPerDay} تمريناً كحد أقصى في اليوم.");

        When(x => x.IsRest == true, () =>
        {
            RuleFor(x => x.Items).Empty().WithMessage("يوم الراحة لا يحتوي تمارين.");
            RuleFor(x => x.Focus).Empty().WithMessage("يوم الراحة لا يحتوي عضلات.");
        });

        RuleForEach(x => x.Items).SetValidator(new PlanItemRequestValidator());
    }
}

public sealed class PlanItemRequestValidator : AbstractValidator<PlanItemRequest>
{
    public PlanItemRequestValidator()
    {
        RuleFor(x => x.ExerciseId).NotNull().WithMessage("اختر التمرين.");
        RuleFor(x => x.Sets).NotNull().WithMessage("عدد الجولات مطلوب.")
            .InclusiveBetween(1, 20).WithMessage("عدد الجولات بين 1 و 20.");
        RuleFor(x => x.Reps).NotNull().WithMessage("عدد التكرارات مطلوب.")
            .InclusiveBetween(1, 100).WithMessage("عدد التكرارات بين 1 و 100.");
        RuleFor(x => x.WeightKg).InclusiveBetween(0, 500).When(x => x.WeightKg is not null)
            .WithMessage("الوزن بين 0 و 500 كغ.");
        RuleFor(x => x.RestSeconds).InclusiveBetween(0, 600).When(x => x.RestSeconds is not null)
            .WithMessage("وقت الراحة بين 0 و 600 ثانية.");
        RuleFor(x => x.Notes).MaximumLength(300).WithMessage("الملاحظات 300 حرف كحد أقصى.");
    }
}
