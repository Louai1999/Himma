# هِمّة (Himma)

Arabic fitness and nutrition web app. ASP.NET Core 8 + EF Core 8 + vanilla JS.
The full step-by-step Arabic guide comes in phase 8; this is the short version.

## Run locally (Windows)

1. Install the .NET 8 SDK (or newer) and make sure the .NET 8 runtime is installed.
2. Trust the HTTPS development certificate (once per machine):
   `dotnet dev-certs https --trust`
3. Set the secrets (stored outside the repo, in your user profile):
   ```
   cd src/Himma.Api
   dotnet user-secrets set "Admin:Username" "your-admin-name"
   dotnet user-secrets set "Admin:Password" "a-long-password-you-choose"
   dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>"
   ```
   A good random key (PowerShell):
   `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`
4. Run: open `Himma.sln` in Visual Studio and press F5 (profile `https`),
   or run `dotnet run --launch-profile https` inside `src/Himma.Api`.
5. Open https://localhost:7099 . In Development the database (`himma.db`) is created
   and migrated automatically.

## Frontend

The app (`src/Himma.Api/wwwroot`) is plain HTML, CSS and JavaScript modules served by the API,
so there is no build step and no Live Server: run the API and open https://localhost:7099.

- Logo: put your `himma-logo.png` in `src/Himma.Api/wwwroot/assets/`. Until then the bundled
  `himma-logo.svg` is used.
- Security: the page never builds HTML from text (no `innerHTML`); everything goes through
  `js/ui/dom.js`, which only creates elements and text nodes.

## Languages (Arabic / English)

Arabic is the default; the button in the header switches to English (the choice is remembered
in the browser and the page reloads right-to-left or left-to-right).

- Frontend: write text as `t('النص العربي')`. The Arabic text is the key; add its English in
  `js/i18n/en.js`. Placeholders: `t('حذف {name}', { name })`.
- Server: user-facing messages are written as `Lang.T("عربي", "English")`. The frontend sends the
  language in the `Accept-Language` header.
- Data: system foods, exercises and ready plans have English names (`NameEn`, ...). User-created
  foods, exercises and logged meals keep the text they were saved with.

## Agent skills

`.claude/skills/apple-design` (installed with `npx skills add https://github.com/emilkowalski/skills --skill apple-design`)
holds interface and motion guidelines for AI coding assistants working on the frontend.

## Tests

`dotnet test` from the repository root.

## Secrets

| Key | Used for | Where |
| --- | --- | --- |
| `Admin:Username`, `Admin:Password` | seeded admin account | user-secrets / env vars |
| `Jwt:Key` | signs login tokens (changing it signs everyone out) | user-secrets / env var `Jwt__Key` |
| `ConnectionStrings:Postgres` | production database | env var `ConnectionStrings__Postgres` |

`Anthropic:ApiKey` (AI coach and meal estimates) comes with the second release.

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
