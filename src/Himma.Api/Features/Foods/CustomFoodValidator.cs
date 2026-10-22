using FluentValidation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Foods;

public sealed class CustomFoodRequestValidator : AbstractValidator<CustomFoodRequest>
{
    public CustomFoodRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage(_ => Lang.T("اسم الطعام من 2 إلى 60 حرفاً.", "Food name must be 2 to 60 characters."));
        RuleFor(x => x.KcalPer100g).NotNull().WithMessage(_ => Lang.T("السعرات لكل 100 غ مطلوبة.", "Calories per 100 g are required."))
            .InclusiveBetween(0, 900).WithMessage(_ => Lang.T("السعرات لكل 100 غ بين 0 و 900.", "Calories per 100 g must be between 0 and 900."));
        RuleFor(x => x.ProteinPer100g).NotNull().WithMessage(_ => Lang.T("البروتين لكل 100 غ مطلوب.", "Protein per 100 g is required."))
            .InclusiveBetween(0, 100).WithMessage(_ => Lang.T("البروتين لكل 100 غ بين 0 و 100.", "Protein per 100 g must be between 0 and 100."));
    }
}
