namespace Himma.Api.Domain.Nutrition;

public static class NutritionMath
{
    /// <summary>Amount in a portion, from a value per 100 g.</summary>
    public static double ForGrams(double per100g, double grams) => Round1(per100g * grams / 100);

    public static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
