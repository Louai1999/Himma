using Himma.Api.Domain.Nutrition;

namespace Himma.Api.Domain.Walking;

public sealed record WalkingPlan(double Calories, double DistanceKm, int Steps, int Minutes);

/// <summary>
/// Walking estimates from height and weight. Calories ≈ 0.7 × body weight (kg) per km at an ordinary
/// pace (about 5 km/h); the real number depends on speed, slopes and the person, so it is an estimate.
/// </summary>
public static class WalkingCalculator
{
    public const double KcalPerKgPerKm = 0.7;
    public const double PaceKmPerHour = 5;

    /// <summary>Step length ≈ 41.5% of height for men and 41.3% for women.</summary>
    public static double StepLengthM(Gender gender, double heightCm) =>
        heightCm * (gender == Gender.Male ? 0.415 : 0.413) / 100;

    public static double DistanceKm(int steps, double stepLengthM) => NutritionMath.Round1(steps * stepLengthM / 1000);

    public static double Calories(double weightKg, double distanceKm) => Math.Round(KcalPerKgPerKm * weightKg * distanceKm);

    public static double CaloriesForSteps(Gender gender, double heightCm, double weightKg, int steps) =>
        Calories(weightKg, steps * StepLengthM(gender, heightCm) / 1000);

    /// <summary>How far, how many steps and how long to walk to burn the given calories.</summary>
    public static WalkingPlan Plan(Gender gender, double heightCm, double weightKg, double calories)
    {
        var km = calories / (KcalPerKgPerKm * weightKg);
        return new WalkingPlan(
            calories,
            NutritionMath.Round1(km),
            (int)Math.Round(km * 1000 / StepLengthM(gender, heightCm)),
            (int)Math.Round(km / PaceKmPerHour * 60));
    }
}
