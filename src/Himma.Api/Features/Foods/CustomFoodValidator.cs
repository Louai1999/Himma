using FluentValidation;

namespace Himma.Api.Features.Foods;

public sealed class CustomFoodRequestValidator : AbstractValidator<CustomFoodRequest>
{
    public CustomFoodRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage("اسم الطعام من 2 إلى 60 حرفاً.");
        RuleFor(x => x.KcalPer100g).NotNull().WithMessage("السعرات لكل 100 غ مطلوبة.")
            .InclusiveBetween(0, 900).WithMessage("السعرات لكل 100 غ بين 0 و 900.");
        RuleFor(x => x.ProteinPer100g).NotNull().WithMessage("البروتين لكل 100 غ مطلوب.")
            .InclusiveBetween(0, 100).WithMessage("البروتين لكل 100 غ بين 0 و 100.");
    }
}
