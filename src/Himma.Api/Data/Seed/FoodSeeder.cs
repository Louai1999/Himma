using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Data.Seed;

public static class FoodSeeder
{
    /// <summary>
    /// Inserts any seed food whose (Arabic) name is not in the database yet, and fills in English
    /// text that older databases do not have. Never changes values or deletes.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var existing = await db.Foods.ToDictionaryAsync(f => f.Name, cancellationToken);
        var added = 0;
        var translated = 0;

        foreach (var seed in FoodSeedData.All)
        {
            if (!existing.TryGetValue(seed.Name, out var food))
            {
                db.Foods.Add(seed);
                added++;
            }
            else if (food.NameEn is null)
            {
                food.NameEn = seed.NameEn;
                food.ServingLabelEn = seed.ServingLabelEn;
                translated++;
            }
        }

        if (added + translated == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Added} foods and added English names to {Translated}", added, translated);
    }
}
