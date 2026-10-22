using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Features.Account;

/// <summary>Permanently deletes soft-deleted accounts once their grace period has ended.</summary>
public sealed class ScheduledPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<AccountDeletionOptions> options,
    ILogger<ScheduledPurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.PurgeIntervalMinutes)));
        do
        {
            try
            {
                await PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Try again on the next tick; one failure must not stop the job.
                logger.LogError(ex, "Scheduled purge failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Purges every account whose deletion date has passed. Returns how many were purged.</summary>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var lifecycle = scope.ServiceProvider.GetRequiredService<AccountLifecycleService>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogger>();

        var now = DateTime.UtcNow;
        var due = await db.Users
            .Where(u => u.DeletionScheduledForUtc != null && u.DeletionScheduledForUtc <= now)
            .ToListAsync(cancellationToken);

        var purged = 0;
        foreach (var user in due)
        {
            if (await users.IsInRoleAsync(user, Roles.Admin))
            {
                continue; // admins are never purged automatically
            }

            await audit.WriteSystemAsync(AuditActions.SystemPurge, user.Id, new { username = user.UserName }, cancellationToken);
            await lifecycle.PurgeAsync(user);
            purged++;
        }

        if (purged > 0)
        {
            logger.LogInformation("Purged {Count} account(s) after their grace period", purged);
        }
        return purged;
    }
}
