using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Features.Account;

public sealed class DeleteAccountRequest
{
    public string Password { get; init; } = string.Empty;
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty().WithMessage(_ => Lang.T("كلمة المرور مطلوبة.", "Password is required.")).MaximumLength(128);
    }
}

/// <summary>The signed-in user's own account: export everything, or delete the account.</summary>
[Route("api/account")]
public sealed class AccountController(
    UserManager<ApplicationUser> users,
    AccountLifecycleService lifecycle,
    UserDataExporter exporter,
    PasswordReauthentication reauthentication,
    AuditLogger audit,
    RefreshCookie refreshCookie) : HimmaControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var data = await exporter.ExportAsync(UserId, cancellationToken);
        if (data is null)
        {
            return NotFoundProblem();
        }

        await audit.WriteAsync(AuditActions.SelfExport, UserId, null, cancellationToken);
        Response.Headers.ContentDisposition = $"attachment; filename=himma-export-{ApiDate.UtcToday:yyyy-MM-dd}.json";
        return Ok(data);
    }

    /// <summary>
    /// Starts the deletion flow: the account is switched off now and erased after the grace period
    /// (support can restore it until then). Requires the current password.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
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

        if (await users.IsInRoleAsync(user, Roles.Admin) && await ActiveAdminCountAsync() <= 1)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: Lang.T("لا يمكن حذف آخر مدير", "The last admin cannot be deleted"),
                detail: Lang.T("يجب أن يبقى حساب مدير واحد على الأقل.", "At least one admin account must remain."));
        }

        await lifecycle.SoftDeleteAsync(user, cancellationToken);
        await audit.WriteAsync(AuditActions.SelfDelete, user.Id, new { username = user.UserName }, cancellationToken);
        refreshCookie.Delete(Response);
        return NoContent();
    }

    private async Task<int> ActiveAdminCountAsync() =>
        (await users.GetUsersInRoleAsync(Roles.Admin)).Count(a => a.IsActive && a.DeletedAtUtc is null);
}
