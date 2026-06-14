param(
    [string]$MatrixPath = "$PSScriptRoot/profile-matrix.json",
    [string[]]$OnlyUser = @(),
    [switch]$UseCache,
    [int]$DelayBetweenProfilesSeconds = 0,
    [int]$MaxRetriesOn429 = 3
)

$ErrorActionPreference = "Stop"
$matrix = Get-Content $MatrixPath -Raw | ConvertFrom-Json
$base = $matrix.apiBaseUrl.TrimEnd("/")
$refresh = if ($UseCache) { "false" } else { [string]$matrix.forceRefresh }
if ($DelayBetweenProfilesSeconds -le 0 -and $refresh -eq "true") {
    $DelayBetweenProfilesSeconds = 20
}

function Invoke-ProfileApi {
    param([string]$Uri, [int]$Retries)
    $attempt = 0
    while ($true) {
        try {
            return Invoke-RestMethod -Uri $Uri -TimeoutSec 600 -Method Get
        }
        catch {
            $msg = $_.Exception.Message
            $is429 = $msg -match '429|Too Many Requests'
            if (-not $is429 -or $attempt -ge $Retries) { throw }
            $wait = 60 * [math]::Pow(2, $attempt)
            Write-Host "  rate limited, waiting ${wait}s (retry $($attempt + 1)/$Retries)..." -ForegroundColor Yellow
            Start-Sleep -Seconds $wait
            $attempt++
        }
    }
}

