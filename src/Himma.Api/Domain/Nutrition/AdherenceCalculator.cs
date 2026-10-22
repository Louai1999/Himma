namespace Himma.Api.Domain.Nutrition;

/// <summary>What one user ate and drank on one local date.</summary>
public sealed record DayTotals(DateOnly Date, double Calories, double ProteinG, int WaterMl, int MealCount)
{
    public bool IsLogged => MealCount > 0;
}

public sealed record AdherenceThresholds(double CalorieTolerancePercent, double ProteinMinPercent, double WaterMinPercent);

public sealed record AdherenceSummary(
    int WindowDays,
    int LoggedDays,
    double ConsistencyPercent,
    double? CalorieAdherencePercent,
    double? ProteinAdherencePercent,
    double? WaterAdherencePercent);

public static class AdherenceCalculator
{
    // Guards against floating-point noise exactly on a boundary (e.g. 2200 kcal vs a 2000 target ±10%).
    private const double Epsilon = 1e-6;

    public static bool IsCalorieAdherent(DayTotals day, int targetCalories, AdherenceThresholds thresholds) =>
        day.IsLogged
        && targetCalories > 0
        && Math.Abs(day.Calories - targetCalories) <= targetCalories * thresholds.CalorieTolerancePercent / 100 + Epsilon;

    public static bool IsProteinAdherent(DayTotals day, int proteinTargetG, AdherenceThresholds thresholds) =>
        proteinTargetG > 0 && day.ProteinG >= proteinTargetG * thresholds.ProteinMinPercent / 100 - Epsilon;

    public static bool IsWaterAdherent(DayTotals day, int waterTargetMl, AdherenceThresholds thresholds) =>
        waterTargetMl > 0 && day.WaterMl >= waterTargetMl * thresholds.WaterMinPercent / 100 - Epsilon;

    /// <summary>
    /// Adherence over the window ending at <paramref name="windowEnd"/> (inclusive). Days without data count
    /// as non-adherent. Without targets (no profile yet) only consistency can be calculated.
    /// </summary>
    public static AdherenceSummary Summarize(
        IEnumerable<DayTotals> days,
        DateOnly windowEnd,
        int windowDays,
        DailyTargets? targets,
        AdherenceThresholds thresholds)
    {
        var windowStart = windowEnd.AddDays(-(windowDays - 1));
        var inWindow = days.Where(d => d.Date >= windowStart && d.Date <= windowEnd).ToList();
        var loggedDays = inWindow.Count(d => d.IsLogged);

        double Percent(int count) => Math.Round(100.0 * count / windowDays, 1, MidpointRounding.AwayFromZero);

        return new AdherenceSummary(
            windowDays,
            loggedDays,
            Percent(loggedDays),
            targets is null ? null : Percent(inWindow.Count(d => IsCalorieAdherent(d, targets.TargetCalories, thresholds))),
            targets is null ? null : Percent(inWindow.Count(d => IsProteinAdherent(d, targets.ProteinTargetG, thresholds))),
            targets is null ? null : Percent(inWindow.Count(d => IsWaterAdherent(d, targets.WaterTargetMl, thresholds))));
    }

    /// <summary>
    /// Consecutive days with at least one meal, ending today or yesterday
    /// (so the streak does not reset in the morning before breakfast is logged).
    /// </summary>
    public static int Streak(IEnumerable<DateOnly> mealDates, DateOnly today)
    {
        var dates = mealDates.ToHashSet();
        var cursor = dates.Contains(today) ? today : today.AddDays(-1);

        var streak = 0;
        while (dates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }
}
