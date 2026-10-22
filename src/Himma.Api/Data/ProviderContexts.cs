using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Data;

/// <summary>Development database. Migrations live in Migrations/Sqlite.</summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);

/// <summary>Production database. Migrations live in Migrations/Postgres.</summary>
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options);
