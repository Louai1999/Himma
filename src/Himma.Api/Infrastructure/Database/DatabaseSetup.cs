using Himma.Api.Data;
using Himma.Api.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Infrastructure.Database;

public static class DatabaseSetup
{
    /// <summary>Registers AppDbContext for the provider named in Database:Provider.</summary>
    public static IServiceCollection AddHimmaDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        if (string.Equals(options.Provider, DatabaseOptions.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = ResolveSqlite(configuration.GetConnectionString("Sqlite"), environment.ContentRootPath);
            services.AddDbContext<AppDbContext, SqliteAppDbContext>(db => db.UseSqlite(connectionString));
        }
        else if (string.Equals(options.Provider, DatabaseOptions.Postgres, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database:Provider is Postgres but ConnectionStrings:Postgres is empty. Set the environment variable ConnectionStrings__Postgres.");
            }
            services.AddDbContext<AppDbContext, PostgresAppDbContext>(db => db.UseNpgsql(connectionString));
        }
        else
        {
            throw new InvalidOperationException($"Unknown Database:Provider '{options.Provider}'. Use 'Sqlite' or 'Postgres'.");
        }

        return services;
    }

    /// <summary>
    /// Makes a relative SQLite file path relative to the project folder, so the database file is the same
    /// whether the app is started from Visual Studio, VS Code or the command line.
    /// </summary>
    public static string ResolveSqlite(string? connectionString, string basePath)
    {
        var builder = new SqliteConnectionStringBuilder(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=himma.db" : connectionString);
        var dataSource = builder.DataSource;

        var isInMemory = dataSource == ":memory:" || builder.Mode == SqliteOpenMode.Memory || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        if (!isInMemory && !Path.IsPathRooted(dataSource))
        {
            builder.DataSource = Path.Combine(basePath, dataSource);
        }

        return builder.ToString();
    }
}
