using Himma.Api.Domain.Nutrition;

namespace Himma.Api.Domain.Body;

/// <summary>Where the body fat number came from.</summary>
public enum BodyFatSource { Navy, Device }

public sealed record BodyComposition(
    double BodyFatPercent,
    BodyFatSource Source,
    double FatMassKg,
    double LeanMassKg,
    double Ffmi,
    double NormalizedFfmi,
    string BodyFatCategory,
    string FfmiLevel);

public sealed record BodyFatProjection(
    double TargetBodyFatPercent,
    double TargetWeightKg,
    double FatToLoseKg,
    bool Reached,
    int? WeeksAtFastestPace,
    int? WeeksAtSlowestPace,
    IReadOnlyList<string> Warnings);

public static class BodyWarnings
{
    /// <summary>The goal is below what most people can keep long term.</summary>
    public const string VeryLowTarget = "veryLowTarget";
}

/// <summary>
/// Body composition estimates. All results are estimates for tracking change over time, not a diagnosis.
/// This is the only place they are calculated; the frontend only displays them.
/// </summary>
public static class BodyCompositionCalculator
{
    /// <summary>Safe pace when cutting: 0.5% to 1% of body weight per week.</summary>
    public const double FastestWeeklyLossShare = 0.01;
    public const double SlowestWeeklyLossShare = 0.005;

    /// <summary>
    /// U.S. Navy circumference method (centimetres). Men: neck and waist; women also need the hips.
    /// Returns null when the measurements are missing or impossible (for example a neck wider than the waist).
    /// </summary>
    public static double? NavyBodyFat(Gender gender, double heightCm, double? neckCm, double? waistCm, double? hipCm)
    {
        if (neckCm is not { } neck || waistCm is not { } waist)
        {
            return null;
        }

        double density;
        if (gender == Gender.Male)
        {
            if (waist - neck <= 0)
            {
                return null;
            }
            density = 1.0324 - 0.19077 * Math.Log10(waist - neck) + 0.15456 * Math.Log10(heightCm);
        }
        else
        {
            if (hipCm is not { } hip || waist + hip - neck <= 0)
            {
                return null;
            }
            density = 1.29579 - 0.35004 * Math.Log10(waist + hip - neck) + 0.22100 * Math.Log10(heightCm);
        }

        var percent = 495 / density - 450;
        return percent is >= 2 and <= 70 ? NutritionMath.Round1(percent) : null;
    }

    /// <summary>A device reading wins over the tape estimate. Null when neither is available.</summary>
    public static BodyComposition? Calculate(
        Gender gender, double heightCm, double weightKg,
        double? neckCm, double? waistCm, double? hipCm, double? deviceBodyFatPercent)
    {
        var source = deviceBodyFatPercent is null ? BodyFatSource.Navy : BodyFatSource.Device;
        var bodyFat = deviceBodyFatPercent ?? NavyBodyFat(gender, heightCm, neckCm, waistCm, hipCm);
        if (bodyFat is not { } percent)
        {
            return null;
        }

        var fatMass = weightKg * percent / 100;
        var leanMass = weightKg - fatMass;
        var heightM = heightCm / 100;
        var ffmi = leanMass / (heightM * heightM);
        // Taller people carry more lean mass naturally; this scales everyone to 1.8 m.
        var normalizedFfmi = ffmi + 6.1 * (1.8 - heightM);

        return new BodyComposition(
            NutritionMath.Round1(percent),
            source,
            NutritionMath.Round1(fatMass),
            NutritionMath.Round1(leanMass),
            NutritionMath.Round1(ffmi),
            NutritionMath.Round1(normalizedFfmi),
            BodyFatCategory(gender, percent),
            FfmiLevel(gender, normalizedFfmi));
    }

    /// <summary>
    /// Weight at the goal body fat if all lean mass is kept, and how long the cut takes at a safe pace.
    /// </summary>
    public static BodyFatProjection Project(Gender gender, double weightKg, BodyComposition current, double targetBodyFatPercent)
    {
        var targetWeight = current.LeanMassKg / (1 - targetBodyFatPercent / 100);
        var fatToLose = Math.Max(0, weightKg - targetWeight);
        var reached = current.BodyFatPercent <= targetBodyFatPercent;

        var warnings = new List<string>();
        if (targetBodyFatPercent < (gender == Gender.Male ? 8 : 15))
        {
            warnings.Add(BodyWarnings.VeryLowTarget);
        }

        return new BodyFatProjection(
            NutritionMath.Round1(targetBodyFatPercent),
            NutritionMath.Round1(targetWeight),
            NutritionMath.Round1(fatToLose),
            reached,
            reached ? null : (int)Math.Ceiling(fatToLose / (weightKg * FastestWeeklyLossShare)),
            reached ? null : (int)Math.Ceiling(fatToLose / (weightKg * SlowestWeeklyLossShare)),
            warnings);
    }

    /// <summary>American Council on Exercise ranges.</summary>
    public static string BodyFatCategory(Gender gender, double percent) => gender == Gender.Male
        ? percent switch
        {
            < 6 => "essential",
            < 14 => "athletic",
            < 18 => "fitness",
            < 25 => "average",
            _ => "high",
        }
        : percent switch
        {
            < 14 => "essential",
            < 21 => "athletic",
            < 25 => "fitness",
            < 32 => "average",
            _ => "high",
        };

    /// <summary>Rough levels for the normalized FFMI; women's ranges are about 3.5 points lower.</summary>
    public static string FfmiLevel(Gender gender, double normalizedFfmi)
    {
        var value = gender == Gender.Male ? normalizedFfmi : normalizedFfmi + 3.5;
        return value switch
        {
            < 18 => "belowAverage",
            < 20 => "average",
            < 22 => "aboveAverage",
            < 23.5 => "advanced",
            < 25 => "elite",
            _ => "exceptional",
        };
    }
}
