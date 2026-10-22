using Himma.Api.Data;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Features.History;

/// <summary>Per-day totals and adherence for one user. Also reused later by the admin dashboard.</summary>
public sealed class HistoryService(AppDbContext db, IOptions<AdherenceOptions> adherenceOptions)
{
    /// <summary>How far back a streak is followed.</summary>
    private const int MaxStreakLookbackDays = 400;

    public AdherenceThresholds Thresholds
    {
        get
        {
            var o = adherenceOptions.Value;
            return new AdherenceThresholds(o.CalorieTolerancePercent, o.ProteinMinPercent, o.WaterMinPercent);
        }
    }

    /// <summary>Only days that have at least one meal or water entry, oldest first.</summary>
    public async Task<List<DayTotals>> GetDayTotalsAsync(string userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => m.UserId == userId && m.Date >= from && m.Date <= to)
            .GroupBy(m => m.Date)
            .Select(g => new { Date = g.Key, Calories = g.Sum(m => m.Calories), Protein = g.Sum(m => m.ProteinG), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var water = await db.WaterEntries.AsNoTracking()
            .Where(w => w.UserId == userId && w.Date >= from && w.Date <= to)
            .GroupBy(w => w.Date)
            .Select(g => new { Date = g.Key, Ml = g.Sum(w => w.Ml) })
            .ToListAsync(cancellationToken);

        var mealsByDate = meals.ToDictionary(m => m.Date);
        var waterByDate = water.ToDictionary(w => w.Date, w => w.Ml);

        return mealsByDate.Keys.Union(waterByDate.Keys)
            .Order()
            .Select(date =>
            {
                mealsByDate.TryGetValue(date, out var m);
                return new DayTotals(date, m?.Calories ?? 0, m?.Protein ?? 0, waterByDate.GetValueOrDefault(date), m?.Count ?? 0);
            })
            .ToList();
    }

    public async Task<int> GetStreakAsync(string userId, DateOnly today, CancellationToken cancellationToken)
    {
        var since = today.AddDays(-MaxStreakLookbackDays);
        var mealDates = await db.MealEntries.AsNoTracking()
            .Where(m => m.UserId == userId && m.Date >= since && m.Date <= today)
            .Select(m => m.Date)
            .Distinct()
            .ToListAsync(cancellationToken);

        return AdherenceCalculator.Streak(mealDates, today);
    }

    /// <summary>
    /// Summary of the <paramref name="days"/> days ending at <paramref name="today"/>.
    /// Adherence uses the user's current targets for every day, because targets are not stored per day.
    /// </summary>
    public async Task<HistorySummaryResponse> GetSummaryAsync(string userId, DateOnly today, int days, CancellationToken cancellationToken)
    {
        var from = today.AddDays(-(days - 1));

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var targets = profile is null ? null : TargetCalculator.Calculate(profile);

        var totals = await GetDayTotalsAsync(userId, from, today, cancellationToken);
        var totalsByDate = totals.ToDictionary(t => t.Date);
        var thresholds = Thresholds;

        // Every day of the window, so the chart has one bar per day even when nothing was logged.
        var daily = Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => totalsByDate.GetValueOrDefault(date) ?? new DayTotals(date, 0, 0, 0, 0))
            .Select(d => new SummaryDayDto(
                d.Date,
                NutritionMath.Round1(d.Calories),
                NutritionMath.Round1(d.ProteinG),
                d.WaterMl,
                d.MealCount,
                targets is null ? null : AdherenceCalculator.IsCalorieAdherent(d, targets.TargetCalories, thresholds),
                targets is null ? null : AdherenceCalculator.IsProteinAdherent(d, targets.ProteinTargetG, thresholds),
                targets is null ? null : AdherenceCalculator.IsWaterAdherent(d, targets.WaterTargetMl, thresholds)))
            .ToList();

        var weights = await db.WeightEntries.AsNoTracking()
            .Where(w => w.UserId == userId && w.Date >= from && w.Date <= today)
            .OrderBy(w => w.Date)
            .Select(w => new WeightPointDto(w.Date, w.WeightKg))
            .ToListAsync(cancellationToken);

        return new HistorySummaryResponse(
            days,
            from,
            today,
            targets,
            await GetStreakAsync(userId, today, cancellationToken),
            AdherenceCalculator.Summarize(totals, today, days, targets, thresholds),
            daily,
            weights);
    }
}
