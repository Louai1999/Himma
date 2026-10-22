using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Foods;

public static class FoodSources
{
    public const string System = "system";
    public const string Custom = "custom";
}

/// <summary>One searchable food. System and custom ids can overlap, so the client keys on (source, id).</summary>
public sealed record FoodDto(
    int Id,
    string Source,
    string Name,
    double KcalPer100g,
    double ProteinPer100g,
    double? CarbsPer100g,
    double? FatPer100g,
    double? ServingGrams,
    string? ServingLabel)
{
    /// <summary>System foods come back in the request's language.</summary>
    public static FoodDto From(Food f) =>
        new(f.Id, FoodSources.System, Lang.Pick(f.Name, f.NameEn), f.KcalPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g,
            f.ServingGrams, Lang.PickOptional(f.ServingLabel, f.ServingLabelEn));

    public static FoodDto From(CustomFood f) =>
        new(f.Id, FoodSources.Custom, f.Name, f.KcalPer100g, f.ProteinPer100g, null, null, null, null);
}

public sealed class CustomFoodRequest
{
    public string Name { get; init; } = string.Empty;
    public double? KcalPer100g { get; init; }
    public double? ProteinPer100g { get; init; }
}