$profiles = $matrix.profiles
if ($OnlyUser.Count -gt 0) {
    $filter = @($OnlyUser | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $profiles = $profiles | Where-Object { $filter -contains $_.username }
}

$results = @()
$failed = 0

foreach ($entry in $profiles) {
    $user = $entry.username
    $issues = [System.Collections.Generic.List[string]]::new()
    Write-Host "`n=== $user (wave $($entry.wave)) ===" -ForegroundColor Cyan

    try {
        $uri = "$base/api/profile/$user" + $(if ($refresh -eq "true") { "?forceRefresh=true" } else { "" })
        $profile = Invoke-ProfileApi -Uri $uri -Retries $MaxRetriesOn429
    }
    catch {
        if ($entry.expectApiError -eq $true) {
            Write-Host "  OK (expected API error)" -ForegroundColor Green
            $results += [pscustomobject]@{ User = $user; Wave = $entry.wave; Ok = $true; Issues = "expected 404" }
            continue
        }
        $issues.Add("API error: $($_.Exception.Message)")
        $results += [pscustomobject]@{ User = $user; Wave = $entry.wave; Ok = $false; Issues = ($issues -join "; ") }
        $failed++
        Write-Host "  FAIL: API" -ForegroundColor Red
        continue
    }

    if ($entry.expectApiError -eq $true) {
        $issues.Add("expected API error but got profile")
    }

    $level = $profile.ProgrammerLevel.Code
    if ($entry.levelIn -and ($entry.levelIn -notcontains $level)) {
        $issues.Add("level=$level expected in [$($entry.levelIn -join ', ')]")
    }

    $projects = @($profile.AuditData.Projects)
    $repoNames = $projects | ForEach-Object { $_.RepoName }

    if ($entry.auditReposMustNotContain) {
        foreach ($bad in $entry.auditReposMustNotContain) {
            if ($repoNames -contains $bad) { $issues.Add("audit must not include $bad") }
        }
    }

    if ($entry.forbidAllAuditDocOps -eq $true) {
        $docCount = @($projects | Where-Object { $_.ProjectClass -match "DocOps" }).Count
        if ($docCount -ge $projects.Count -and $projects.Count -gt 0) {
            $issues.Add("all $($projects.Count) audit projects are DocOps")
        }
    }

    if ($null -ne $entry.minDocOpsInAudit) {
        $docCount = @($projects | Where-Object { $_.ProjectClass -match "DocOps" }).Count
        if ($docCount -lt $entry.minDocOpsInAudit) {
            $issues.Add("DocOps count=$docCount min=$($entry.minDocOpsInAudit)")
        }
    }

    if ($null -ne $entry.maxDocOpsInAudit) {
        $docCount = @($projects | Where-Object { $_.ProjectClass -match "DocOps" }).Count
        if ($docCount -gt $entry.maxDocOpsInAudit) {
            $issues.Add("DocOps count=$docCount max=$($entry.maxDocOpsInAudit)")
        }
    }

    if ($entry.dominantLanguage -and $null -ne $entry.minDominantLanguagePercent) {
        $top = @($profile.LanguageStack | Sort-Object { $_.Percentage } -Descending | Select-Object -First 1)
        if ($top.Count -eq 0) {
            $issues.Add("LanguageStack empty")
        }
        else {
            $lang = $top[0].Language
            $pct = $top[0].Percentage
            if ($lang -ne $entry.dominantLanguage) {
                $issues.Add("top language=$lang expected $($entry.dominantLanguage)")
            }
            if ($pct -lt $entry.minDominantLanguagePercent) {
                $issues.Add("$lang percent=$pct min=$($entry.minDominantLanguagePercent)")
            }
        }
    }

    foreach ($rule in @($entry.projectRules | Where-Object { $_.repo -and $_.repo.Trim() })) {
        $p = $projects | Where-Object { $_.RepoName -eq $rule.repo } | Select-Object -First 1
        if (-not $p) {
            $issues.Add("missing project $($rule.repo) in audit (got: $($repoNames -join ', '))")
            continue
        }
        foreach ($must in @($rule.frameworkMustContain | Where-Object { $_ })) {
            if ($p.Framework -notmatch [regex]::Escape($must)) {
                $issues.Add("$($rule.repo).Framework='$($p.Framework)' must contain $must")
            }
        }
        foreach ($not in @($rule.frameworkMustNotContain | Where-Object { $_ })) {
            if ($p.Framework -match [regex]::Escape($not)) {
                $issues.Add("$($rule.repo).Framework must not contain $not")
            }
        }
        if ($rule.classMustNotBe -and $p.ProjectClass -eq $rule.classMustNotBe) {
            $issues.Add("$($rule.repo).Class=$($p.ProjectClass)")
        }
        if ($rule.classShouldBe -and $p.ProjectClass -ne $rule.classShouldBe) {
            $issues.Add("$($rule.repo).Class=$($p.ProjectClass) expected $($rule.classShouldBe)")
        }
        foreach ($debt in @($rule.debtMustContain | Where-Object { $_ })) {
            if ($p.TechnicalDebt -notmatch [regex]::Escape($debt)) {
                $issues.Add("$($rule.repo).debt missing '$debt'")
            }
        }
        foreach ($debt in @($rule.debtMustNotContain | Where-Object { $_ })) {
            if ($p.TechnicalDebt -match [regex]::Escape($debt)) {
                $issues.Add("$($rule.repo).debt must not contain '$debt'")
            }
        }
    }

    foreach ($bad in @($entry.forbidProsContaining | Where-Object { $_ })) {
        foreach ($p in $projects) {
            foreach ($pro in @($p.Pros)) {
                if ($pro -match [regex]::Escape($bad)) {
                    $issues.Add("$($p.RepoName) pros contain forbidden '$bad'")
                }
            }
        }
    }

    foreach ($p in $projects) {
        foreach ($pro in @($p.Pros)) {
            if ($pro -match "Wallet/Controller|MVC / MV / Flat" -and $p.RepoName -notmatch "architecture-examples|unity") {
                $issues.Add("$($p.RepoName): hallucination pros")
            }
        }
        if ($p.TechnicalDebt -match "оценивайте|Evaluate the|Look at the") {
            $issues.Add("$($p.RepoName): instructional debt tone")
        }
    }

    $ok = $issues.Count -eq 0
    if (-not $ok) { $failed++ }
    $issueText = if ($ok) { "OK" } else { $issues -join " | " }
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host "  $issueText" -ForegroundColor $color
    if (-not $ok) {
        $projects | ForEach-Object { Write-Host "    $($_.RepoName) | $($_.Framework) | $($_.ProjectClass)" }
    }

    $results += [pscustomobject]@{
        User = $user
        Wave = $entry.wave
        Level = $level
        Score = $profile.ProgrammerLevel.Score
        AuditRepos = ($repoNames -join ", ")
        Ok = $ok
        Issues = $issueText
    }

    if ($DelayBetweenProfilesSeconds -gt 0 -and $entry -ne $profiles[-1]) {
        Start-Sleep -Seconds $DelayBetweenProfilesSeconds
    }
}

$reportPath = Join-Path $PSScriptRoot "regression-report.json"
$passed = $results.Count - $failed
$passRate = if ($results.Count -gt 0) { [math]::Round(100.0 * $passed / $results.Count, 1) } else { 0 }

$cohortByUser = @{}
foreach ($p in $profiles) { $cohortByUser[$p.username] = $p.cohort }
$byCohort = $results | Group-Object { if ($cohortByUser.ContainsKey($_.User)) { $cohortByUser[$_.User] } else { "unknown" } }
$cohortStats = foreach ($g in $byCohort) {
    $cohortName = if ($g.Name) { $g.Name } else { "unknown" }
    $cohortFailed = @($g.Group | Where-Object { -not $_.Ok }).Count
    [pscustomobject]@{
        cohort = $cohortName
        total = $g.Count
        passed = $g.Count - $cohortFailed
        failed = $cohortFailed
    }
}

$byWave = $results | Group-Object Wave | Sort-Object { [int]$_.Name }
$waveStats = foreach ($g in $byWave) {
    $waveFailed = @($g.Group | Where-Object { -not $_.Ok }).Count
    [pscustomobject]@{
        wave = [int]$g.Name
        total = $g.Count
        passed = $g.Count - $waveFailed
        failed = $waveFailed
    }
}

$report = [pscustomobject]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    summary = [pscustomobject]@{
        total = $results.Count
        passed = $passed
        failed = $failed
        passRatePercent = $passRate
    }
    byCohort = $cohortStats
    byWave = $waveStats
    results = $results
}

$report | ConvertTo-Json -Depth 6 | Set-Content $reportPath -Encoding utf8

Write-Host "`n--- Summary: $passed/$($results.Count) passed ($passRate%), $failed failed ---" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "By cohort:" -ForegroundColor Cyan
$cohortStats | ForEach-Object { Write-Host "  $($_.cohort): $($_.passed)/$($_.total)" }
Write-Host "Report: $reportPath"
exit $failed
