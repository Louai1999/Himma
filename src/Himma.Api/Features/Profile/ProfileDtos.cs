using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;

namespace Himma.Api.Features.Profile;

/// <summary>Fields are nullable so a missing value is reported instead of silently becoming 0 or the first enum value.</summary>
public sealed class ProfileRequest
{
    /// <summary>The user's local date; the first save records the weight on this date.</summary>
    public string Date { get; init; } = string.Empty;

    public Gender? Gender { get; init; }
    public int? Age { get; init; }
    public double? HeightCm { get; init; }
    public double? WeightKg { get; init; }
    public ActivityLevel? ActivityLevel { get; init; }
    public Goal? Goal { get; init; }
    public double? TargetWeightKg { get; init; }
}

public sealed record ProfileDto(
    Gender Gender,
    int Age,
    double HeightCm,
    double WeightKg,
    ActivityLevel ActivityLevel,
    Goal Goal,
    double? TargetWeightKg,
    DateTime UpdatedAtUtc)
{
    public static ProfileDto From(UserProfile p) =>
        new(p.Gender, p.Age, p.HeightCm, p.WeightKg, p.ActivityLevel, p.Goal, p.TargetWeightKg, p.UpdatedAtUtc);
}

public sealed record ProfileResponse(ProfileDto Profile, DailyTargets Targets)
{
    public static ProfileResponse From(UserProfile p) => new(ProfileDto.From(p), TargetCalculator.Calculate(p));
}
