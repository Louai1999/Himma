using Himma.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Himma.Api.Data;

// Used only by the `dotnet ef` tool (migrations add / database update), never at runtime.
// The tool runs with the project folder as the current directory.

internal static class DesignTimeConfiguration
{
    public static IConfiguration Build()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = DatabaseSetup.ResolveSqlite(configuration.GetConnectionString("Sqlite"), Directory.GetCurrentDirectory());
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite(connectionString).Options;
        return new SqliteAppDbContext(options);
    }
}

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("Postgres");

        // `migrations add` never connects, so a placeholder is enough there.
        // `database update` needs the real value from the ConnectionStrings__Postgres environment variable.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Database=himma_design_time";
        }

        var options = new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql(connectionString).Options;
        return new PostgresAppDbContext(options);
    }
}
