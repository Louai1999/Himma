using System.Security.Cryptography;
using Himma.Api.Domain.Entities;
using Himma.Api.Features.Account;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Features.Admin;

/// <summary>
/// Suspend, reactivate, delete (soft), restore and purge subscriber accounts.
/// Destructive actions require the admin's own password again; every action is audited.
/// </summary>
[Route("api/admin/users/{id}")]
public sealed class AdminActionsController(
    UserManager<ApplicationUser> users,
    AccountLifecycleService lifecycle,
    PasswordReauthentication reauthentication,
    RefreshTokenService refreshTokens,
    AuditLogger audit) : AdminControllerBase(users)
{
    private const string TemporaryPasswordAlphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// For someone who forgot their password and has no recovery email: sets a random temporary password,
    /// shown to the admin once, and makes the user choose a new one at the next sign-in. The admin hands it
    /// over only after checking who is asking. Every device of the user is signed out.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<TemporaryPasswordResponse>> ResetPassword(string id, AdminReauthRequest request, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;
        if (await ReauthenticateAsync(request.AdminPassword, user.Id, cancellationToken) is { } denied) return denied;

        var temporary = string.Concat(Enumerable.Range(0, 12)
            .Select(_ => TemporaryPasswordAlphabet[RandomNumberGenerator.GetInt32(TemporaryPasswordAlphabet.Length)]));
        var token = await Users.GeneratePasswordResetTokenAsync(user);
        var reset = await Users.ResetPasswordAsync(user, token, temporary);
        if (!reset.Succeeded)
        {
            // Practically impossible with 12 random characters; never echo the password in an error.
            return WrongState(Lang.T("تعذّر إنشاء كلمة مرور مؤقتة، حاول مرة أخرى.", "Could not create a temporary password, please try again."));
        }

        user.MustChangePassword = true;
        await Users.UpdateAsync(user);
        await Users.SetLockoutEndDateAsync(user, null);
        await Users.ResetAccessFailedCountAsync(user);
        await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await audit.WriteAsync(AuditActions.ResetPassword, user.Id, new { username = user.UserName }, cancellationToken);
        return new TemporaryPasswordResponse(temporary);
    }

    [HttpPost("suspend")]
    public async Task<IActionResult> Suspend(string id, AdminReauthRequest request, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;
        if (await ReauthenticateAsync(request.AdminPassword, user.Id, cancellationToken) is { } denied) return denied;
        if (AccountLifecycleService.StatusOf(user) != AccountStatus.Active)
        {
            return WrongState(Lang.T("الحساب ليس نشطاً.", "The account is not active."));
        }

        await lifecycle.SuspendAsync(user, cancellationToken);
        await audit.WriteAsync(AuditActions.Suspend, user.Id, new { username = user.UserName }, cancellationToken);
        return NoContent();
    }

    [HttpPost("reactivate")]
    public async Task<IActionResult> Reactivate(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;
        if (AccountLifecycleService.StatusOf(user) != AccountStatus.Suspended)
        {
            return WrongState(Lang.T("الحساب ليس موقوفاً.", "The account is not suspended."));
        }

        await lifecycle.ReactivateAsync(user);
        await audit.WriteAsync(AuditActions.Reactivate, user.Id, new { username = user.UserName }, cancellationToken);
        return NoContent();
    }

    /// <summary>Soft delete: the account is switched off and erased automatically after the grace period.</summary>
    [HttpDelete]
    public async Task<IActionResult> SoftDelete(string id, AdminReauthRequest request, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;
        if (await ReauthenticateAsync(request.AdminPassword, user.Id, cancellationToken) is { } denied) return denied;
        if (AccountLifecycleService.StatusOf(user) == AccountStatus.PendingDeletion)
        {
            return WrongState(Lang.T("الحساب مجدول للحذف أصلاً.", "The account is already scheduled for deletion."));
        }

        await lifecycle.SoftDeleteAsync(user, cancellationToken);
        await audit.WriteAsync(AuditActions.SoftDelete, user.Id,
            new { username = user.UserName, scheduledForUtc = user.DeletionScheduledForUtc }, cancellationToken);
        return NoContent();
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;
        if (AccountLifecycleService.StatusOf(user) != AccountStatus.PendingDeletion)
        {
            return WrongState(Lang.T("الحساب ليس مجدولاً للحذف.", "The account is not scheduled for deletion."));
        }

        await lifecycle.RestoreAsync(user);
        await audit.WriteAsync(AuditActions.Restore, user.Id, new { username = user.UserName }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Permanent delete, right now. Cannot be undone. Requires the admin's password and the
    /// target's username typed exactly.
    /// </summary>
    [HttpPost("purge")]
    public async Task<IActionResult> Purge(string id, PurgeRequest request, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;

        if (!string.Equals(request.ConfirmUsername, user.UserName, StringComparison.Ordinal))
        {
            return InvalidField("confirmUsername", Lang.T("اسم المستخدم المكتوب لا يطابق الحساب.", "The username you typed does not match this account."));
        }
        if (await ReauthenticateAsync(request.AdminPassword, user.Id, cancellationToken) is { } denied) return denied;

        // Audit first, so the record exists even though the account (and its data) will be gone.
        await audit.WriteAsync(AuditActions.Purge, user.Id, new { username = user.UserName }, cancellationToken);
        await lifecycle.PurgeAsync(user, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Checks the admin's password. A wrong password is a 400 on the password field (not 401, which
    /// the app treats as "session expired") and is audited.
    /// </summary>
    private async Task<ActionResult?> ReauthenticateAsync(string password, string targetUserId, CancellationToken cancellationToken)
    {
        var admin = await Users.FindByIdAsync(UserId);
        if (admin is not null && await reauthentication.VerifyAsync(admin, password))
        {
            return null;
        }

        await audit.WriteAsync(AuditActions.ReauthFailed, targetUserId, null, cancellationToken);
        return InvalidField("adminPassword", Lang.T(
            "كلمة المرور غير صحيحة، أو أن الدخول أُوقف مؤقتاً بعد محاولات كثيرة.",
            "The password is incorrect, or it is paused for a while after too many attempts."));
    }

    private ObjectResult WrongState(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: Lang.T("لا يمكن تنفيذ الإجراء الآن", "This action is not possible now"), detail: detail);
}
