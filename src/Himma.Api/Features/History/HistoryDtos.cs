using Himma.Api.Domain.Nutrition;

namespace Himma.Api.Features.History;

public sealed record HistoryDayDto(DateOnly Date, double Calories, double ProteinG, int WaterMl, int MealCount)
{
    public static HistoryDayDto From(DayTotals d) =>
        new(d.Date, NutritionMath.Round1(d.Calories), NutritionMath.Round1(d.ProteinG), d.WaterMl, d.MealCount);
}

/// <summary>One bar of the daily chart. The adherence flags are null when the user has no profile yet.</summary>
public sealed record SummaryDayDto(
    DateOnly Date,
    double Calories,
    double ProteinG,
    int WaterMl,
    int MealCount,
    bool? CalorieAdherent,
    bool? ProteinAdherent,
    bool? WaterAdherent);

public sealed record WeightPointDto(DateOnly Date, double WeightKg);

public sealed record HistorySummaryResponse(
    int Days,
    DateOnly From,
    DateOnly To,
    DailyTargets? Targets,
    int Streak,
    AdherenceSummary Adherence,
    IReadOnlyList<SummaryDayDto> Daily,
    IReadOnlyList<WeightPointDto> Weights);
