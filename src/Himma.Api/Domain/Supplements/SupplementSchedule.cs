using System.Globalization;
using Himma.Api.Domain.Entities;

namespace Himma.Api.Domain.Supplements;

/// <summary>When a supplement is due, how many doses a day, and the "days in a row" streak.</summary>
public static class SupplementSchedule
{
    public const int EveryDay = 127;
    public const int MaxTimesPerDay = 6;
    public const int StreakLookbackDays = 366;

    public static IReadOnlyList<TimeOnly> ParseTimes(string times) =>
        times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => TimeOnly.ParseExact(t, "HH:mm", CultureInfo.InvariantCulture))
            .ToList();

    /// <summary>Sorted, without duplicates, as stored in the database.</summary>
    public static string FormatTimes(IEnumerable<TimeOnly> times) =>
        string.Join(',', times.Distinct().Order().Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)));

    public static int ToMask(IEnumerable<DayKey> days) => days.Aggregate(0, (mask, day) => mask | 1 << (int)day);

    public static IReadOnlyList<DayKey> FromMask(int mask) =>
        Enum.GetValues<DayKey>().Where(day => (mask & 1 << (int)day) != 0).ToList();

    /// <summary>The week starts on Saturday (DayKey.Sat = 0).</summary>
    public static DayKey DayKeyOf(DateOnly date) => (DayKey)(((int)date.DayOfWeek + 1) % 7);

    public static bool IsScheduledOn(Supplement supplement, DateOnly date) =>
        (supplement.DaysMask & 1 << (int)DayKeyOf(date)) != 0;

    /// <summary>One dose per reminder time; a supplement without fixed times counts as one dose a day.</summary>
    public static int DosesOn(Supplement supplement, DateOnly date) =>
        IsScheduledOn(supplement, date) ? Math.Max(1, ParseTimes(supplement.Times).Count) : 0;

    /// <summary>
    /// Scheduled days in a row with every dose taken, counting back from today. Today only counts once it is
    /// complete, and an unfinished today does not break the streak. Days off the schedule are skipped.
    /// </summary>
    public static int Streak(Supplement supplement, IReadOnlyDictionary<DateOnly, int> takenByDate, DateOnly today)
    {
        var streak = 0;
        for (var i = 0; i < StreakLookbackDays; i++)
        {
            var date = today.AddDays(-i);
            var due = DosesOn(supplement, date);
            if (due == 0)
            {
                continue;
            }

            var taken = takenByDate.GetValueOrDefault(date);
            if (taken >= due)
            {
                streak++;
            }
            else if (i > 0)
            {
                break;
            }
        }
        return streak;
    }
}
