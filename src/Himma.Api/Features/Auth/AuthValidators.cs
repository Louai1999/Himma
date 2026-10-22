using FluentValidation;

namespace Himma.Api.Features.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب.")
            .Length(3, 30).WithMessage("اسم المستخدم من 3 إلى 30 حرفاً.")
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("اسم المستخدم يقبل الحروف الإنجليزية والأرقام والرموز . _ - فقط.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .Length(8, 128).WithMessage("كلمة المرور من 8 إلى 128 حرفاً.");

        RuleFor(x => x.DisplayName)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage("الاسم الظاهر من 2 إلى 60 حرفاً.");

        RuleFor(x => x.AcceptPrivacy)
            .Equal(true).WithMessage("يجب الموافقة على إشعار الخصوصية لإنشاء الحساب.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("اسم المستخدم مطلوب.").MaximumLength(30);
        RuleFor(x => x.Password).NotEmpty().WithMessage("كلمة المرور مطلوبة.").MaximumLength(128);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة.").MaximumLength(128);
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة.")
            .Length(8, 128).WithMessage("كلمة المرور من 8 إلى 128 حرفاً.")
            .NotEqual(x => x.CurrentPassword).WithMessage("كلمة المرور الجديدة يجب أن تختلف عن الحالية.");
    }
}
