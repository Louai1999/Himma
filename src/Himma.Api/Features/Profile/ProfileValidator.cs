using FluentValidation;
using Himma.Api.Infrastructure.Validation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Profile;

public sealed class ProfileRequestValidator : AbstractValidator<ProfileRequest>
{
    public ProfileRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.Gender).NotNull().WithMessage(_ => Lang.T("اختر الجنس.", "Choose your sex.")).IsInEnum();
        RuleFor(x => x.Age).NotNull().WithMessage(_ => Lang.T("العمر مطلوب.", "Age is required."))
            .InclusiveBetween(13, 100).WithMessage(_ => Lang.T("العمر بين 13 و 100 سنة.", "Age must be between 13 and 100."));
        RuleFor(x => x.HeightCm).NotNull().WithMessage(_ => Lang.T("الطول مطلوب.", "Height is required."))
            .InclusiveBetween(100, 250).WithMessage(_ => Lang.T("الطول بين 100 و 250 سم.", "Height must be between 100 and 250 cm."));
        RuleFor(x => x.WeightKg).NotNull().WithMessage(_ => Lang.T("الوزن مطلوب.", "Weight is required."))
            .InclusiveBetween(30, 300).WithMessage(_ => Lang.T("الوزن بين 30 و 300 كغ.", "Weight must be between 30 and 300 kg."));
        RuleFor(x => x.ActivityLevel).NotNull().WithMessage(_ => Lang.T("اختر مستوى النشاط.", "Choose your activity level.")).IsInEnum();
        RuleFor(x => x.Goal).NotNull().WithMessage(_ => Lang.T("اختر الهدف.", "Choose your goal.")).IsInEnum();
        RuleFor(x => x.TargetWeightKg)
            .InclusiveBetween(30, 300).WithMessage(_ => Lang.T("الوزن المستهدف بين 30 و 300 كغ.", "Target weight must be between 30 and 300 kg."))
            .When(x => x.TargetWeightKg is not null);
    }
}
