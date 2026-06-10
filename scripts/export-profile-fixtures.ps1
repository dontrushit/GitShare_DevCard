param(
    [string]$MatrixPath = "$PSScriptRoot/profile-matrix.json",
    [string]$OutDir = "$PSScriptRoot/../tests/GitShare.Api.Tests/Fixtures/profiles",
    [string[]]$OnlyUser = @(),
    [switch]$UseCache
)

$ErrorActionPreference = "Stop"
$matrix = Get-Content $MatrixPath -Raw | ConvertFrom-Json
$base = $matrix.apiBaseUrl.TrimEnd("/")
$refresh = if ($UseCache) { "false" } else { "true" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$profiles = $matrix.profiles | Where-Object { $_.expectApiError -ne $true }
if ($OnlyUser.Count -gt 0) {
    $filter = @($OnlyUser | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $profiles = $profiles | Where-Object { $filter -contains $_.username }
}

foreach ($entry in $profiles) {
    $user = $entry.username
    $uri = "$base/api/profile/$user" + $(if ($refresh -eq "true") { "?forceRefresh=true" } else { "" })
    Write-Host "Export $user ..." -ForegroundColor Cyan
    $profile = Invoke-RestMethod -Uri $uri -TimeoutSec 600 -Method Get
    $path = Join-Path $OutDir "$user.json"
    $profile | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
    Write-Host "  -> $path (level=$($profile.ProgrammerLevel.Code))" -ForegroundColor Green
}

Write-Host "`nDone. Run: dotnet test tests/GitShare.Api.Tests" -ForegroundColor Yellow
