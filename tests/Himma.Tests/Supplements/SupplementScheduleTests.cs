using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Supplements;

namespace Himma.Tests.Supplements;

public sealed class SupplementScheduleTests
{
    private static readonly DateOnly Saturday = new(2026, 10, 24);

    private static Supplement Twice(int daysMask = SupplementSchedule.EveryDay) =>
        new() { Times = "08:00,21:00", DaysMask = daysMask };

    [Fact]
    public void The_week_starts_on_saturday()
    {
        Assert.Equal(DayKey.Sat, SupplementSchedule.DayKeyOf(Saturday));
        Assert.Equal(DayKey.Sun, SupplementSchedule.DayKeyOf(Saturday.AddDays(1)));
        Assert.Equal(DayKey.Fri, SupplementSchedule.DayKeyOf(Saturday.AddDays(6)));
    }

    [Fact]
    public void Times_are_stored_sorted_without_duplicates()
    {
        var times = new[] { new TimeOnly(21, 0), new TimeOnly(8, 30), new TimeOnly(21, 0) };

        Assert.Equal("08:30,21:00", SupplementSchedule.FormatTimes(times));
    }

    [Fact]
    public void Days_round_trip_through_the_mask()
    {
        var mask = SupplementSchedule.ToMask([DayKey.Sat, DayKey.Mon, DayKey.Wed]);

        Assert.Equal([DayKey.Sat, DayKey.Mon, DayKey.Wed], SupplementSchedule.FromMask(mask));
    }

    [Fact]
    public void Doses_follow_the_times_and_the_days()
    {
        var weekdaysOnly = Twice(SupplementSchedule.ToMask([DayKey.Sun, DayKey.Mon, DayKey.Tue, DayKey.Wed, DayKey.Thu]));
        var noFixedTime = new Supplement { Times = "", DaysMask = SupplementSchedule.EveryDay };

        Assert.Equal(2, SupplementSchedule.DosesOn(Twice(), Saturday));
        Assert.Equal(0, SupplementSchedule.DosesOn(weekdaysOnly, Saturday));
        Assert.Equal(2, SupplementSchedule.DosesOn(weekdaysOnly, Saturday.AddDays(1)));
        Assert.Equal(1, SupplementSchedule.DosesOn(noFixedTime, Saturday));
    }

    [Fact]
    public void Streak_counts_complete_days_and_an_unfinished_today_does_not_break_it()
    {
        var today = Saturday.AddDays(3);
        var taken = new Dictionary<DateOnly, int>
        {
            [today] = 1,                 // one of two doses so far
            [today.AddDays(-1)] = 2,
            [today.AddDays(-2)] = 2,
            [today.AddDays(-3)] = 1,     // missed a dose: the streak stops here
            [today.AddDays(-4)] = 2,
        };

        Assert.Equal(2, SupplementSchedule.Streak(Twice(), taken, today));

        taken[today] = 2;
        Assert.Equal(3, SupplementSchedule.Streak(Twice(), taken, today));
    }

    [Fact]
    public void Days_off_the_schedule_do_not_break_the_streak()
    {
        var onlySaturdayAndMonday = new Supplement { Times = "09:00", DaysMask = SupplementSchedule.ToMask([DayKey.Sat, DayKey.Mon]) };
        var monday = Saturday.AddDays(2);
        var taken = new Dictionary<DateOnly, int> { [monday] = 1, [Saturday] = 1 };

        Assert.Equal(2, SupplementSchedule.Streak(onlySaturdayAndMonday, taken, monday));
    }
}
