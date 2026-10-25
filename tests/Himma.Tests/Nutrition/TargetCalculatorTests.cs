using Himma.Api.Domain;
using Himma.Api.Domain.Nutrition;

namespace Himma.Tests.Nutrition;

public sealed class TargetCalculatorTests
{
    [Fact]
    public void Male_moderate_losing_weight()
    {
        // BMR = 10*80 + 6.25*180 - 5*30 + 5 = 1780; TDEE = 1780 * 1.55 = 2759
        var t = TargetCalculator.Calculate(Gender.Male, 30, 180, 80, ActivityLevel.Moderate, Goal.Lose);

        Assert.Equal(1780, t.Bmr);
        Assert.Equal(2759, t.Tdee);
        Assert.Equal(2259, t.TargetCalories);
        Assert.Equal(160, t.ProteinTargetG);         // 80 * 2.0
        Assert.Equal(2650 + 500, t.WaterTargetMl);   // round(80*33/50)=53 -> 2650, +500 for moderate
        Assert.Empty(t.Warnings);
    }

    [Fact]
    public void Female_sedentary_maintaining()
    {
        // BMR = 600 + 1031.25 - 125 - 161 = 1345.25; TDEE = 1614.3
        var t = TargetCalculator.Calculate(Gender.Female, 25, 165, 60, ActivityLevel.Sedentary, Goal.Maintain);

        Assert.Equal(1345, t.Bmr);
        Assert.Equal(1614, t.Tdee);
        Assert.Equal(1614, t.TargetCalories);
        Assert.Equal(96, t.ProteinTargetG);          // 60 * 1.6
        Assert.Equal(2000, t.WaterTargetMl);         // round(39.6)=40 -> 2000, no bonus
    }

    [Fact]
    public void Gaining_adds_400_and_uses_1_8_g_protein_per_kg()
    {
        var maintain = TargetCalculator.Calculate(Gender.Male, 22, 175, 70, ActivityLevel.Light, Goal.Maintain);
        var gain = TargetCalculator.Calculate(Gender.Male, 22, 175, 70, ActivityLevel.Light, Goal.Gain);

        Assert.Equal(maintain.TargetCalories + 400, gain.TargetCalories);
        Assert.Equal(126, gain.ProteinTargetG);      // 70 * 1.8
    }

    [Fact]
    public void Fat_is_a_quarter_of_calories_and_carbs_fill_the_rest()
    {
        var t = TargetCalculator.Calculate(Gender.Male, 30, 180, 80, ActivityLevel.Moderate, Goal.Lose);

        Assert.Equal(63, t.FatTargetG);              // 2259 * 25% / 9 = 62.75
        Assert.Equal(264, t.CarbsTargetG);           // (2259 - 160*4 - 62.75*9) / 4 = 263.6
    }

    [Fact]
    public void Fat_never_goes_below_0_6_g_per_kg()
    {
        // Floor target 1500 kcal for 120 kg: 25% is 41.7 g, but 0.6 * 120 = 72 g.
        var t = TargetCalculator.Calculate(Gender.Male, 70, 155, 120, ActivityLevel.Sedentary, Goal.Lose);

        Assert.Equal(72, t.FatTargetG);
        Assert.True(t.CarbsTargetG >= 0);
    }

    [Fact]
    public void Female_target_is_clamped_to_1200_with_a_warning()
    {
        // TDEE = (450 + 937.5 - 300 - 161) * 1.2 = 1111.8; minus 500 = 611.8
        var t = TargetCalculator.Calculate(Gender.Female, 60, 150, 45, ActivityLevel.Sedentary, Goal.Lose);

        Assert.Equal(TargetCalculator.FemaleCalorieFloor, t.TargetCalories);
        Assert.Contains(TargetWarnings.CalorieFloorApplied, t.Warnings);
    }

    [Fact]
    public void Male_target_is_clamped_to_1500_with_a_warning()
    {
        var t = TargetCalculator.Calculate(Gender.Male, 70, 155, 50, ActivityLevel.Sedentary, Goal.Lose);

        Assert.Equal(TargetCalculator.MaleCalorieFloor, t.TargetCalories);
        Assert.Contains(TargetWarnings.CalorieFloorApplied, t.Warnings);
    }

    [Fact]
    public void Target_above_the_floor_has_no_warning()
    {
        // Female floor is 1200; this target is about 1614.
        var t = TargetCalculator.Calculate(Gender.Female, 25, 165, 60, ActivityLevel.Sedentary, Goal.Maintain);

        Assert.True(t.TargetCalories > TargetCalculator.FemaleCalorieFloor);
        Assert.Empty(t.Warnings);
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 1.2, 0)]
    [InlineData(ActivityLevel.Light, 1.375, 0)]
    [InlineData(ActivityLevel.Moderate, 1.55, 500)]
    [InlineData(ActivityLevel.Active, 1.725, 750)]
    [InlineData(ActivityLevel.VeryActive, 1.9, 1000)]
    public void Activity_sets_multiplier_and_water_bonus(ActivityLevel activity, double multiplier, int waterBonus)
    {
        var t = TargetCalculator.Calculate(Gender.Male, 30, 180, 80, activity, Goal.Maintain);

        Assert.Equal(multiplier, TargetCalculator.ActivityMultiplier(activity));
        Assert.Equal((int)Math.Round(1780 * multiplier, MidpointRounding.AwayFromZero), t.Tdee);
        Assert.Equal(2650 + waterBonus, t.WaterTargetMl);
    }

    [Theory]
    [InlineData(75.0, 2500)]   // 49.5 rounds up to 50 -> 2500
    [InlineData(74.0, 2450)]   // 48.84 -> 49 -> 2450
    public void Water_rounds_to_the_nearest_50_ml(double weightKg, int expectedMl)
    {
        var t = TargetCalculator.Calculate(Gender.Female, 30, 165, weightKg, ActivityLevel.Sedentary, Goal.Maintain);

        Assert.Equal(expectedMl, t.WaterTargetMl);
    }
}
