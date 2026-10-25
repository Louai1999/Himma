using Himma.Api.Domain.Nutrition;

namespace Himma.Api.Domain.Strength;

public sealed record OneRepMaxEstimate(double OneRepMaxKg, bool Reliable, IReadOnlyList<LoadRow> Table);

/// <summary>A row of the load chart: this share of the 1RM is usually good for about this many reps.</summary>
public sealed record LoadRow(int Percent, double WeightKg, int Reps);

/// <summary>
/// Estimated one-rep max (Epley): weight × (1 + reps / 30). Lets a lifter know their strength without
/// testing a true max. Estimates above 12 reps are much less accurate, so they are flagged.
/// </summary>
public static class OneRepMax
{
    public const int MaxReps = 30;
    public const int ReliableUpToReps = 12;

    private static readonly (int Percent, int Reps)[] Chart =
        [(100, 1), (95, 2), (90, 4), (85, 6), (80, 8), (75, 10), (70, 12), (65, 15), (60, 20)];

    public static double Estimate(double weightKg, int reps) =>
        reps <= 1 ? NutritionMath.Round1(weightKg) : NutritionMath.Round1(weightKg * (1 + reps / 30.0));

    public static OneRepMaxEstimate Calculate(double weightKg, int reps)
    {
        var max = Estimate(weightKg, reps);
        return new OneRepMaxEstimate(
            max,
            reps <= ReliableUpToReps,
            Chart.Select(c => new LoadRow(c.Percent, RoundToPlate(max * c.Percent / 100), c.Reps)).ToList());
    }

    /// <summary>Gym weights go up in 0.5 kg steps at best.</summary>
    public static double RoundToPlate(double kg) => Math.Round(kg * 2, MidpointRounding.AwayFromZero) / 2;
}
