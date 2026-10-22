using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Himma.Tests;

/// <summary>
/// Starts the real app against a private database.
/// By default: an in-memory SQLite database (the open connection keeps it alive for the factory's lifetime).
/// With the environment variable HIMMA_TEST_POSTGRES set to a PostgreSQL server connection string
/// (no database name), each factory creates its own temporary PostgreSQL database instead, so the
/// whole suite also verifies the production database provider and its migrations.
/// </summary>
public sealed class HimmaApiFactory : WebApplicationFactory<Program>
{
    // The seeded admin's password is random per test run, so no credential is ever stored in the repo.
    public const string AdminUsername = "test-admin";
    public string AdminPassword { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("HIMMA_TEST_POSTGRES");

    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;
    private readonly string? _postgresDatabase;

    public HimmaApiFactory()
    {
        if (string.IsNullOrWhiteSpace(PostgresServer))
        {
            _connectionString = $"Data Source=himma-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
        }
        else
        {
            _postgresDatabase = $"himma_test_{Guid.NewGuid():N}";
            _connectionString = new NpgsqlConnectionStringBuilder(PostgresServer) { Database = _postgresDatabase }.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "true");
        if (_postgresDatabase is null)
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:Sqlite", _connectionString);
        }
        else
        {
            builder.UseSetting("Database:Provider", "Postgres");
            builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        }
        builder.UseSetting("Admin:Username", AdminUsername);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));

        // All test requests come from the same (empty) IP, so lift the limits; one test checks them explicitly.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Api:PermitLimit", "100000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        _keepAlive?.Dispose();
        if (_postgresDatabase is not null)
        {
            NpgsqlConnection.ClearAllPools();
            using var admin = new NpgsqlConnection(PostgresServer);
            admin.Open();
            using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_postgresDatabase}\" WITH (FORCE)", admin);
            drop.ExecuteNonQuery();
        }
    }
}
