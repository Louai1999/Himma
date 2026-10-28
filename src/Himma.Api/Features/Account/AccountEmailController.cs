using FluentValidation;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Email;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Account;

public sealed class SetEmailRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class ConfirmEmailRequest
{
    public string Email { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public sealed class RemoveEmailRequest
{
    public string Password { get; init; } = string.Empty;
}

/// <summary>Email is null until one is confirmed. EmailEnabled is false while the server has no email provider.</summary>
public sealed record AccountEmailDto(string? Email, bool EmailEnabled);

public sealed class SetEmailRequestValidator : AbstractValidator<SetEmailRequest>
{
    public SetEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress()
            .WithMessage(_ => Lang.T("اكتب بريداً إلكترونياً صحيحاً.", "Enter a valid email address."));
        RuleFor(x => x.Password).NotEmpty().WithMessage(_ => Lang.T("كلمة المرور مطلوبة.", "Password is required.")).MaximumLength(128);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$")
            .WithMessage(_ => Lang.T("الرمز 6 أرقام.", "The code has 6 digits."));
    }
}

public sealed class RemoveEmailRequestValidator : AbstractValidator<RemoveEmailRequest>
{
    public RemoveEmailRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty().WithMessage(_ => Lang.T("كلمة المرور مطلوبة.", "Password is required.")).MaximumLength(128);
    }
}

/// <summary>
/// The recovery email of the signed-in user. A new email only replaces the old one once the 6-digit code
/// sent to it is entered, so nobody can attach an address they do not own. Changing it needs the password.
/// </summary>
[Route("api/account/email")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AccountEmailController(
    UserManager<ApplicationUser> users,
    PasswordReauthentication reauthentication,
    IEmailSender email) : HimmaControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AccountEmailDto>> Get()
    {
        var user = await users.FindByIdAsync(UserId);
        return user is null ? NotFoundProblem() : new AccountEmailDto(user.EmailConfirmed ? user.Email : null, email.IsEnabled);
    }

    /// <summary>Sends a confirmation code to the new address. Nothing changes until it is confirmed.</summary>
    [HttpPost]
    public async Task<IActionResult> Start(SetEmailRequest request, CancellationToken cancellationToken)
    {
        if (!email.IsEnabled)
        {
            return EmailUnavailable();
        }

        var user = await users.FindByIdAsync(UserId);
        if (user is null)
        {
            return NotFoundProblem();
        }
        if (!await reauthentication.VerifyAsync(user, request.Password))
        {
            return InvalidField("password", Lang.T("كلمة المرور غير صحيحة.", "The password is incorrect."));
        }

        var address = request.Email.Trim();
        if (await IsUsedByAnotherAccountAsync(user.Id, address, cancellationToken))
        {
            return EmailTaken();
        }

        var code = await users.GenerateChangeEmailTokenAsync(user, address);
        if (!await email.SendAsync(EmailTemplates.ConfirmEmail(address, code), cancellationToken))
        {
            return SendFailed();
        }
        return Accepted();
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<AccountEmailDto>> Confirm(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(UserId);
        if (user is null)
        {
            return NotFoundProblem();
        }
        if (await users.IsLockedOutAsync(user))
        {
            return WrongCode();
        }

        var address = request.Email.Trim();
        if (await IsUsedByAnotherAccountAsync(user.Id, address, cancellationToken))
        {
            return EmailTaken();
        }

        // ChangeEmailAsync checks the code, sets the email as confirmed and rotates the security stamp.
        var changed = await users.ChangeEmailAsync(user, address, request.Code);
        if (!changed.Succeeded)
        {
            await users.AccessFailedAsync(user);   // guessing codes counts towards the normal lockout
            return WrongCode();
        }

        await users.ResetAccessFailedCountAsync(user);
        return new AccountEmailDto(user.Email, email.IsEnabled);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(RemoveEmailRequest request)
    {
        var user = await users.FindByIdAsync(UserId);
        if (user is null)
        {
            return NotFoundProblem();
        }
        if (!await reauthentication.VerifyAsync(user, request.Password))
        {
            return InvalidField("password", Lang.T("كلمة المرور غير صحيحة.", "The password is incorrect."));
        }

        await users.SetEmailAsync(user, null);
        return NoContent();
    }

    private Task<bool> IsUsedByAnotherAccountAsync(string userId, string address, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(address);
        return users.Users.AnyAsync(u => u.Id != userId && u.EmailConfirmed && u.NormalizedEmail == normalized, cancellationToken);
    }

    private ActionResult WrongCode() =>
        InvalidField("code", Lang.T("الرمز غير صحيح أو انتهت صلاحيته. اطلب رمزاً جديداً.", "The code is wrong or has expired. Ask for a new one."));

    private ActionResult EmailTaken() =>
        InvalidField("email", Lang.T("هذا البريد مستخدم في حساب آخر.", "This email is used by another account."));

    private ObjectResult EmailUnavailable() =>
        Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: Lang.T("البريد غير متاح حالياً", "Email is not available yet"),
            detail: Lang.T("إرسال البريد لم يُفعَّل بعد في الخادم.", "Sending email is not set up on the server yet."));

    private ObjectResult SendFailed() =>
        Problem(statusCode: StatusCodes.Status502BadGateway,
            title: Lang.T("تعذّر إرسال البريد", "Could not send the email"),
            detail: Lang.T("حاول مرة أخرى بعد قليل.", "Please try again shortly."));
}
