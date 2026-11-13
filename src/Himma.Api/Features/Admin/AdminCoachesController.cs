using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Features.Coaching;
using Himma.Api.Infrastructure.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Admin;

/// <summary>
/// Selling the coach package: the admin switches it on for a user, with the number of clients (seats)
/// and the end date that were paid for, and can extend, change or remove it later.
/// </summary>
[Route("api/admin")]
public sealed class AdminCoachesController(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    CoachingService coaching,
    AuditLogger audit) : AdminControllerBase(users)
{
    [HttpGet("coaches")]
    public async Task<ActionResult<IReadOnlyList<AdminCoachRow>>> List(CancellationToken cancellationToken)
    {
        var rows = await db.CoachAccounts.AsNoTracking()
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new
            {
                Account = a,
                u.UserName,
                u.DisplayName,
                ActiveClients = db.CoachClients.Count(l => l.CoachUserId == a.UserId && l.Status == CoachLinkStatus.Active),
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.Account.ActiveUntilUtc)
            .Select(r => ToRow(r.Account, r.UserName, r.DisplayName, r.ActiveClients))
            .ToList();
    }

    [HttpGet("users/{id}/coach")]
    public async Task<ActionResult<AdminCoachRow>> Get(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;

        var account = await coaching.FindAccountAsync(user.Id, cancellationToken);
        if (account is null)
        {
            return NotFoundProblem();
        }
        return ToRow(account, user.UserName, user.DisplayName, await coaching.ActiveClientCountAsync(user.Id, cancellationToken));
    }

    /// <summary>Creates or updates the package. Lowering the seats below the current clients keeps them, but no one new can join.</summary>
    [HttpPut("users/{id}/coach")]
    public async Task<ActionResult<AdminCoachRow>> Set(string id, AdminCoachRequest request, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;

        AdminCoachRequestValidator.TryParseUntil(request.ActiveUntil, out var until);
        var now = DateTime.UtcNow;
        var account = await coaching.FindAccountAsync(user.Id, cancellationToken);
        var created = account is null;
        if (account is null)
        {
            account = new CoachAccount
            {
                UserId = user.Id,
                InviteCode = await coaching.NewInviteCodeAsync(cancellationToken),
                CreatedAtUtc = now,
            };
            db.CoachAccounts.Add(account);
        }

        account.BusinessName = request.BusinessName.Trim();
        account.MaxClients = request.MaxClients;
        // Works through the whole end day, wherever the coach is.
        account.ActiveUntilUtc = until.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        account.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(AuditActions.SetCoach, user.Id, new
        {
            username = user.UserName,
            created,
            account.MaxClients,
            activeUntil = until,
        }, cancellationToken);

        return ToRow(account, user.UserName, user.DisplayName, await coaching.ActiveClientCountAsync(user.Id, cancellationToken));
    }

    /// <summary>Removes the package and ends every coaching link. The clients keep all their data.</summary>
    [HttpDelete("users/{id}/coach")]
    public async Task<IActionResult> Remove(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null) return error!;

        var account = await coaching.FindAccountAsync(user.Id, cancellationToken);
        if (account is null)
        {
            return NotFoundProblem();
        }

        var ended = await coaching.EndAllLinksAsync(user.Id, cancellationToken);
        db.CoachAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(AuditActions.RemoveCoach, user.Id, new { username = user.UserName, endedClients = ended }, cancellationToken);
        return NoContent();
    }

    private static AdminCoachRow ToRow(CoachAccount a, string? username, string displayName, int activeClients) =>
        new(a.UserId, username ?? string.Empty, displayName, a.BusinessName, a.InviteCode, a.MaxClients, activeClients,
            a.ActiveUntilUtc, CoachingService.IsActive(a), a.CreatedAtUtc);
}
