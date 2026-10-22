using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Data.Seed;

public static class FoodSeeder
{
    /// <summary>Inserts any seed food whose name is not in the database yet. Never overwrites or deletes.</summary>
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var existingNames = await db.Foods.Select(f => f.Name).ToListAsync(cancellationToken);
        var known = existingNames.ToHashSet();

        var missing = FoodSeedData.All.Where(f => !known.Contains(f.Name)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        db.Foods.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} foods", missing.Count);
    }
}
