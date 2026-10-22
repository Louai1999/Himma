using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Data.Seed;

public static class ExerciseSeeder
{
    /// <summary>
    /// Inserts any system exercise whose (Arabic) name is not in the database yet, and fills in
    /// English text that older databases do not have. Never changes values or deletes.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var existing = await db.Exercises
            .Where(e => e.OwnerUserId == null)
            .ToDictionaryAsync(e => e.Name, cancellationToken);
        var added = 0;
        var translated = 0;

        foreach (var seed in ExerciseSeedData.All)
        {
            if (!existing.TryGetValue(seed.Name, out var exercise))
            {
                db.Exercises.Add(seed);
                added++;
            }
            else if (exercise.NameEn is null)
            {
                exercise.NameEn = seed.NameEn;
                exercise.EquipmentEn = seed.EquipmentEn;
                exercise.InstructionsEn = seed.InstructionsEn;
                translated++;
            }
        }

        if (added + translated == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Added} system exercises and added English text to {Translated}", added, translated);
    }
}
