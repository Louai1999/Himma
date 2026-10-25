using Himma.Api.Domain;
using Himma.Api.Domain.Body;

namespace Himma.Tests.Nutrition;

public sealed class BodyCompositionCalculatorTests
{
    [Fact]
    public void Navy_method_for_men_uses_neck_and_waist()
    {
        // 495 / (1.0324 - 0.19077*log10(90-38) + 0.15456*log10(178)) - 450 = 20.15
        Assert.Equal(20.1, BodyCompositionCalculator.NavyBodyFat(Gender.Male, 178, 38, 90, hipCm: null));
    }

    [Fact]
    public void Navy_method_for_women_also_needs_the_hips()
    {
        // 495 / (1.29579 - 0.35004*log10(75+100-32) + 0.22100*log10(165)) - 450 = 29.93
        Assert.Equal(29.9, BodyCompositionCalculator.NavyBodyFat(Gender.Female, 165, 32, 75, 100));
        Assert.Null(BodyCompositionCalculator.NavyBodyFat(Gender.Female, 165, 32, 75, hipCm: null));
    }

    [Theory]
    [InlineData(null, 90.0)]
    [InlineData(38.0, null)]
    [InlineData(45.0, 44.0)]
    public void Missing_or_impossible_measurements_give_no_estimate(double? neck, double? waist)
    {
        Assert.Null(BodyCompositionCalculator.NavyBodyFat(Gender.Male, 178, neck, waist, hipCm: null));
    }

    [Fact]
    public void Composition_splits_weight_into_fat_and_lean_mass()
    {
        var c = BodyCompositionCalculator.Calculate(Gender.Male, 178, 85, 38, 90, hipCm: null, deviceBodyFatPercent: null)!;

        Assert.Equal(BodyFatSource.Navy, c.Source);
        Assert.Equal(20.1, c.BodyFatPercent);
        Assert.Equal(17.1, c.FatMassKg);         // 85 * 20.1%
        Assert.Equal(67.9, c.LeanMassKg);        // 85 - 17.085
        Assert.Equal(21.4, c.Ffmi);              // 67.915 / 1.78^2
        Assert.Equal(21.6, c.NormalizedFfmi);    // + 6.1 * (1.8 - 1.78)
        Assert.Equal("average", c.BodyFatCategory);
        Assert.Equal("aboveAverage", c.FfmiLevel);
    }

    [Fact]
    public void A_device_reading_is_used_instead_of_the_tape_estimate()
    {
        var c = BodyCompositionCalculator.Calculate(Gender.Male, 178, 80, 38, 90, hipCm: null, deviceBodyFatPercent: 15)!;

        Assert.Equal(BodyFatSource.Device, c.Source);
        Assert.Equal(15, c.BodyFatPercent);
        Assert.Equal(68, c.LeanMassKg);
    }

    [Fact]
    public void No_tape_measurements_and_no_device_reading_gives_nothing()
    {
        Assert.Null(BodyCompositionCalculator.Calculate(Gender.Male, 178, 80, null, null, null, null));
    }

    [Fact]
    public void Projection_keeps_lean_mass_and_loses_at_half_to_one_percent_a_week()
    {
        var current = BodyCompositionCalculator.Calculate(Gender.Male, 178, 85, 38, 90, null, null)!;

        var p = BodyCompositionCalculator.Project(Gender.Male, 85, current, 12);

        Assert.Equal(77.2, p.TargetWeightKg);    // 67.9 / 0.88
        Assert.Equal(7.8, p.FatToLoseKg);
        Assert.False(p.Reached);
        Assert.Equal(10, p.WeeksAtFastestPace);  // 7.84 / 0.85 kg a week
        Assert.Equal(19, p.WeeksAtSlowestPace);  // 7.84 / 0.425 kg a week
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void A_goal_above_the_current_body_fat_is_already_reached()
    {
        var current = BodyCompositionCalculator.Calculate(Gender.Male, 178, 80, null, null, null, deviceBodyFatPercent: 12)!;

        var p = BodyCompositionCalculator.Project(Gender.Male, 80, current, 15);

        Assert.True(p.Reached);
        Assert.Equal(0, p.FatToLoseKg);
        Assert.Null(p.WeeksAtFastestPace);
        Assert.Null(p.WeeksAtSlowestPace);
    }

    [Theory]
    [InlineData("male", 7, true)]
    [InlineData("male", 8, false)]
    [InlineData("female", 14, true)]
    [InlineData("female", 15, false)]
    public void Very_low_goals_get_a_warning(string gender, double target, bool warned)
    {
        var g = gender == "male" ? Gender.Male : Gender.Female;
        var current = BodyCompositionCalculator.Calculate(g, 170, 70, null, null, null, deviceBodyFatPercent: 30)!;

        var p = BodyCompositionCalculator.Project(g, 70, current, target);

        Assert.Equal(warned, p.Warnings.Contains(BodyWarnings.VeryLowTarget));
    }

    [Theory]
    [InlineData("male", 5, "essential")]
    [InlineData("male", 10, "athletic")]
    [InlineData("male", 16, "fitness")]
    [InlineData("male", 20, "average")]
    [InlineData("male", 25, "high")]
    [InlineData("female", 18, "athletic")]
    [InlineData("female", 28, "average")]
    [InlineData("female", 32, "high")]
    public void Body_fat_categories_follow_the_ace_ranges(string gender, double percent, string expected)
    {
        var g = gender == "male" ? Gender.Male : Gender.Female;
        Assert.Equal(expected, BodyCompositionCalculator.BodyFatCategory(g, percent));
    }
}
