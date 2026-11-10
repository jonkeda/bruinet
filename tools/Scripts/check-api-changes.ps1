#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fails when a change breaks the public API and the consumers' change list is untouched
    (skill api-upgrade, review proposal 13).

.DESCRIPTION
    Compares the working tree (or -Head) with -Base and reads every src/*/PublicAPI.Unshipped.txt.
    A change breaks a consumer when it:
      - removes or alters a line (an altered line shows as removed and added), or
      - adds a member to an interface, or an abstract member, to a type that was already public.
    When any such line is found and .github/skills/api-upgrade/references/changes.md is not changed
    in the same range, the check fails and lists the lines. A line moved within one file is not a
    change. An interface member added with a default body breaks no one, but the check cannot tell:
    give it a row that says so.

.PARAMETER Base
    The revision to compare with. Default: HEAD, so uncommitted work is checked.

.PARAMETER Head
    A revision to compare instead of the working tree (CI: the pull request's head).

.EXAMPLE
    ./tools/Scripts/check-api-changes.ps1
    ./tools/Scripts/check-api-changes.ps1 -Base HEAD~1 -Head HEAD
#>
param(
    [string] $Base = 'HEAD',
    [string] $Head
)

$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))

$changeList = '.github/skills/api-upgrade/references/changes.md'
[string[]] $range = if ($Head) { $Base, $Head } else { $Base }

$files = @(git diff --name-only @range -- 'src/*/PublicAPI.Unshipped.txt')
if ($files.Count -eq 0) {
    Write-Host 'API change list: no public API file changed.'
    exit 0
}

function Read-Lines([string] $rev, [string] $path) {
    if ($rev) {
        $text = git show "${rev}:$path" 2>$null
        if ($LASTEXITCODE -ne 0) { return @() }
        return @($text)
    }
    if (Test-Path $path) { return @(Get-Content $path) }
    return @()
}

# A type's own line: no signature arrow and no parameter list.
function Get-DeclaredType([string] $line) {
    if ($line.Contains(' -> ') -or $line.Contains('(')) { return $null }
    $name = $line -replace '^(abstract |static |sealed |virtual )+', ''
    return ($name -replace '<[^<>]*>', '' -replace '<[^<>]*>', '')
}

# The type a member line belongs to: the part before the member's name, without generic arguments.
function Get-OwningType([string] $line) {
    $signature = ($line -replace '^(abstract |static |virtual |override |const |readonly |sealed )+', '') -split ' -> ' | Select-Object -First 1
    $signature = ($signature -split '\(')[0]
    $signature = $signature -replace '<[^<>]*>', '' -replace '<[^<>]*>', ''
    $signature = $signature -replace '\.(get|set|init|add|remove)$', ''
    $dot = $signature.LastIndexOf('.')
    if ($dot -lt 0) { return $null }
    return $signature.Substring(0, $dot)
}

$breaks = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $old = Read-Lines $Base $file | Where-Object { $_ -and -not $_.StartsWith('#') }
    $new = Read-Lines $Head $file | Where-Object { $_ -and -not $_.StartsWith('#') }
    $oldSet = [System.Collections.Generic.HashSet[string]]::new([string[]] @($old))
    $newSet = [System.Collections.Generic.HashSet[string]]::new([string[]] @($new))

    foreach ($line in $old) {
        if (-not $newSet.Contains($line)) { $breaks.Add("$file`: removed  $line") }
    }

    # Types that were public before: an added member on one of them is what can break an implementer.
    $oldTypes = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($line in $old) {
        $type = Get-DeclaredType $line
        if ($type) { [void] $oldTypes.Add($type) }
    }
    foreach ($line in $new) {
        if ($oldSet.Contains($line)) { continue }
        $type = Get-OwningType $line
        if (-not $type -or -not $oldTypes.Contains($type)) { continue }
        $simple = ($type -split '\.')[-1]
        if ($line.StartsWith('abstract ') -or $simple -cmatch '^I[A-Z]') {
            $breaks.Add("$file`: added    $line")
        }
    }
}

if ($breaks.Count -eq 0) {
    Write-Host 'API change list: only new types or class members; no row needed.'
    exit 0
}

$listed = @(git diff --name-only @range -- $changeList).Count -gt 0
if ($listed) {
    Write-Host "API change list: $($breaks.Count) breaking line(s), and $changeList is changed with them."
    exit 0
}
foreach ($break in $breaks) { Write-Host "FAIL $break" }
Write-Host "API change list: add a row for these to $changeList (skill api-upgrade)."
exit 1
