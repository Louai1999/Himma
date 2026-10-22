# هِمّة (Himma)

Arabic fitness and nutrition web app. ASP.NET Core 8 + EF Core 8 + vanilla JS.
The full step-by-step Arabic guide comes in phase 8; this is the short version.

## Run locally (Windows)

1. Install the .NET 8 SDK (or newer) and make sure the .NET 8 runtime is installed.
2. Trust the HTTPS development certificate (once per machine):
   `dotnet dev-certs https --trust`
3. Set the admin secrets (stored outside the repo, in your user profile):
   ```
   cd src/Himma.Api
   dotnet user-secrets set "Admin:Username" "your-admin-name"
   dotnet user-secrets set "Admin:Password" "a-long-password-you-choose"
   ```
4. Run: open `Himma.sln` in Visual Studio and press F5 (profile `https`),
   or run `dotnet run --launch-profile https` inside `src/Himma.Api`.
5. Open https://localhost:7099 . In Development the database (`himma.db`) is created
   and migrated automatically.

## Tests

`dotnet test` from the repository root.

## Secrets

| Key | Used for | Where |
| --- | --- | --- |
| `Admin:Username`, `Admin:Password` | seeded admin account | user-secrets / env vars |
| `ConnectionStrings:Postgres` | production database | env var `ConnectionStrings__Postgres` |

More keys (`Jwt:Key`, `Anthropic:ApiKey`) are added in later phases.

## Migrations

There is one migrations folder per database provider:

```
dotnet tool restore
cd src/Himma.Api
dotnet dotnet-ef migrations add <Name> --context SqliteAppDbContext --output-dir Migrations/Sqlite
dotnet dotnet-ef migrations add <Name> --context PostgresAppDbContext --output-dir Migrations/Postgres
```

Production never migrates on startup; run
`dotnet dotnet-ef database update --context PostgresAppDbContext` with `ConnectionStrings__Postgres` set.
