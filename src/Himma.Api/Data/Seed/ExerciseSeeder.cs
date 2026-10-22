using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Data.Seed;

public static class ExerciseSeeder
{
    /// <summary>Inserts any system exercise whose name is not in the database yet. Never overwrites or deletes.</summary>
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var existing = (await db.Exercises
            .Where(e => e.OwnerUserId == null)
            .Select(e => e.Name)
            .ToListAsync(cancellationToken)).ToHashSet();

        var missing = ExerciseSeedData.All.Where(e => !existing.Contains(e.Name)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        db.Exercises.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} system exercises", missing.Count);
    }
}
