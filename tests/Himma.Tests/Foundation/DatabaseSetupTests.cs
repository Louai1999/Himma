using Himma.Api.Infrastructure.Database;
using Npgsql;

namespace Himma.Tests.Foundation;

public sealed class DatabaseSetupTests
{
    [Fact]
    public void Platform_database_urls_become_npgsql_connection_strings()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseSetup.NormalizePostgres("postgresql://himma_user:p%40ss%3Aword@dpg-abc.oregon-postgres.render.com:6543/himma_db?sslmode=require"));

        Assert.Equal("dpg-abc.oregon-postgres.render.com", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("himma_db", result.Database);
        Assert.Equal("himma_user", result.Username);
        Assert.Equal("p@ss:word", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Url_without_port_uses_the_default_postgres_port()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseSetup.NormalizePostgres("postgres://u:p@db.internal/himma"));

        Assert.Equal(5432, result.Port);
        Assert.Equal("himma", result.Database);
    }

    [Fact]
    public void Npgsql_connection_strings_are_left_as_they_are()
    {
        const string connectionString = "Host=localhost;Database=himma;Username=postgres";

        Assert.Equal(connectionString, DatabaseSetup.NormalizePostgres(connectionString));
    }
}
