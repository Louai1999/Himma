using System.Security.Cryptography;
using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Features.Admin;
using Himma.Api.Infrastructure.Push;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Coaching;

/// <summary>Shared rules for the coach package: who is a coach, links, invite codes, plans and messages.</summary>
public sealed class CoachingService(AppDbContext db, IPushSender push, ILogger<CoachingService> logger)
{
    /// <summary>No 0/O or 1/I/L, so a code read aloud or typed from a poster is hard to get wrong.</summary>
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    /// <summary>Days without any logging before a client is flagged as inactive.</summary>
    public const int InactiveAfterDays = 3;

    /// <summary>
    /// Flags look at the last week only: what the coach should act on now. Over 30 days a client who
    /// joined last week would look off target for the weeks before they started.
    /// </summary>
    public const int AttentionWindowDays = 7;

    /// <summary>Below this share of days on target, a client is flagged for that target.</summary>
    public const double LowAdherencePercent = 50;

    public static bool IsActive(CoachAccount account) => account.ActiveUntilUtc > DateTime.UtcNow;

    /// <summary>Upper case without spaces or dashes, so "hm7k-2q9x" and "HM7K2Q9X" are the same code.</summary>
    public static string NormalizeCode(string? code) =>
        new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public Task<CoachAccount?> FindAccountAsync(string userId, CancellationToken cancellationToken) =>
        db.CoachAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

    public Task<int> ActiveClientCountAsync(string coachUserId, CancellationToken cancellationToken) =>
        db.CoachClients.CountAsync(l => l.CoachUserId == coachUserId && l.Status == CoachLinkStatus.Active, cancellationToken);

    /// <summary>The coach's link to a client, only while it is active. A client of another coach looks the same as no client.</summary>
    public Task<CoachClient?> FindActiveLinkAsync(string coachUserId, string clientUserId, CancellationToken cancellationToken) =>
        db.CoachClients.FirstOrDefaultAsync(
            l => l.CoachUserId == coachUserId && l.ClientUserId == clientUserId && l.Status == CoachLinkStatus.Active, cancellationToken);

    public Task<CoachClient?> FindClientLinkAsync(string clientUserId, CancellationToken cancellationToken) =>
        db.CoachClients.FirstOrDefaultAsync(l => l.ClientUserId == clientUserId && l.Status == CoachLinkStatus.Active, cancellationToken);

