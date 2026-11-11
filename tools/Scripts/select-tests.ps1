#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Picks the tests and scenarios of the Html and Blazor UI suites a change reaches, with why, as one dotnet test --filter
    (`bruinet tests affected`, .my/test-selection/next-plan.md item 3); -Run runs them.

.DESCRIPTION
    For each suite with a touch-map.json (tests/Bruinet.Blazor.UITests, tests/Bruinet.Html.UITests): a sample page
    reaches the tests that used its test ids or open its route, a page object the tests that used it, a feature file its
    scenarios, a test class its tests. A change to a library the suite references (src/), the suite's test base or
    fixture, the sample's layout or start, or the root build files runs the suite whole; anything else runs nothing.

    bruinet is built from tools/Bruinet.Cli into a temp folder, without the recorders: the agents' MCP servers keep
    tools/Bruinet.Cli/bin open.

.PARAMETER Base
    Changed since this commit, the working tree and new files included. Default: HEAD.

.PARAMETER Files
    These changed files, relative to the Bruinet root, instead of git's.

.PARAMETER Suite
    Blazor, Html or both (default).

.PARAMETER Run
    Run what is picked, on -Head.

.PARAMETER WriteMap
    Run the suite whole with the timings on and write its touch-map.json from them; commit the file.

.PARAMETER Head
    bunit (default, about a second) or playwright (the browser, which judges).

.EXAMPLE
    pwsh tools/Scripts/select-tests.ps1
    pwsh tools/Scripts/select-tests.ps1 -Run; pwsh tools/Scripts/select-tests.ps1 -Run -Head playwright
    pwsh tools/Scripts/select-tests.ps1 -Suite Html -WriteMap -Head playwright
#>
param(
    [string]$Base = 'HEAD',
    [string[]]$Files,
    [ValidateSet('Blazor', 'Html', 'both')] [string]$Suite = 'both',
    [switch]$Run,
    [switch]$WriteMap,
    [ValidateSet('bunit', 'playwright')] [string]$Head = 'bunit'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$cli = Join-Path ([IO.Path]::GetTempPath()) "bruinet-select-cli/$(Split-Path $root -Leaf)"
dotnet build (Join-Path $root 'tools/Bruinet.Cli') --artifacts-path $cli -p:BruinetRecorders=false --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'bruinet did not build.' }
$bruinet = Join-Path $cli 'bin/Bruinet.Cli/debug/bruinet.exe'
$names = if ($Suite -eq 'both') { 'Blazor', 'Html' } else { , $Suite }

function Test-Suite([string]$project, [string[]]$arguments) {
    $env:BRUINET_HEAD = $Head
    try { dotnet test $project @arguments } finally { Remove-Item Env:BRUINET_HEAD }
}

foreach ($name in $names) {
    $project = Join-Path $root "tests/Bruinet.$name.UITests"
    Write-Host "== $name"
    if ($WriteMap) {
        $timings = Join-Path ([IO.Path]::GetTempPath()) "bruinet-timings-$name-$(Get-Date -Format yyyyMMdd-HHmmss)"
        $env:BRUINET_TIMINGS = $timings
        try { Test-Suite $project @() } finally { Remove-Item Env:BRUINET_TIMINGS }
        & $bruinet tests touch-map $timings --project $project
        if ($LASTEXITCODE -ne 0) { throw "bruinet tests touch-map failed for $name." }
        continue
    }

    $arguments = @('tests', 'affected', '--project', $project, '--json')
    $arguments += if ($Files) { @('--files') + ($Files | ForEach-Object { Join-Path $root $_ }) } else { @('--base', $Base) }
    Push-Location $root
    try { $json = & $bruinet @arguments } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "bruinet tests affected failed for $name." }
    $affected = ($json -join "`n") | ConvertFrom-Json
    $affected.notes | ForEach-Object { Write-Host "  $_" }
    if ($affected.whole) { Write-Host "The whole suite: $($affected.whole)." }
    elseif ($affected.filter) {
        $affected.picked | ForEach-Object { Write-Host "$($_.name): $($_.why)" }
        Write-Host "--filter `"$($affected.filter)`""
    }
    else { Write-Host "Nothing to run: $($affected.changed) changed files reach no test of the suite." }
    if (-not $Run) { continue }
    if ($affected.whole) { Test-Suite $project @() }
    elseif ($affected.filter) { Test-Suite $project @('--filter', $affected.filter) }
}
