using Himma.Api.Data;
using Himma.Api.Data.Seed;
using Himma.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Database;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations (only when Database:AutoMigrate is true) and runs the idempotent seeders.
    /// In production, migrations must be applied explicitly; startup stops if any are pending.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            if (!options.AutoMigrate)
            {
                throw new InvalidOperationException(
                    $"The database has {pending.Count} pending migration(s). Apply them with 'dotnet ef database update' before starting the app.");
            }

            logger.LogInformation("Applying {Count} pending migration(s)", pending.Count);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await IdentitySeeder.SeedAsync(provider, logger);
        await FoodSeeder.SeedAsync(db, logger, cancellationToken);
        await ExerciseSeeder.SeedAsync(db, logger, cancellationToken);
    }
}
