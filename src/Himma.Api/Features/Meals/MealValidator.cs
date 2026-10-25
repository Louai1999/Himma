using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Infrastructure.Validation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Meals;

public sealed class MealRequestValidator : AbstractValidator<MealRequest>
{
    /// <summary>More than this for a single meal is almost certainly a typo or a bad AI estimate.</summary>
    public const double MaxMealCalories = 5000;

    public MealRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.MealType).NotNull().WithMessage(_ => Lang.T("اختر نوع الوجبة.", "Choose the meal.")).IsInEnum();
        RuleFor(x => x.Source).NotNull().WithMessage(_ => Lang.T("مصدر الوجبة مطلوب.", "Meal source is required.")).IsInEnum()
            // Supplement entries are only created by logging a supplement dose.
            .NotEqual(MealSource.Supplement).WithMessage(_ => Lang.T("مصدر الوجبة غير صحيح.", "Invalid meal source."));

        When(x => x.Source == MealSource.Manual, () =>
        {
            RuleFor(x => x.FoodId).NotNull().WithMessage(_ => Lang.T("اختر طعاماً من القائمة.", "Choose a food from the list."));
            RuleFor(x => x.Grams).NotNull().WithMessage(_ => Lang.T("الكمية بالغرام مطلوبة.", "Amount in grams is required."))
                .InclusiveBetween(1, 3000).WithMessage(_ => Lang.T("الكمية بين 1 و 3000 غ.", "Amount must be between 1 and 3000 g."));
        });

        When(x => x.Source == MealSource.Custom, () =>
        {
            RuleFor(x => x.CustomFoodId).NotNull().WithMessage(_ => Lang.T("اختر طعاماً من أطعمتك.", "Choose one of your foods."));
            RuleFor(x => x.Grams).NotNull().WithMessage(_ => Lang.T("الكمية بالغرام مطلوبة.", "Amount in grams is required."))
                .InclusiveBetween(1, 3000).WithMessage(_ => Lang.T("الكمية بين 1 و 3000 غ.", "Amount must be between 1 and 3000 g."));
        });

        When(x => x.Source == MealSource.Ai, () =>
        {
            RuleFor(x => x.Name)
                .Must(name => name is not null && name.Trim().Length is >= 2 and <= 120)
                .WithMessage(_ => Lang.T("وصف الوجبة من 2 إلى 120 حرفاً.", "Meal description must be 2 to 120 characters."));
            RuleFor(x => x.Calories).NotNull().WithMessage(_ => Lang.T("السعرات مطلوبة.", "Calories are required."))
                .InclusiveBetween(0, MaxMealCalories).WithMessage(_ => Lang.T("السعرات للوجبة الواحدة بين 0 و 5000.", "Calories for one meal must be between 0 and 5000."));
            RuleFor(x => x.ProteinG).NotNull().WithMessage(_ => Lang.T("البروتين مطلوب.", "Protein is required."))
                .InclusiveBetween(0, 500).WithMessage(_ => Lang.T("البروتين بين 0 و 500 غ.", "Protein must be between 0 and 500 g."));
            RuleFor(x => x.CarbsG).InclusiveBetween(0, 1000).When(x => x.CarbsG is not null)
                .WithMessage(_ => Lang.T("الكربوهيدرات بين 0 و 1000 غ.", "Carbs must be between 0 and 1000 g."));
            RuleFor(x => x.FatG).InclusiveBetween(0, 500).When(x => x.FatG is not null)
                .WithMessage(_ => Lang.T("الدهون بين 0 و 500 غ.", "Fat must be between 0 and 500 g."));
            RuleFor(x => x.Grams).InclusiveBetween(0, 3000).When(x => x.Grams is not null)
                .WithMessage(_ => Lang.T("الكمية بين 0 و 3000 غ.", "Amount must be between 0 and 3000 g."));
        });
    }
}
