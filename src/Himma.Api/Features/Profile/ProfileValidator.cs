using FluentValidation;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Profile;

public sealed class ProfileRequestValidator : AbstractValidator<ProfileRequest>
{
    public ProfileRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.Gender).NotNull().WithMessage("اختر الجنس.").IsInEnum();
        RuleFor(x => x.Age).NotNull().WithMessage("العمر مطلوب.")
            .InclusiveBetween(13, 100).WithMessage("العمر بين 13 و 100 سنة.");
        RuleFor(x => x.HeightCm).NotNull().WithMessage("الطول مطلوب.")
            .InclusiveBetween(100, 250).WithMessage("الطول بين 100 و 250 سم.");
        RuleFor(x => x.WeightKg).NotNull().WithMessage("الوزن مطلوب.")
            .InclusiveBetween(30, 300).WithMessage("الوزن بين 30 و 300 كغ.");
        RuleFor(x => x.ActivityLevel).NotNull().WithMessage("اختر مستوى النشاط.").IsInEnum();
        RuleFor(x => x.Goal).NotNull().WithMessage("اختر الهدف.").IsInEnum();
        RuleFor(x => x.TargetWeightKg)
            .InclusiveBetween(30, 300).WithMessage("الوزن المستهدف بين 30 و 300 كغ.")
            .When(x => x.TargetWeightKg is not null);
    }
}
