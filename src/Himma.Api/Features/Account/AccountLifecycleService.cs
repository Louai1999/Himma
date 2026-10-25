using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
    AppDbContext db,
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
    /// Permanently deletes the account and everything it owns, in one transaction. Audit log entries are
    /// kept because they have no foreign key.
    /// Workout rows are removed first, explicitly: they reference the user's own exercises, and
    /// PostgreSQL checks those references between the cascade steps, so leaving it all to the
    /// cascade from the user row would fail there.
    /// </summary>
    public async Task PurgeAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.WorkoutSetLogs.Where(l => db.WorkoutSessions.Any(s => s.Id == l.SessionId && s.UserId == user.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await db.WorkoutSessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await db.WorkoutPlanItems.Where(i => db.WorkoutPlanDays.Any(d => d.Id == i.PlanDayId && d.Plan!.UserId == user.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await db.WorkoutPlans.Where(p => p.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        // Intakes reference the user, a supplement and a meal; remove them before those cascades run.
        await db.SupplementIntakes.Where(i => i.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await db.Supplements.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not purge user {user.Id}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SignOutEverywhereAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Saves the changes above and invalidates every access token (they carry the stamp).
        await users.UpdateSecurityStampAsync(user);
        await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
    }
}
