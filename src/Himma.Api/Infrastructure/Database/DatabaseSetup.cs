using Himma.Api.Data;
using Himma.Api.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            // Hosting platforms (Render, Railway) provide DATABASE_URL; an explicit connection string wins.
            var connectionString = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = configuration["DATABASE_URL"];
            }
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database:Provider is Postgres but no connection string was found. Set ConnectionStrings__Postgres or DATABASE_URL.");
            }
            var npgsql = NormalizePostgres(connectionString);
            services.AddDbContext<AppDbContext, PostgresAppDbContext>(db => db.UseNpgsql(npgsql));
        }
        else
        {
            throw new InvalidOperationException($"Unknown Database:Provider '{options.Provider}'. Use 'Sqlite' or 'Postgres'.");
        }

        return services;
    }

    /// <summary>
    /// Accepts both the Npgsql format ("Host=...;Database=...") and the URL format hosting platforms give
    /// ("postgresql://user:password@host:5432/database"), and returns the Npgsql format.
    /// </summary>
    public static string NormalizePostgres(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            SslMode = SslMode.Prefer,
        };

        // Keep options such as ?sslmode=require from the URL.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(Uri.UnescapeDataString(parts[1]), ignoreCase: true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }
        return builder.ConnectionString;
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
