#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The unit tests with code coverage, and one report over them (AD-033).

.DESCRIPTION
    Runs each unit test project below with coverlet's collector (coverage.runsettings: Bruinet's
    libraries only, generated code left out), writes a .trx per project, then merges the coverage
    with ReportGenerator (a local tool, .config/dotnet-tools.json) into <Results>/coverage: index.html,
    Summary.txt and SummaryGithub.md. CI runs it as its unit test step.

    A report, not a gate: a line run is not a line proven. Mutation testing (mutate.ps1) says whether
    the tests would notice; coverage shows the code no unit test runs at all. Fails when a test fails,
    after the report is written.

.PARAMETER Projects
    The test projects under tests/. Default: the unit test projects CI runs.

.PARAMETER Results
    Where the .trx files, the coverage files and the report go. Default: TestResults/coverage.

.PARAMETER NoBuild
    Test what is already built.

.EXAMPLE
    ./tools/Scripts/coverage.ps1
    ./tools/Scripts/coverage.ps1 -Projects Bruinet.Core.Tests, Bruinet.Bdd.Tests
#>
param(
    [string[]] $Projects = @(
        'Bruinet.Core.Tests',
        'Bruinet.Maui.Tests',
        'Bruinet.Wpf.Tests',
        'Bruinet.WinForms.Tests',
        'Bruinet.Blazor.Tests',
        'Bruinet.Html.Tests',
        'Bruinet.Stride.Automation.Tests',
        'Bruinet.Mocking.Tests',
        'Bruinet.Uat.Tests',
        'Bruinet.TestData.Tests',
        'Bruinet.Appium.Tests',
        'Bruinet.NativeAndroid.Tests',
        'Bruinet.Cli.Tests'
    ),
    [string] $Results = 'TestResults/coverage',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $root

# A rerun starts clean: old coverage files would be merged into the new report.
Get-ChildItem $Results -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue | Remove-Item

$failed = @()
foreach ($p in $Projects) {
    $arguments = @("tests/$p/$p.csproj", '-v:minimal',
        # Category=Browser drives Chromium against a local site; not installed in CI.
        '--filter', 'Category!=Browser',
        '--collect', 'XPlat Code Coverage', '--settings', 'coverage.runsettings',
        '--logger', "trx;LogFileName=$p.trx", '--results-directory', $Results)
    if ($NoBuild) { $arguments += '--no-build' }
    dotnet test @arguments
    if ($LASTEXITCODE -ne 0) { $failed += $p }
}

dotnet tool restore | Out-Null
# One folder down only: the trx logger keeps a second copy of each file in its own In/ folder.
dotnet reportgenerator "-reports:$Results/*/coverage.cobertura.xml" "-targetdir:$Results/report" `
    '-reporttypes:Html;TextSummary;MarkdownSummaryGithub' '-verbosity:Warning'
if ($LASTEXITCODE -ne 0) { Write-Error 'ReportGenerator failed' }

Get-Content "$Results/report/Summary.txt" -TotalCount 14
Write-Host "Report: $Results/report/index.html"
if ($env:GITHUB_STEP_SUMMARY) { Get-Content "$Results/report/SummaryGithub.md" >> $env:GITHUB_STEP_SUMMARY }

if ($failed) { Write-Error "Failed: $($failed -join ', ')"; exit 1 }
