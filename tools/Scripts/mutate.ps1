#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Mutation testing at the gate: Stryker.NET on the code a change touched (AD-033).

.DESCRIPTION
    For each library below that the change since -Since touched, mutates only its changed files and
    runs its unit tests against each mutant. A mutant that survives is a change the tests did not
    notice: a test to add, or code no test needs. Fails when a library's score on the changed code
    is under -Break. Reports land in TestResults/mutation/<library>.

    The libraries are the ones whose logic their own unit tests prove; the drivers and controls are
    proven by the UI suites, which Stryker cannot run.

.PARAMETER Since
    The revision the change starts from. Default: origin/move.

.PARAMETER Break
    The lowest mutation score, in percent, that passes. Default: 0, a report; the baselines on
    2026-11-01 were 14 to 41% on the files a change touched, 24% on all of Bruinet.Core.

.PARAMETER All
    Mutate every file, not only the changed ones: a baseline, minutes per library.

.EXAMPLE
    ./tools/Scripts/mutate.ps1
    ./tools/Scripts/mutate.ps1 -Since HEAD~3 -Break 70
#>
param(
    [string] $Since = 'origin/move',
    [int] $Break = 0,
    [switch] $All
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $root

# library under src/ -> its unit test project under tests/
$libraries = [ordered]@{
    'Bruinet.Core'         = 'Bruinet.Core.Tests'
    'Bruinet.Bdd'          = 'Bruinet.Bdd.Tests'
    'Bruinet.TestData'     = 'Bruinet.TestData.Tests'
    'Bruinet.Mocking.Data' = 'Bruinet.Mocking.Data.Tests'
    'Bruinet.Recording'    = 'Bruinet.Recording.Tests'
}

# The changed files come from git here: Stryker's own --since cannot read a submodule's history.
$changed = git diff --name-only $Since -- src
$failed = @()
foreach ($library in $libraries.Keys) {
    $files = @($changed | Where-Object { $_ -like "src/$library/*.cs" -and $_ -notlike '*.gen.cs' -and (Test-Path $_) })
    if (-not $All -and -not $files) { continue }

    $output = Join-Path $root "TestResults/mutation/$library"
    $arguments = @('--project', "$library.csproj", '--target-framework', 'net10.0',
        '--reporter', 'html', '--reporter', 'json', '--reporter', 'progress',
        '--break-at', $Break, '--output', $output)
    if (-not $All) {
        foreach ($file in $files) { $arguments += '--mutate', ('**/' + $file.Substring("src/$library/".Length)) }
    }

    Write-Host "== $library"
    Push-Location (Join-Path $root "tests/$($libraries[$library])")
    try {
        dotnet tool run dotnet-stryker -- @arguments
        if ($LASTEXITCODE -ne 0) { $failed += $library }
    }
    finally {
        Pop-Location
    }
}

if ($failed) {
    Write-Error "Mutation score under $Break% on the changed code of: $($failed -join ', '). The surviving mutants are in TestResults/mutation/<library>/reports."
    exit 1
}
Write-Host 'Mutation testing: done.'
