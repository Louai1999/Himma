using System.Security.Claims;
using System.Text.Json;
using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Auth;

namespace Himma.Api.Infrastructure.Audit;

/// <summary>Names of the actions written to the audit log.</summary>
public static class AuditActions
{
    public const string ViewUser = "admin.view_user";
    public const string ViewMeals = "admin.view_meals";
    public const string ExportUser = "admin.export_user";
    public const string Suspend = "admin.suspend";
    public const string Reactivate = "admin.reactivate";
    public const string SoftDelete = "admin.soft_delete";
    public const string Restore = "admin.restore";
    public const string Purge = "admin.purge";
    public const string ReauthFailed = "admin.reauth_failed";
    public const string ResetPassword = "admin.reset_password";
    public const string SystemPurge = "system.purge";
    public const string SelfDelete = "user.self_delete";
    public const string SelfExport = "user.export";

    public static readonly string[] All =
        [ViewUser, ViewMeals, ExportUser, Suspend, Reactivate, SoftDelete, Restore, Purge, ReauthFailed, ResetPassword, SystemPurge, SelfDelete, SelfExport];
}

/// <summary>
/// Writes who did what to whom, when and from where. Details are small JSON objects and must
/// never contain passwords, tokens or other secrets.
/// </summary>
public sealed class AuditLogger(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public const string SystemActor = "system";
    private const int MaxDetailsLength = 4000;

    /// <summary>Logs an action by the signed-in user of the current request.</summary>
    public Task WriteAsync(string action, string? targetUserId, object? details, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;
        var user = http?.User;
        var actorId = user?.FindFirstValue(HimmaClaims.Subject) ?? "anonymous";
        var actorRole = user?.IsInRole(Roles.Admin) == true ? Roles.Admin : Roles.User;
        return SaveAsync(actorId, actorRole, action, targetUserId, details, http?.Connection.RemoteIpAddress?.ToString(), cancellationToken);
    }

    /// <summary>Logs an action taken by the server itself (for example the scheduled purge).</summary>
    public Task WriteSystemAsync(string action, string? targetUserId, object? details, CancellationToken cancellationToken) =>
        SaveAsync(SystemActor, SystemActor, action, targetUserId, details, null, cancellationToken);

    private async Task SaveAsync(
        string actorId, string actorRole, string action, string? targetUserId, object? details, string? ip, CancellationToken cancellationToken)
    {
        var json = details is null ? "{}" : JsonSerializer.Serialize(details);
        if (json.Length > MaxDetailsLength)
        {
            json = json[..MaxDetailsLength];
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            ActorRole = actorRole,
            Action = action,
            TargetUserId = targetUserId,
            Details = json,
            IpAddress = ip,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
