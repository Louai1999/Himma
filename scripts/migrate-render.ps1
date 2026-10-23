<#
.SYNOPSIS
  Applies Himma's database updates (migrations) and seed data to a hosted PostgreSQL database.

.DESCRIPTION
  Render's free plan has no "pre-deploy command", so run this from your PC:
    - once, right after the database is created, and
    - again whenever a new version contains a database update (a new folder in Migrations).
  It asks for the database's External URL without showing it on screen, and keeps it only for
  this run (it is never written to a file).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\migrate-render.ps1
#>
param(
    # Optional: pass the URL directly (otherwise you are asked for it).
    [string] $DatabaseUrl
)

$ErrorActionPreference = 'Stop'

if (-not $DatabaseUrl) {
    $secure = Read-Host 'Paste the External Database URL from Render (it will not be shown)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $DatabaseUrl = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

$DatabaseUrl = $DatabaseUrl.Trim()
if ($DatabaseUrl -notmatch '^postgres(ql)?://') {
    throw 'That does not look like a database URL. It should start with postgresql://'
}

# Connections from outside Render must be encrypted.
if ($DatabaseUrl -notmatch 'sslmode=') {
    $separator = if ($DatabaseUrl.Contains('?')) { '&' } else { '?' }
    $DatabaseUrl = "$DatabaseUrl${separator}sslmode=require"
}

$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:Database__Provider = 'Postgres'
$env:DATABASE_URL = $DatabaseUrl

Push-Location (Join-Path $PSScriptRoot '..\src\Himma.Api')
try {
    Write-Host 'Applying database updates...'
    dotnet run --no-launch-profile -- --migrate
    if ($LASTEXITCODE -ne 0) { throw "The database update failed (exit code $LASTEXITCODE)." }
    Write-Host 'Done. The database is up to date.' -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item Env:DATABASE_URL, Env:Database__Provider, Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
}
