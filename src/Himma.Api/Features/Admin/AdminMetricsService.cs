using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Account;
using Himma.Api.Features.History;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Admin;

public sealed record UserMetrics(
    int Streak,
    double ConsistencyPercent,
    double? CalorieAdherencePercent,
    double? ProteinAdherencePercent,
    double? WaterAdherencePercent,
    DateTime? LastActivityAtUtc,
    Goal? Goal);

/// <summary>
/// Per-user numbers for the admin dashboard, computed for many users at once with a handful of
/// grouped queries (never one query per user).
/// </summary>
public sealed class AdminMetricsService(AppDbContext db, HistoryService history)
{
    public const int WindowDays = 30;
    private const int StreakLookbackDays = 400;
    private const int SignupWeeks = 12;

    /// <summary>Every non-admin account (subscribers), whatever its status.</summary>
    public IQueryable<ApplicationUser> Subscribers() =>
        db.Users.Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin)));

    public async Task<Dictionary<string, UserMetrics>> ComputeAsync(IReadOnlyCollection<string> userIds, DateOnly today, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var windowStart = today.AddDays(-(WindowDays - 1));
        var streakStart = today.AddDays(-StreakLookbackDays);

        var mealDays = await db.MealEntries.AsNoTracking()
            .Where(m => ids.Contains(m.UserId) && m.Date >= streakStart && m.Date <= today)
            .GroupBy(m => new { m.UserId, m.Date })
            .Select(g => new { g.Key.UserId, g.Key.Date, Calories = g.Sum(m => m.Calories), Protein = g.Sum(m => m.ProteinG), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var waterDays = await db.WaterEntries.AsNoTracking()
            .Where(w => ids.Contains(w.UserId) && w.Date >= windowStart && w.Date <= today)
            .GroupBy(w => new { w.UserId, w.Date })
            .Select(g => new { g.Key.UserId, g.Key.Date, Ml = g.Sum(w => w.Ml) })
            .ToListAsync(cancellationToken);

        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var lastActivity = await LastActivityAsync(ids, cancellationToken);
        var thresholds = history.Thresholds;
        var mealsByUser = mealDays.ToLookup(d => d.UserId);
        var waterByUser = waterDays.ToLookup(d => d.UserId);

        var result = new Dictionary<string, UserMetrics>();
        foreach (var id in ids)
        {
            var meals = mealsByUser[id].ToList();
            var waterByDate = waterByUser[id].ToDictionary(w => w.Date, w => w.Ml);
            var mealsByDate = meals.Where(m => m.Date >= windowStart).ToDictionary(m => m.Date);

            var dayTotals = mealsByDate.Keys.Union(waterByDate.Keys)
                .Select(date =>
                {
                    mealsByDate.TryGetValue(date, out var m);
                    return new DayTotals(date, m?.Calories ?? 0, m?.Protein ?? 0, waterByDate.GetValueOrDefault(date), m?.Count ?? 0);
                })
                .ToList();

            profiles.TryGetValue(id, out var profile);
            var targets = profile is null ? null : TargetCalculator.Calculate(profile);
            var summary = AdherenceCalculator.Summarize(dayTotals, today, WindowDays, targets, thresholds);

            result[id] = new UserMetrics(
                AdherenceCalculator.Streak(meals.Select(m => m.Date), today),
                summary.ConsistencyPercent,
                summary.CalorieAdherencePercent,
                summary.ProteinAdherencePercent,
                summary.WaterAdherencePercent,
                lastActivity.GetValueOrDefault(id),
                profile?.Goal);
        }
        return result;
    }

    public async Task<AdminOverview> OverviewAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var subscribers = await Subscribers().AsNoTracking()
            .Select(u => new { u.Id, u.CreatedAtUtc, u.IsActive, u.DeletedAtUtc })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var current = subscribers.Where(u => u.DeletedAtUtc is null).ToList();
        var metrics = await ComputeAsync(current.Select(u => u.Id).ToList(), today, cancellationToken);

        bool ActiveRecently(string id) => metrics.TryGetValue(id, out var m) && m.LastActivityAtUtc >= now.AddDays(-7);
        double? Average(IEnumerable<double?> values)
        {
            var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return list.Count == 0 ? null : Math.Round(list.Average(), 1);
        }

        var veterans = current.Where(u => u.CreatedAtUtc <= now.AddDays(-30)).ToList();

        var firstWeekStart = today.AddDays(-(SignupWeeks * 7 - 1));
        var weekly = Enumerable.Range(0, SignupWeeks)
            .Select(week =>
            {
                var start = firstWeekStart.AddDays(week * 7);
                var end = start.AddDays(7);
                var count = subscribers.Count(u =>
                {
                    var joined = DateOnly.FromDateTime(u.CreatedAtUtc);
                    return joined >= start && joined < end;
                });
                return new WeeklyCount(start, count);
            })
            .ToList();

        return new AdminOverview(
            subscribers.Count,
            subscribers.Count(u => u.CreatedAtUtc >= now.AddDays(-7)),
            subscribers.Count(u => u.CreatedAtUtc >= now.AddDays(-30)),
            current.Count(u => u.IsActive && ActiveRecently(u.Id)),
            current.Count(u => !u.IsActive),
            subscribers.Count(u => u.DeletedAtUtc is not null),
            Average(metrics.Values.Select(m => m.CalorieAdherencePercent)),
            Average(metrics.Values.Select(m => m.ProteinAdherencePercent)),
            Average(metrics.Values.Select(m => m.WaterAdherencePercent)),
            Average(metrics.Values.Select(m => (double?)m.ConsistencyPercent)),
            veterans.Count == 0 ? null : Math.Round(100.0 * veterans.Count(u => ActiveRecently(u.Id)) / veterans.Count, 1),
            weekly);
    }

    /// <summary>Latest of: last login, or the last meal, water, weight or workout the user logged.</summary>
    private async Task<Dictionary<string, DateTime?>> LastActivityAsync(List<string> ids, CancellationToken cancellationToken)
    {
        var logins = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, Last = u.LastLoginAtUtc }).ToListAsync(cancellationToken);
        var meals = await db.MealEntries.AsNoTracking().Where(m => ids.Contains(m.UserId))
            .GroupBy(m => m.UserId).Select(g => new { Id = g.Key, Last = (DateTime?)g.Max(m => m.CreatedAtUtc) }).ToListAsync(cancellationToken);
        var water = await db.WaterEntries.AsNoTracking().Where(w => ids.Contains(w.UserId))
            .GroupBy(w => w.UserId).Select(g => new { Id = g.Key, Last = (DateTime?)g.Max(w => w.CreatedAtUtc) }).ToListAsync(cancellationToken);
        var weights = await db.WeightEntries.AsNoTracking().Where(w => ids.Contains(w.UserId))
            .GroupBy(w => w.UserId).Select(g => new { Id = g.Key, Last = (DateTime?)g.Max(w => w.CreatedAtUtc) }).ToListAsync(cancellationToken);
        var workouts = await db.WorkoutSessions.AsNoTracking().Where(s => ids.Contains(s.UserId))
            .GroupBy(s => s.UserId).Select(g => new { Id = g.Key, Last = (DateTime?)g.Max(s => s.StartedAtUtc) }).ToListAsync(cancellationToken);

        return logins.Concat(meals).Concat(water).Concat(weights).Concat(workouts)
            .Where(x => x.Last.HasValue)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Last));
    }
}
