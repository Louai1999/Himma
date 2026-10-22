using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Himma.Api.Features.Account;

public enum AccountStatus
{
    Active,
    Suspended,
    PendingDeletion,
}

/// <summary>
/// Suspend, reactivate, soft-delete, restore and purge accounts. Whenever an account is switched off,
/// its security stamp changes and its refresh tokens are revoked, so it is signed out everywhere at once.
/// </summary>
public sealed class AccountLifecycleService(
    UserManager<ApplicationUser> users,
    RefreshTokenService refreshTokens,
    IOptions<AccountDeletionOptions> deletionOptions)
{
    public static AccountStatus StatusOf(ApplicationUser user) =>
        user.DeletedAtUtc is not null ? AccountStatus.PendingDeletion
        : !user.IsActive ? AccountStatus.Suspended
        : AccountStatus.Active;

    public async Task SuspendAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.IsActive = false;
        await SignOutEverywhereAsync(user, cancellationToken);
    }

    public async Task ReactivateAsync(ApplicationUser user)
    {
        user.IsActive = true;
        await users.UpdateAsync(user);
    }

    /// <summary>Hides the account and schedules it for permanent deletion after the grace period.</summary>
    public async Task SoftDeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.DeletedAtUtc = now;
        user.DeletionScheduledForUtc = now.AddDays(deletionOptions.Value.GraceDays);
        await SignOutEverywhereAsync(user, cancellationToken);
    }

    /// <summary>Cancels a pending deletion. The account becomes active again.</summary>
    public async Task RestoreAsync(ApplicationUser user)
    {
        user.DeletedAtUtc = null;
        user.DeletionScheduledForUtc = null;
        user.IsActive = true;
        await users.UpdateAsync(user);
    }

    /// <summary>
    /// Permanently deletes the account. The database cascades the delete to every row the user owns;
    /// audit log entries are kept because they have no foreign key.
    /// </summary>
    public async Task PurgeAsync(ApplicationUser user)
    {
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not purge user {user.Id}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
    }

    private async Task SignOutEverywhereAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Saves the changes above and invalidates every access token (they carry the stamp).
        await users.UpdateSecurityStampAsync(user);
        await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
    }
}