    public async Task<string> NewInviteCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = string.Concat(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
            if (!await db.CoachAccounts.AnyAsync(a => a.InviteCode == code, cancellationToken))
            {
                return code;
            }
        }
    }

    /// <summary>Ends every active link of a coach (when the admin removes the package).</summary>
    public Task<int> EndAllLinksAsync(string coachUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return db.CoachClients
            .Where(l => l.CoachUserId == coachUserId && l.Status == CoachLinkStatus.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, CoachLinkStatus.Ended)
                .SetProperty(l => l.EndedAtUtc, now), cancellationToken);
    }

    /// <param name="lastLoggedAtUtc">When the client last logged a meal, water, weight or workout. Opening the app does not count.</param>
    /// <param name="metrics">Computed over <see cref="AttentionWindowDays"/>.</param>
    /// <param name="joinedAtUtc">
    /// When the client joined. In their first week only the days since joining count, so a client who joined
    /// two days ago and hit their targets on both days is on track, not "on target 2 of 7 days".
    /// </param>
    public static IReadOnlyList<string> Attention(UserMetrics metrics, DateTime? lastLoggedAtUtc, DateTime joinedAtUtc, DateTime nowUtc)
    {
        var daysWithCoach = (DateOnly.FromDateTime(nowUtc).DayNumber - DateOnly.FromDateTime(joinedAtUtc).DayNumber) + 1;
        var scale = (double)AttentionWindowDays / Math.Clamp(daysWithCoach, 1, AttentionWindowDays);
        bool Low(double? percent) => percent * scale < LowAdherencePercent;

        var reasons = new List<string>();
        if (lastLoggedAtUtc is null || lastLoggedAtUtc < nowUtc.AddDays(-InactiveAfterDays))
        {
            reasons.Add(AttentionReasons.Inactive);
        }
        if (metrics.Goal is null)
        {
            reasons.Add(AttentionReasons.NoProfile);
            return reasons;
        }
        if (Low(metrics.CalorieAdherencePercent))
        {
            reasons.Add(AttentionReasons.LowCalories);
        }
        if (Low(metrics.ProteinAdherencePercent))
        {
            reasons.Add(AttentionReasons.LowProtein);
        }
        if (Low(metrics.WaterAdherencePercent))
        {
            reasons.Add(AttentionReasons.LowWater);
        }
        return reasons;
    }

    /// <summary>The newest meal, water, weight or workout entry of each user (null when they never logged anything).</summary>
    public async Task<Dictionary<string, DateTime?>> LastLoggedAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var meals = await db.MealEntries.AsNoTracking().Where(m => userIds.Contains(m.UserId))
            .GroupBy(m => m.UserId).Select(g => new { Id = g.Key, Last = g.Max(m => m.CreatedAtUtc) }).ToListAsync(cancellationToken);
        var water = await db.WaterEntries.AsNoTracking().Where(w => userIds.Contains(w.UserId))
            .GroupBy(w => w.UserId).Select(g => new { Id = g.Key, Last = g.Max(w => w.CreatedAtUtc) }).ToListAsync(cancellationToken);
        var weights = await db.WeightEntries.AsNoTracking().Where(w => userIds.Contains(w.UserId))
            .GroupBy(w => w.UserId).Select(g => new { Id = g.Key, Last = g.Max(w => w.Date) }).ToListAsync(cancellationToken);
        var workouts = await db.WorkoutSessions.AsNoTracking().Where(s => userIds.Contains(s.UserId))
            .GroupBy(s => s.UserId).Select(g => new { Id = g.Key, Last = g.Max(s => s.StartedAtUtc) }).ToListAsync(cancellationToken);

        var all = meals.Select(m => (m.Id, At: m.Last))
            .Concat(water.Select(w => (w.Id, At: w.Last)))
            .Concat(weights.Select(w => (w.Id, At: w.Last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))))
            .Concat(workouts.Select(w => (w.Id, At: w.Last)))
            .ToLookup(x => x.Id, x => x.At);

        return userIds.ToDictionary(id => id, id => all[id].Any() ? all[id].Max() : (DateTime?)null);
    }

    /// <summary>Weight change over the window for many users at once (last entry minus first; null with fewer than two).</summary>
    public async Task<Dictionary<string, double?>> WeightChangesAsync(IReadOnlyCollection<string> userIds, DateOnly today, int days, CancellationToken cancellationToken)
    {
        var from = today.AddDays(-(days - 1));
        var entries = await db.WeightEntries.AsNoTracking()
            .Where(w => userIds.Contains(w.UserId) && w.Date >= from && w.Date <= today)
            .Select(w => new { w.UserId, w.Date, w.WeightKg })
            .ToListAsync(cancellationToken);

        return userIds.ToDictionary(id => id, id =>
        {
            var mine = entries.Where(e => e.UserId == id).OrderBy(e => e.Date).ToList();
            return mine.Count >= 2 ? Math.Round(mine[^1].WeightKg - mine[0].WeightKg, 1) : (double?)null;
        });
    }

    /// <summary>
    /// Copies one of the coach's plans into the client's account. The client gets their own copy, which they
    /// can change freely and which stays even if the coaching ends. The coach's own exercises are copied too,
    /// because a client's plan may only use system exercises and the client's own.
    /// </summary>
    public async Task<WorkoutPlan?> CopyPlanToClientAsync(int planId, string coachUserId, string clientUserId, bool activate, CancellationToken cancellationToken)
    {
        var source = await db.WorkoutPlans.AsNoTracking()
            .Include(p => p.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Exercise)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == coachUserId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var copiedExercises = new Dictionary<int, ExerciseDefinition>();
        ExerciseDefinition? ClientExercise(WorkoutPlanItem item)
        {
            var exercise = item.Exercise!;
            if (exercise.OwnerUserId is null)
            {
                return null; // system exercise: shared by everyone, referenced as is
            }
            if (!copiedExercises.TryGetValue(exercise.Id, out var copy))
            {
                copy = new ExerciseDefinition
                {
                    OwnerUserId = clientUserId,
                    Name = exercise.Name,
                    MuscleGroup = exercise.MuscleGroup,
                    Region = exercise.Region,
                    Place = exercise.Place,
                    Equipment = exercise.Equipment,
                    IconKey = exercise.IconKey,
                    DefaultSets = exercise.DefaultSets,
                    DefaultReps = exercise.DefaultReps,
                    Instructions = exercise.Instructions,
                    CreatedAtUtc = now,
                };
                copiedExercises[exercise.Id] = copy;
            }
            return copy;
        }

        var plan = new WorkoutPlan
        {
            UserId = clientUserId,
            Name = source.Name,
            Place = source.Place,
            IsActive = activate || !await db.WorkoutPlans.AnyAsync(p => p.UserId == clientUserId && p.IsActive, cancellationToken),
            CreatedAtUtc = now,
            Days = source.Days.OrderBy(d => d.DayKey).Select(d => new WorkoutPlanDay
            {
                DayKey = d.DayKey,
                IsRest = d.IsRest,
                Focus = d.Focus,
                Items = d.Items.OrderBy(i => i.Order).Select(i =>
                {
                    var copy = ClientExercise(i);
                    return new WorkoutPlanItem
                    {
                        ExerciseId = copy is null ? i.ExerciseId : 0,
                        Exercise = copy,
                        Order = i.Order,
                        Sets = i.Sets,
                        Reps = i.Reps,
                        WeightKg = i.WeightKg,
                        RestSeconds = i.RestSeconds,
                        Notes = i.Notes,
                    };
                }).ToList(),
            }).ToList(),
        };

        if (activate)
        {
            await db.WorkoutPlans
                .Where(p => p.UserId == clientUserId && p.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), cancellationToken);
        }

        db.WorkoutPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    /// <summary>Best effort: a failed notification never fails the message, which is already saved.</summary>
    /// <param name="textEn">English text for devices set to English; defaults to <paramref name="text"/> (a coach's own words).</param>
    public async Task NotifyClientAsync(string clientUserId, string coachName, string text, CancellationToken cancellationToken, string? textEn = null)
    {
        var devices = await db.PushDevices.Where(d => d.UserId == clientUserId).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            var title = device.Language == "en" ? $"Message from {coachName}" : $"رسالة من {coachName}";
            var chosen = device.Language == "en" && textEn is not null ? textEn : text;
            var body = chosen.Length > 140 ? chosen[..140] + "…" : chosen;
            try
            {
                var result = await push.SendAsync(device, new PushPayload(title, body, "coach-note", "/#coach"), cancellationToken);
                if (result == PushSendResult.Gone)
                {
                    db.PushDevices.Remove(device);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Coach message push failed for device {DeviceId}", device.Id);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
