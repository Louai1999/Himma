using Himma.Api.Data;
using Himma.Api.Data.Seed;
using Himma.Api.Infrastructure.Push;
using Himma.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Database;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations and runs the idempotent seeders. Migrations are applied only when
    /// Database:AutoMigrate is true (Development) or when explicitly asked (the "--migrate" command used
    /// at deploy time). Otherwise startup stops if any migration is pending.
    /// </summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services, bool applyMigrations = false, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            if (!options.AutoMigrate && !applyMigrations)
            {
                throw new InvalidOperationException(
                    $"The database has {pending.Count} pending migration(s). Apply them first: run the app once with --migrate " +
                    "(or 'dotnet ef database update'), then start it normally.");
            }

            logger.LogInformation("Applying {Count} pending migration(s)", pending.Count);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await IdentitySeeder.SeedAsync(provider, logger);
        await FoodSeeder.SeedAsync(db, logger, cancellationToken);
        await ExerciseSeeder.SeedAsync(db, logger, cancellationToken);
        await provider.GetRequiredService<PushKeyStore>().EnsureAsync(db, logger, cancellationToken);
    }
}
