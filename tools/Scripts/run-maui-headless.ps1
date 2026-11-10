#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs an app's MAUI UI tests on Bruinet's in-memory MauiHeadless head.

.DESCRIPTION
    Builds the UI-test project for the plain net10.0 target and runs the selected tests with
    BRUINET_HEAD=headless. The app runs inside the test process, so no device, window or emulator
    is needed. Tests that need a device skip themselves. For Bruinet's own suites use
    test-maui-headless.ps1.

    Before the run it stops app processes an earlier run left (-AppProcess) and warns when the
    Bruinet submodule is missing or at another commit than the one recorded (-RepositoryRoot).

.PARAMETER Project
    The UI-test project (.csproj).

.PARAMETER Filter
    dotnet test filter. Default: none, every test.

.PARAMETER NoBuild
    Skip the build.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER ResultsDirectory
    When set, writes a TRX report to this folder.

.PARAMETER TrxName
    The TRX file name. Default: uitests-headless.trx.

.PARAMETER AppProcess
    Name patterns of app processes to stop first, for example 'MyApp*'.

.PARAMETER RepositoryRoot
    The app repository's root, to check its Bruinet submodule. Skipped when empty.

.PARAMETER SubmodulePath
    The Bruinet submodule's path below -RepositoryRoot. Default: Brinell.

.EXAMPLE
    ./Brinell/tools/Scripts/run-maui-headless.ps1 -Project MyApp.UITests/MyApp.UITests.csproj -Filter "FullyQualifiedName~Smoke"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [string]$Filter,
    [switch]$NoBuild,
    [string]$Configuration = 'Debug',
    [string]$ResultsDirectory,
    [string]$TrxName = 'uitests-headless.trx',
    [string[]]$AppProcess = @(),
    [string]$RepositoryRoot,
    [string]$SubmodulePath = 'Brinell'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'RunGuards.psm1') -Force

if ($AppProcess) { Stop-LeftoverApp -Name $AppProcess | Out-Null }
if ($RepositoryRoot) { Test-BruinetSubmodule -RepositoryRoot $RepositoryRoot -Path $SubmodulePath | Out-Null }

# A Windows test project defaults to the device TFM; the in-memory head needs the plain net10.0 target.
$tfmArgs = @('-f', 'net10.0', '-p:TargetFrameworks=net10.0', '-c', $Configuration)

if (-not $NoBuild) {
    dotnet build $Project @tfmArgs '-p:MauiXamlInflator=XamlC' -v:minimal /nr:false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$testArgs = @('--no-build', '--logger', 'console;verbosity=normal')
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($ResultsDirectory) { $testArgs += @('--logger', "trx;LogFileName=$TrxName", '--results-directory', $ResultsDirectory) }

$previousHead = $env:BRUINET_HEAD
$env:BRUINET_HEAD = 'headless'
try {
    dotnet test $Project @tfmArgs @testArgs
    exit $LASTEXITCODE
}
finally {
    $env:BRUINET_HEAD = $previousHead
}
