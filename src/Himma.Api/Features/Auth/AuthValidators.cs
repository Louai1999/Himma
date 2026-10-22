using FluentValidation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(_ => Lang.T("اسم المستخدم مطلوب.", "Username is required."))
            .Length(3, 30).WithMessage(_ => Lang.T("اسم المستخدم من 3 إلى 30 حرفاً.", "Username must be 3 to 30 characters."))
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage(_ => Lang.T("اسم المستخدم يقبل الحروف الإنجليزية والأرقام والرموز . _ - فقط.", "Username may only contain English letters, digits and . _ -"));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => Lang.T("كلمة المرور مطلوبة.", "Password is required."))
            .Length(8, 128).WithMessage(_ => Lang.T("كلمة المرور من 8 إلى 128 حرفاً.", "Password must be 8 to 128 characters."));

        RuleFor(x => x.DisplayName)
            .Must(name => name.Trim().Length is >= 2 and <= 60).WithMessage(_ => Lang.T("الاسم الظاهر من 2 إلى 60 حرفاً.", "Display name must be 2 to 60 characters."));

        RuleFor(x => x.AcceptPrivacy)
            .Equal(true).WithMessage(_ => Lang.T("يجب الموافقة على إشعار الخصوصية لإنشاء الحساب.", "You need to accept the privacy notice to create an account."));
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage(_ => Lang.T("اسم المستخدم مطلوب.", "Username is required.")).MaximumLength(30);
        RuleFor(x => x.Password).NotEmpty().WithMessage(_ => Lang.T("كلمة المرور مطلوبة.", "Password is required.")).MaximumLength(128);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(_ => Lang.T("كلمة المرور الحالية مطلوبة.", "Current password is required.")).MaximumLength(128);
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(_ => Lang.T("كلمة المرور الجديدة مطلوبة.", "New password is required."))
            .Length(8, 128).WithMessage(_ => Lang.T("كلمة المرور من 8 إلى 128 حرفاً.", "Password must be 8 to 128 characters."))
            .NotEqual(x => x.CurrentPassword).WithMessage(_ => Lang.T("كلمة المرور الجديدة يجب أن تختلف عن الحالية.", "The new password must be different from the current one."));
    }
}
