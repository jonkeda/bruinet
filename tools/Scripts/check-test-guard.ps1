#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fails when a change takes tests away: deleted test files, fewer test cases, skipped tests or
    scenarios, fewer assertions (AD-033).

.DESCRIPTION
    Compares the working tree (or -Head) with -Base and reads only the test files: C# files under a
    tests folder or a *Tests* project, and *.feature.md scenarios. A change that removes tests is not
    always wrong, but it must say why, in one of these ways, and the findings then become notes:
      - the -Reason parameter;
      - the TEST_GUARD_REASON environment variable (CI copies it from the pull request description);
      - a "Test-Guard-Reason: <why>" line in a commit message between -Base and -Head.

    Test cases are counted as test methods without data ([Fact], [Test], [TestMethod] and attributes
    ending in Fact, such as [BrowserFact]) plus data rows ([InlineData], [MemberData], [ClassData],
    [TestCase], [DataRow]). Merging facts into one theory is then not fewer tests, and the assertions
    it folds together are reported as a note.

    Also notes, without failing, assertions rewritten in a change that also edits non-test code: the
    pattern of a test bent to fit the code. A reviewer reads those lines first.

    An app's repository calls this script with -Root (its own root) and -Exclude (the Bruinet
    submodule), so it keeps no copy of its own.

.PARAMETER Base
    The revision to compare with. Default: HEAD, so uncommitted work is checked.

.PARAMETER Head
    A revision to compare instead of the working tree (CI: the pull request's head).

.PARAMETER Reason
    Why tests went away; the findings are printed as notes and the script passes.

.PARAMETER Root
    The repository to check. Default: the Bruinet root.

.PARAMETER Exclude
    Path patterns (-like, relative to -Root, forward slashes) to leave out, for example 'Brinell/*'.

.EXAMPLE
    ./tools/Scripts/check-test-guard.ps1
    ./tools/Scripts/check-test-guard.ps1 -Base origin/move -Head HEAD
    ./Brinell/tools/Scripts/check-test-guard.ps1 -Root . -Exclude 'Brinell/*' -Base origin/main -Head HEAD
#>
param(
    [string] $Base = 'HEAD',
    [string] $Head,
    [string] $Reason,
    [string] $Root = (Join-Path $PSScriptRoot '../..'),
    [string[]] $Exclude = @()
)

$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path $Root)

[string[]] $range = if ($Head) { $Base, $Head } else { $Base }
$isExcluded = { param($path) @($Exclude | Where-Object { $path -like $_ }).Count -gt 0 }
$isTest = { param($path) -not (& $isExcluded $path) -and ($path -match '(^|/)tests/|Tests?(\.[A-Za-z]+)*/' -or $path -like '*.feature.md') }

if (-not $Reason) { $Reason = $env:TEST_GUARD_REASON }
if (-not $Reason -and $Head) {
    $trailer = git log --format=%B "$Base..$Head" | Where-Object { $_ -match '^\s*Test-Guard-Reason:\s*(\S.*)$' } | Select-Object -First 1
    if ($trailer) { $Reason = ($trailer -replace '^\s*Test-Guard-Reason:\s*', '').Trim() }
}

$status = git diff -M --name-status @range | ForEach-Object {
    $parts = $_ -split "`t"
    [pscustomobject]@{ Status = $parts[0].Substring(0, 1); Path = $parts[-1]; Old = $parts[1] }
}
$codeChanged = @($status | Where-Object { $_.Path -like '*.cs' -and -not (& $isExcluded $_.Path) -and -not (& $isTest $_.Path) }).Count -gt 0

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

    $case = '\[(\w*Fact|Test|TestMethod|InlineData|MemberData|ClassData|TestCase|DataRow|BddScenario)\b'
    $fewer = (& $count $removed $case) - (& $count $added $case)
    if ($fewer -gt 0) { $failures.Add("$($file.Path): $fewer test case(s) fewer") }
    $merged = $fewer -le 0 -and (& $count $added '\[\w*Theory\b') -gt (& $count $removed '\[\w*Theory\b')

    $skips = & $count $added 'Skip\s*=|\[Ignore|Assert\.Skip|\[Skip'
    if ($skips -gt 0) { $failures.Add("$($file.Path): $skips skip(s) added") }

    # xUnit's Assert.X(...) and a control's .AssertText(...) alike.
    $assert = '\bAssert\.\w+|\.Assert\w*\('
    $assertsRemoved = & $count $removed $assert
    $assertsFewer = $assertsRemoved - (& $count $added $assert)
    if ($assertsFewer -gt 0 -and $merged) {
        $notes.Add("$($file.Path): facts merged into a theory; $assertsFewer assertion line(s) folded together")
    }
    elseif ($assertsFewer -gt 0) {
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
Write-Host 'Test guard: tests were taken away. Restore them, or give a reason: -Reason "<why>",'
Write-Host 'a "Test-Guard-Reason: <why>" line in a commit message, or the same line in the pull request description.'
exit 1
