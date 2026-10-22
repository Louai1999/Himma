using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Meals;

[Route("api/meals")]
public sealed class MealsController(AppDbContext db) : HimmaControllerBase
{
    [HttpGet("{date}")]
    public async Task<ActionResult<DayMealsResponse>> GetDay(string date, CancellationToken cancellationToken)
    {
        if (!ApiDate.TryParse(date, out var day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        var userId = UserId;
        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => m.UserId == userId && m.Date == day)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var totals = new MealTotals(
            NutritionMath.Round1(meals.Sum(m => m.Calories)),
            NutritionMath.Round1(meals.Sum(m => m.ProteinG)),
            NutritionMath.Round1(meals.Sum(m => m.CarbsG)),
            NutritionMath.Round1(meals.Sum(m => m.FatG)));

        return new DayMealsResponse(day, meals.Select(MealDto.From).ToList(), totals);
    }

    [HttpPost]
    public async Task<ActionResult<MealDto>> Create(MealRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var entry = new MealEntry
        {
            UserId = userId,
            Date = ApiDate.Parse(request.Date),
            MealType = request.MealType!.Value,
            Source = request.Source!.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };

        switch (entry.Source)
        {
            case MealSource.Manual:
                var food = await db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FoodId, cancellationToken);
                if (food is null)
                {
                    return InvalidField("foodId", Lang.T("هذا الطعام غير موجود.", "This food does not exist."));
                }
                FillFromPer100g(entry, food.Name, request.Grams!.Value, food.KcalPer100g, food.ProteinPer100g, food.CarbsPer100g, food.FatPer100g);
                break;

            case MealSource.Custom:
                // Filtered by owner: another user's custom food id is treated as not existing.
                var custom = await db.CustomFoods.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == request.CustomFoodId && f.UserId == userId, cancellationToken);
                if (custom is null)
                {
                    return InvalidField("customFoodId", Lang.T("هذا الطعام غير موجود في أطعمتك.", "This food is not in your foods."));
                }
                FillFromPer100g(entry, custom.Name, request.Grams!.Value, custom.KcalPer100g, custom.ProteinPer100g, 0, 0);
                break;

            case MealSource.Ai:
                entry.Name = request.Name!.Trim();
                entry.Grams = NutritionMath.Round1(request.Grams ?? 0);
                entry.Calories = NutritionMath.Round1(request.Calories!.Value);
                entry.ProteinG = NutritionMath.Round1(request.ProteinG!.Value);
                entry.CarbsG = NutritionMath.Round1(request.CarbsG ?? 0);
                entry.FatG = NutritionMath.Round1(request.FatG ?? 0);
                break;
        }

        db.MealEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, MealDto.From(entry));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var deleted = await db.MealEntries
            .Where(m => m.Id == id && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFoundProblem() : NoContent();
    }

    private static void FillFromPer100g(MealEntry entry, string name, double grams, double kcal, double protein, double carbs, double fat)
    {
        entry.Name = name;
        entry.Grams = NutritionMath.Round1(grams);
        entry.Calories = NutritionMath.ForGrams(kcal, grams);
        entry.ProteinG = NutritionMath.ForGrams(protein, grams);
        entry.CarbsG = NutritionMath.ForGrams(carbs, grams);
        entry.FatG = NutritionMath.ForGrams(fat, grams);
    }
}
