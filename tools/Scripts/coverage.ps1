#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The tests with code coverage, and one report over them (AD-033).

.DESCRIPTION
    Runs each unit test project below with coverlet's collector (coverage.runsettings: Bruinet's
    libraries only, generated code left out), writes a .trx per project, then merges the coverage
    with ReportGenerator (a local tool, .config/dotnet-tools.json) into <Results>/report: index.html,
    Summary.txt and SummaryGithub.md. CI runs it with -Headless -Browser as its test step.

    The switches add the UI suites, which run the drivers and controls in the test process, so
    their coverage counts for Bruinet.Maui, Bruinet.Html, Bruinet.Blazor and the rest: the unit
    tests alone leave most of the controls unrun.

    A report, not a gate: a line run is not a line proven. Mutation testing (mutate.ps1) says whether
    the tests would notice; coverage shows the code no test runs at all. Fails when a test fails,
    after the report is written.

.PARAMETER Projects
    The test projects under tests/. Default: every unit test project. Left out: the *.UITests
    suites (the switches below) and Presenter.Bdd.Tests (drives the Presenter app; its cloud voice
    scenario reads this machine's voice.secrets.json).

.PARAMETER Headless
    Also the MAUI suites on MauiHeadless (test-maui-headless.ps1's list): no app, no window.

.PARAMETER Browser
    Also Chromium: the Category=Browser tests and the Html and Blazor UI suites, headless. Needs
    Playwright's Chromium (tests/Bruinet.Html.UITests/bin/Debug/net10.0/playwright.ps1 install chromium).

.PARAMETER Desktop
    Also the Wpf and WinForms UI suites: real windows on this desktop, so local only.

.PARAMETER Results
    Where the .trx files, the coverage files and the report go. Default: TestResults/coverage.

.PARAMETER NoBuild
    Test what is already built.

.EXAMPLE
    ./tools/Scripts/coverage.ps1 -Headless -Browser
    ./tools/Scripts/coverage.ps1 -Projects Bruinet.Core.Tests, Bruinet.Bdd.Tests
#>
param(
    [string[]] $Projects = @(
        'Bruinet.Core.Tests',
        'Bruinet.Maui.Tests',
        'Bruinet.FlaUI.Tests',
        'Bruinet.Wpf.Tests',
        'Bruinet.WinForms.Tests',
        'Bruinet.Blazor.Tests',
        'Bruinet.Html.Tests',
        'Bruinet.Stride.Tests',
        'Bruinet.Stride.Automation.Tests',
        'Bruinet.Mocking.Tests',
        'Bruinet.Uat.Tests',
        'Bruinet.TestData.Tests',
        'Bruinet.Appium.Tests',
        'Bruinet.NativeAndroid.Tests',
        'Bruinet.Cli.Tests',
        'Bruinet.Bdd.Tests',
        'Bruinet.Bdd.Workspace.Tests',
        'Bruinet.Html.Bunit.Tests',
        'Bruinet.Maui.Headless.Tests',
        'Bruinet.Mocking.Data.Tests',
        'Bruinet.Mocking.Data.Hook.Tests',
        'Bruinet.Mocking.Data.Net48.Tests',
        'Bruinet.Narrator.Tests',
        'Bruinet.Recording.Tests',
        'Bruinet.Scraper.Tests',
        'Bruinet.Uia.Tests'
    ),
    [switch] $Headless,
    [switch] $Browser,
    [switch] $Desktop,
    [string] $Results = 'TestResults/coverage',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $root

if ($Browser) { $Projects += 'Bruinet.Html.UITests', 'Bruinet.Blazor.UITests' }
if ($Desktop) { $Projects += 'Bruinet.Wpf.UITests', 'Bruinet.WinForms.UITests' }

# A rerun starts clean: old coverage files would be merged into the new report.
Get-ChildItem $Results -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue | Remove-Item

$failed = @()
foreach ($p in $Projects) {
    $arguments = @("tests/$p/$p.csproj", '-v:minimal',
        '--collect', 'XPlat Code Coverage', '--settings', 'coverage.runsettings',
        '--logger', "trx;LogFileName=$p.trx", '--results-directory', $Results)
    # Category=Browser drives Chromium against a local site.
    if (-not $Browser) { $arguments += '--filter', 'Category!=Browser' }
    if ($NoBuild) { $arguments += '--no-build' }
    dotnet test @arguments
    if ($LASTEXITCODE -ne 0) { $failed += $p }
}

if ($Headless) {
    # Its own process: it sets BRUINET_HEAD and fails with Write-Error, which would stop this script
    # before the report.
    $arguments = @('-NoProfile', '-File', './tools/Scripts/test-maui-headless.ps1', '-Coverage', '-Results', $Results)
    if ($NoBuild) { $arguments += '-NoBuild' }
    pwsh @arguments
    if ($LASTEXITCODE -ne 0) { $failed += 'the MauiHeadless suites' }
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
