using Himma.Api.Domain.Nutrition;

namespace Himma.Tests.Nutrition;

public sealed class AdherenceCalculatorTests
{
    private static readonly AdherenceThresholds Defaults = new(10, 90, 80);
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly DailyTargets Targets = new(1700, 2000, 2000, 150, 200, 60, 2500, []);

    private static DayTotals Day(int daysAgo, double calories = 2000, double protein = 150, int water = 2500, int meals = 3) =>
        new(Today.AddDays(-daysAgo), calories, protein, water, meals);

    [Theory]
    [InlineData(1800, true)]    // exactly -10%
    [InlineData(2200, true)]    // exactly +10%
    [InlineData(1799, false)]
    [InlineData(2201, false)]
    public void Calories_within_ten_percent_count(double calories, bool expected)
    {
        Assert.Equal(expected, AdherenceCalculator.IsCalorieAdherent(Day(0, calories), 2000, Defaults));
    }

    [Fact]
    public void A_day_without_meals_is_never_calorie_adherent()
    {
        Assert.False(AdherenceCalculator.IsCalorieAdherent(Day(0, calories: 0, meals: 0), 0, Defaults));
        Assert.False(AdherenceCalculator.IsCalorieAdherent(Day(0, calories: 2000, meals: 0), 2000, Defaults));
    }

    [Theory]
    [InlineData(135, true)]     // exactly 90% of 150
    [InlineData(134.9, false)]
    public void Protein_needs_ninety_percent(double protein, bool expected)
    {
        Assert.Equal(expected, AdherenceCalculator.IsProteinAdherent(Day(0, protein: protein), 150, Defaults));
    }

    [Theory]
    [InlineData(2000, true)]    // exactly 80% of 2500
    [InlineData(1999, false)]
    public void Water_needs_eighty_percent(int water, bool expected)
    {
        Assert.Equal(expected, AdherenceCalculator.IsWaterAdherent(Day(0, water: water), 2500, Defaults));
    }

    [Fact]
    public void Streak_counts_back_from_today()
    {
        var dates = new[] { Today, Today.AddDays(-1), Today.AddDays(-2), Today.AddDays(-4) };

        Assert.Equal(3, AdherenceCalculator.Streak(dates, Today));
    }

    [Fact]
    public void Streak_can_end_yesterday_before_today_is_logged()
    {
        var dates = new[] { Today.AddDays(-1), Today.AddDays(-2) };

        Assert.Equal(2, AdherenceCalculator.Streak(dates, Today));
    }

    [Fact]
    public void Streak_is_zero_when_the_last_meal_was_two_days_ago()
    {
        var dates = new[] { Today.AddDays(-2), Today.AddDays(-3) };

        Assert.Equal(0, AdherenceCalculator.Streak(dates, Today));
    }

    [Fact]
    public void Summary_counts_only_days_inside_the_window()
    {
        var days = new[]
        {
            Day(0),                          // on target
            Day(1, calories: 3000),          // too many calories, protein and water fine
            Day(2, protein: 50, water: 500), // calories fine only
            Day(7),                          // outside a 7-day window
        };

        var s = AdherenceCalculator.Summarize(days, Today, 7, Targets, Defaults);

        Assert.Equal(3, s.LoggedDays);
        Assert.Equal(42.9, s.ConsistencyPercent);        // 3 / 7
        Assert.Equal(28.6, s.CalorieAdherencePercent);   // 2 / 7
        Assert.Equal(28.6, s.ProteinAdherencePercent);
        Assert.Equal(28.6, s.WaterAdherencePercent);
    }

    [Fact]
    public void Without_targets_only_consistency_is_calculated()
    {
        var s = AdherenceCalculator.Summarize([Day(0)], Today, 30, targets: null, Defaults);

        Assert.Equal(3.3, s.ConsistencyPercent);
        Assert.Null(s.CalorieAdherencePercent);
        Assert.Null(s.ProteinAdherencePercent);
        Assert.Null(s.WaterAdherencePercent);
    }
}
