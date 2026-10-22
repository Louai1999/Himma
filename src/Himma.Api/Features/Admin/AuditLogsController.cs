using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Admin;

/// <summary>Read-only audit trail, newest first. There is deliberately no endpoint to edit or delete it.</summary>
[Route("api/admin/audit-logs")]
public sealed class AuditLogsController(UserManager<ApplicationUser> users, AppDbContext db) : AdminControllerBase(users)
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogRow>>> List([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            var actorIds = await IdsForAsync(query.Actor.Trim(), cancellationToken);
            logs = logs.Where(l => actorIds.Contains(l.ActorUserId));
        }
        if (!string.IsNullOrWhiteSpace(query.Target))
        {
            var targetIds = await IdsForAsync(query.Target.Trim(), cancellationToken);
            logs = logs.Where(l => l.TargetUserId != null && targetIds.Contains(l.TargetUserId));
        }
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logs = logs.Where(l => l.Action.StartsWith(action));
        }
        if (!string.IsNullOrEmpty(query.From))
        {
            var from = ApiDate.Parse(query.From).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            logs = logs.Where(l => l.CreatedAtUtc >= from);
        }
        if (!string.IsNullOrEmpty(query.To))
        {
            var toExclusive = ApiDate.Parse(query.To).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            logs = logs.Where(l => l.CreatedAtUtc < toExclusive);
        }

        var total = await logs.CountAsync(cancellationToken);
        var page = await logs
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Show usernames where the account still exists (purged accounts only keep their id).
        var ids = page.Select(l => l.ActorUserId).Concat(page.Select(l => l.TargetUserId).OfType<string>()).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken);

        var rows = page.Select(l => new AuditLogRow(
            l.Id, l.CreatedAtUtc, l.ActorUserId, names.GetValueOrDefault(l.ActorUserId), l.ActorRole, l.Action,
            l.TargetUserId, l.TargetUserId is null ? null : names.GetValueOrDefault(l.TargetUserId), l.Details, l.IpAddress)).ToList();

        return new PagedResult<AuditLogRow>(rows, query.Page, query.PageSize, total);
    }

    /// <summary>The value itself (an id, or "system") plus the id of the user with that username, if any.</summary>
    private async Task<List<string>> IdsForAsync(string idOrUsername, CancellationToken cancellationToken)
    {
        var normalized = idOrUsername.ToUpperInvariant();
        var ids = await db.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        ids.Add(idOrUsername);
        return ids;
    }
}
