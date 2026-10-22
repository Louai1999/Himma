using Himma.Api.Domain;
using Himma.Api.Domain.Entities;

namespace Himma.Api.Features.Meals;

/// <summary>
/// How a meal is logged depends on <see cref="Source"/>:
/// manual = system food (FoodId + Grams), custom = the user's food (CustomFoodId + Grams),
/// ai = values the user confirmed from an AI estimate (Name + Calories + ProteinG, carbs/fat optional).
/// For manual and custom the server calculates the nutrition itself.
/// </summary>
public sealed class MealRequest
{
    public string Date { get; init; } = string.Empty;
    public MealType? MealType { get; init; }
    public MealSource? Source { get; init; }

    public int? FoodId { get; init; }
    public int? CustomFoodId { get; init; }
    public double? Grams { get; init; }

    public string? Name { get; init; }
    public double? Calories { get; init; }
    public double? ProteinG { get; init; }
    public double? CarbsG { get; init; }
    public double? FatG { get; init; }
}

public sealed record MealDto(
    int Id,
    DateOnly Date,
    MealType MealType,
    string Name,
    double Grams,
    double Calories,
    double ProteinG,
    double CarbsG,
    double FatG,
    MealSource Source,
    DateTime CreatedAtUtc)
{
    public static MealDto From(MealEntry m) =>
        new(m.Id, m.Date, m.MealType, m.Name, m.Grams, m.Calories, m.ProteinG, m.CarbsG, m.FatG, m.Source, m.CreatedAtUtc);
}

public sealed record MealTotals(double Calories, double ProteinG, double CarbsG, double FatG);

public sealed record DayMealsResponse(DateOnly Date, IReadOnlyList<MealDto> Items, MealTotals Totals);
