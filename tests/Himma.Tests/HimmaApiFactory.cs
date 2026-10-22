using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Himma.Tests;

/// <summary>
/// Starts the real app against a private in-memory SQLite database.
/// The open connection keeps the shared in-memory database alive for the factory's lifetime.
/// </summary>
public sealed class HimmaApiFactory : WebApplicationFactory<Program>
{
    // The seeded admin's password is random per test run, so no credential is ever stored in the repo.
    public const string AdminUsername = "test-admin";
    public string AdminPassword { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    private readonly string _connectionString = $"Data Source=himma-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public HimmaApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("ConnectionStrings:Sqlite", _connectionString);
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
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
