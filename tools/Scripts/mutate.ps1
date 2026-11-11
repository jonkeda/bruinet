#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Mutation testing at the gate: Stryker.NET on the code a change touched (AD-033).

.DESCRIPTION
    For each library below that the change since -Since touched, mutates only its changed files and
    runs its unit tests against each mutant. A mutant that survives is a change the tests did not
    notice: a test to add, or code no test needs. Fails when a library's score on the changed code
    is under -Break. Reports land in TestResults/mutation/<library>.

    The libraries are the ones whose logic their own unit tests prove, and the controls whose suites
    run in the test process: Html's on bUnit, Blazor's on its unit tests, Maui's on MauiHeadless
    (BRUINET_HEAD=headless). The drivers and the device heads stay with the UI suites.

.PARAMETER Since
    The revision the change starts from. Default: origin/move.

.PARAMETER Break
    The lowest mutation score, in percent, that passes. Default: 0, a report; the baselines on
    2026-11-01 were 14 to 41% on the files a change touched, 24% on all of Bruinet.Core.

.PARAMETER All
    Mutate every file, not only the changed ones: a baseline, minutes per library.

.PARAMETER Mutate
    Only these files, as globs under each library (for example '**/Controls/RangeControlBase.tpl.cs'),
    whatever changed: a baseline of the files named. With -Library, one library.

.PARAMETER Library
    Only this library.

.EXAMPLE
    ./tools/Scripts/mutate.ps1
    ./tools/Scripts/mutate.ps1 -Since HEAD~3 -Break 70
    ./tools/Scripts/mutate.ps1 -Library Bruinet.Html -Mutate '**/Controls/RangeControlBase.tpl.cs'
#>
param(
    [string] $Since = 'origin/move',
    [int] $Break = 0,
    [switch] $All,
    [string[]] $Mutate = @(),
    [string] $Library
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $root

# library under src/ -> its unit test projects under tests/, the first one's folder the run's. Bruinet.Bdd's binder and
# callables are tested from Bruinet.Uat.Tests too, which a run on Bdd.Tests alone counted as survivors (review 08, finding 13).
$libraries = [ordered]@{
    'Bruinet.Core'         = 'Bruinet.Core.Tests'
    'Bruinet.Bdd'          = @('Bruinet.Bdd.Tests', 'Bruinet.Uat.Tests')
    'Bruinet.TestData'     = 'Bruinet.TestData.Tests'
    'Bruinet.Traits'       = 'Bruinet.Traits.Tests'
    'Bruinet.Mocking.Data' = 'Bruinet.Mocking.Data.Tests'
    'Bruinet.Recording'    = 'Bruinet.Recording.Tests'
    'Bruinet.Html'         = 'Bruinet.Html.Bunit.Tests'
    'Bruinet.Blazor'       = 'Bruinet.Blazor.Tests'
    'Bruinet.Maui'         = 'Bruinet.Maui.UITests'
}

# A library whose suite needs a head: the one that runs in the test process.
$heads = @{ 'Bruinet.Maui' = 'headless' }

# The changed files come from git here: Stryker's own --since cannot read a submodule's history.
$changed = git diff --name-only $Since -- src
$failed = @()
foreach ($current in $libraries.Keys) {
    if ($Library -and $current -ne $Library) { continue }
    $files = @($changed | Where-Object { $_ -like "src/$current/*.cs" -and $_ -notlike '*.gen.cs' -and (Test-Path $_) })
    if (-not $All -and -not $Mutate -and -not $files) { continue }

    $output = Join-Path $root "TestResults/mutation/$current"
    $arguments = @('--project', "$current.csproj", '--target-framework', 'net10.0',
        '--reporter', 'html', '--reporter', 'json', '--reporter', 'progress',
        '--break-at', $Break, '--output', $output)
    if ($Mutate) {
        foreach ($glob in $Mutate) { $arguments += '--mutate', $glob }
    }
    elseif (-not $All) {
        foreach ($file in $files) { $arguments += '--mutate', ('**/' + $file.Substring("src/$current/".Length)) }
    }

    $tests = @($libraries[$current])
    if ($tests.Count -gt 1) {
        foreach ($test in $tests) { $arguments += '--test-project', (Join-Path $root "tests/$test/$test.csproj") }
    }

    Write-Host "== $current"
    Push-Location (Join-Path $root "tests/$($tests[0])")
    $head = $env:BRUINET_HEAD
    try {
        if ($heads[$current]) { $env:BRUINET_HEAD = $heads[$current] }
        dotnet tool run dotnet-stryker -- @arguments
        if ($LASTEXITCODE -ne 0) { $failed += $current }
    }
    finally {
        $env:BRUINET_HEAD = $head
        Pop-Location
    }
}

if ($failed) {
    Write-Error "Mutation score under $Break% on the changed code of: $($failed -join ', '). The surviving mutants are in TestResults/mutation/<library>/reports."
    exit 1
}
Write-Host 'Mutation testing: done.'
