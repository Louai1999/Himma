using Himma.Api.Domain.Entities;

namespace Himma.Api.Domain.Nutrition;

public sealed record DailyTargets(
    int Bmr,
    int Tdee,
    int TargetCalories,
    int ProteinTargetG,
    int CarbsTargetG,
    int FatTargetG,
    int WaterTargetMl,
    IReadOnlyList<string> Warnings);

public static class TargetWarnings
{
    /// <summary>The goal formula went below the safe minimum, so the minimum was used instead.</summary>
    public const string CalorieFloorApplied = "calorieFloorApplied";
}

/// <summary>
/// Daily targets. This is the only place they are calculated; the frontend only displays them.
/// </summary>
public static class TargetCalculator
{
    public const int FemaleCalorieFloor = 1200;
    public const int MaleCalorieFloor = 1500;

    /// <summary>Fat gives 25% of the calories, but never less than 0.6 g per kg (hormones need it).</summary>
    public const double FatShareOfCalories = 0.25;
    public const double MinFatPerKg = 0.6;

    public static DailyTargets Calculate(UserProfile profile) =>
        Calculate(profile.Gender, profile.Age, profile.HeightCm, profile.WeightKg, profile.ActivityLevel, profile.Goal);

    public static DailyTargets Calculate(Gender gender, int age, double heightCm, double weightKg, ActivityLevel activity, Goal goal)
    {
        // Mifflin-St Jeor
        var bmr = 10 * weightKg + 6.25 * heightCm - 5 * age + (gender == Gender.Male ? 5 : -161);
        var tdee = bmr * ActivityMultiplier(activity);

        var calories = goal switch
        {
            Goal.Lose => tdee - 500,
            Goal.Gain => tdee + 400,
            _ => tdee,
        };

        var warnings = new List<string>();
        var floor = gender == Gender.Male ? MaleCalorieFloor : FemaleCalorieFloor;
        if (calories < floor)
        {
            calories = floor;
            warnings.Add(TargetWarnings.CalorieFloorApplied);
        }

        var proteinPerKg = goal switch
        {
            Goal.Lose => 2.0,
            Goal.Gain => 1.8,
            _ => 1.6,
        };

        var proteinG = weightKg * proteinPerKg;
        var fatG = Math.Max(calories * FatShareOfCalories / 9, weightKg * MinFatPerKg);
        // Carbs fill the rest of the calories.
        var carbsG = Math.Max(0, (calories - proteinG * 4 - fatG * 9) / 4);

        var waterMl = (int)Math.Round(weightKg * 33 / 50, MidpointRounding.AwayFromZero) * 50 + WaterBonusMl(activity);

        return new DailyTargets(
            Round(bmr),
            Round(tdee),
            Round(calories),
            Round(proteinG),
            Round(carbsG),
            Round(fatG),
            waterMl,
            warnings);
    }

    public static double ActivityMultiplier(ActivityLevel activity) => activity switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Light => 1.375,
        ActivityLevel.Moderate => 1.55,
        ActivityLevel.Active => 1.725,
        ActivityLevel.VeryActive => 1.9,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>Extra water for active people. Only the highest matching tier applies.</summary>
    private static int WaterBonusMl(ActivityLevel activity) => activity switch
    {
        ActivityLevel.VeryActive => 1000,
        ActivityLevel.Active => 750,
        ActivityLevel.Moderate => 500,
        _ => 0,
    };

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
