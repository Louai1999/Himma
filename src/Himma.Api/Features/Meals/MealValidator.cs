using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Meals;

public sealed class MealRequestValidator : AbstractValidator<MealRequest>
{
    /// <summary>More than this for a single meal is almost certainly a typo or a bad AI estimate.</summary>
    public const double MaxMealCalories = 5000;

    public MealRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.MealType).NotNull().WithMessage("اختر نوع الوجبة.").IsInEnum();
        RuleFor(x => x.Source).NotNull().WithMessage("مصدر الوجبة مطلوب.").IsInEnum();

        When(x => x.Source == MealSource.Manual, () =>
        {
            RuleFor(x => x.FoodId).NotNull().WithMessage("اختر طعاماً من القائمة.");
            RuleFor(x => x.Grams).NotNull().WithMessage("الكمية بالغرام مطلوبة.")
                .InclusiveBetween(1, 3000).WithMessage("الكمية بين 1 و 3000 غ.");
        });

        When(x => x.Source == MealSource.Custom, () =>
        {
            RuleFor(x => x.CustomFoodId).NotNull().WithMessage("اختر طعاماً من أطعمتك.");
            RuleFor(x => x.Grams).NotNull().WithMessage("الكمية بالغرام مطلوبة.")
                .InclusiveBetween(1, 3000).WithMessage("الكمية بين 1 و 3000 غ.");
        });

        When(x => x.Source == MealSource.Ai, () =>
        {
            RuleFor(x => x.Name)
                .Must(name => name is not null && name.Trim().Length is >= 2 and <= 120)
                .WithMessage("وصف الوجبة من 2 إلى 120 حرفاً.");
            RuleFor(x => x.Calories).NotNull().WithMessage("السعرات مطلوبة.")
                .InclusiveBetween(0, MaxMealCalories).WithMessage("السعرات للوجبة الواحدة بين 0 و 5000.");
            RuleFor(x => x.ProteinG).NotNull().WithMessage("البروتين مطلوب.")
                .InclusiveBetween(0, 500).WithMessage("البروتين بين 0 و 500 غ.");
            RuleFor(x => x.CarbsG).InclusiveBetween(0, 1000).When(x => x.CarbsG is not null)
                .WithMessage("الكربوهيدرات بين 0 و 1000 غ.");
            RuleFor(x => x.FatG).InclusiveBetween(0, 500).When(x => x.FatG is not null)
                .WithMessage("الدهون بين 0 و 500 غ.");
            RuleFor(x => x.Grams).InclusiveBetween(0, 3000).When(x => x.Grams is not null)
                .WithMessage("الكمية بين 0 و 3000 غ.");
        });
    }
}
