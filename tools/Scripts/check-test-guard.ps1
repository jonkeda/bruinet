#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fails when a change takes tests away: deleted test files, fewer tests, skipped tests or scenarios,
    fewer assertions (AD-033).

.DESCRIPTION
    Compares the working tree (or -Head) with -Base and reads only the test files: C# files under a
    tests folder or a *Tests* project, and *.feature.md scenarios. A change that removes tests is not
    always wrong, but it must say why: pass -Reason and the findings become notes.

    Also notes, without failing, assertions rewritten in a change that also edits non-test code: the
    pattern of a test bent to fit the code. A reviewer reads those lines first.

.PARAMETER Base
    The revision to compare with. Default: HEAD, so uncommitted work is checked.

.PARAMETER Head
    A revision to compare instead of the working tree (CI: the pull request's head).

.PARAMETER Reason
    Why tests went away; the findings are printed as notes and the script passes.

.EXAMPLE
    ./tools/Scripts/check-test-guard.ps1
    ./tools/Scripts/check-test-guard.ps1 -Base origin/move -Head HEAD
#>
param(
    [string] $Base = 'HEAD',
    [string] $Head,
    [string] $Reason
)

$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))

[string[]] $range = if ($Head) { $Base, $Head } else { $Base }
$isTest = { param($path) $path -match '(^|/)tests/|Tests?(\.[A-Za-z]+)*/' -or $path -like '*.feature.md' }

$status = git diff -M --name-status @range | ForEach-Object {
    $parts = $_ -split "`t"
    [pscustomobject]@{ Status = $parts[0].Substring(0, 1); Path = $parts[-1]; Old = $parts[1] }
}
$codeChanged = @($status | Where-Object { $_.Path -like '*.cs' -and -not (& $isTest $_.Path) }).Count -gt 0

$failures = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()

foreach ($file in $status | Where-Object { & $isTest $_.Path }) {
    if ($file.Status -eq 'D') {
        $failures.Add("$($file.Path): deleted")
        continue
    }
    if ($file.Path -notlike '*.cs' -and $file.Path -notlike '*.feature.md') { continue }

    $lines = git diff -M -U0 -w @range -- $file.Path
    $removed = @($lines | Where-Object { $_ -match '^-(?!--)' })
    $added = @($lines | Where-Object { $_ -match '^\+(?!\+\+)' })
    $count = { param($set, $pattern) @($set | Where-Object { $_ -match $pattern }).Count }

    if ($file.Path -like '*.feature.md') {
        $fewer = (& $count $removed '^-\s*##\s+Scenario') - (& $count $added '^\+\s*##\s+Scenario')
        if ($fewer -gt 0) { $failures.Add("$($file.Path): $fewer scenario(s) fewer") }
        $skips = & $count $added '@(skip|ignore|wip)\b'
        if ($skips -gt 0) { $failures.Add("$($file.Path): $skips scenario tag(s) that skip") }
        continue
    }

    $test = '\[(Fact|Theory|Test|TestMethod|TestCase|BddScenario)\b'
    $fewer = (& $count $removed $test) - (& $count $added $test)
    if ($fewer -gt 0) { $failures.Add("$($file.Path): $fewer test(s) fewer") }

    $skips = & $count $added 'Skip\s*=|\[Ignore|Assert\.Skip|\[Skip'
    if ($skips -gt 0) { $failures.Add("$($file.Path): $skips skip(s) added") }

    # xUnit's Assert.X(...) and a control's .AssertText(...) alike.
    $assert = '\bAssert\.\w+|\.Assert\w*\('
    $assertsRemoved = & $count $removed $assert
    $assertsFewer = $assertsRemoved - (& $count $added $assert)
    if ($assertsFewer -gt 0) {
        $failures.Add("$($file.Path): $assertsFewer assertion(s) fewer")
    }
    elseif ($assertsRemoved -gt 0 -and $codeChanged) {
        $notes.Add("$($file.Path): $assertsRemoved assertion(s) rewritten beside a code change; read them first")
    }
}

foreach ($note in $notes) { Write-Host "NOTE $note" }
if ($failures.Count -eq 0) {
    Write-Host 'Test guard: no tests taken away.'
    exit 0
}
if ($Reason) {
    foreach ($failure in $failures) { Write-Host "NOTE $failure" }
    Write-Host "Test guard: tests taken away, because: $Reason"
    exit 0
}
foreach ($failure in $failures) { Write-Host "FAIL $failure" }
Write-Host 'Test guard: tests were taken away. Restore them, or run again with -Reason "<why>".'
exit 1
