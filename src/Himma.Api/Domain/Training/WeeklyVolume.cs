namespace Himma.Api.Domain.Training;

public sealed record MuscleVolume(MuscleGroup Muscle, int Sets, double VolumeKg, string Status);

public sealed record WeekVolume(DateOnly WeekStart, IReadOnlyList<MuscleVolume> Muscles);

public static class VolumeStatus
{
    public const string Low = "low";
    public const string Good = "good";
    public const string High = "high";
}

/// <summary>
/// Hard sets per muscle per week. About 10 to 20 sets a week per muscle is the common range for growth;
/// fewer leaves gains on the table, many more is hard to recover from. Cardio is not counted.
/// </summary>
public static class WeeklyVolume
{
    public const int TargetMinSets = 10;
    public const int TargetMaxSets = 20;

    /// <summary>Weeks start on Saturday.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 1) % 7));

    public static string Status(int sets) =>
        sets < TargetMinSets ? VolumeStatus.Low : sets > TargetMaxSets ? VolumeStatus.High : VolumeStatus.Good;

    /// <summary>Weeks oldest first; every week lists the muscles trained that week, most sets first.</summary>
    public static IReadOnlyList<WeekVolume> Build(
        IEnumerable<(DateOnly Date, MuscleGroup Muscle, int Reps, double? WeightKg)> completedSets, DateOnly today, int weeks)
    {
        var currentWeek = WeekStart(today);
        var firstWeek = currentWeek.AddDays(-7 * (weeks - 1));
        var byWeek = completedSets
            .Where(s => s.Muscle != MuscleGroup.Cardio && s.Date >= firstWeek && s.Date <= today)
            .GroupBy(s => WeekStart(s.Date))
            .ToDictionary(g => g.Key, g => g
                .GroupBy(s => s.Muscle)
                .Select(m => new MuscleVolume(m.Key, m.Count(), Math.Round(m.Sum(s => (s.WeightKg ?? 0) * s.Reps), 1), Status(m.Count())))
                .OrderByDescending(m => m.Sets)
                .ToList());

        return Enumerable.Range(0, weeks)
            .Select(i => firstWeek.AddDays(7 * i))
            .Select(start => new WeekVolume(start, byWeek.GetValueOrDefault(start) ?? []))
            .ToList();
    }
}
