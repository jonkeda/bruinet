#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Rewrites the docs' marked code samples from the regions the build compiles.

.DESCRIPTION
    A doc's block after a "<!-- sample: <file>#<name> -->" line is replaced by that file's "#region docs:<name>",
    as update-public-api.ps1 does for the API files (review 06, proposal 4). DocsSamplesTests in Bruinet.Core.Tests
    fails while a block and its region differ.

.EXAMPLE
    ./tools/Scripts/update-doc-samples.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$marker = '<!-- sample: (?<file>[^#\s]+)#(?<name>[\w-]+) -->\n```csharp\n(?<code>.*?)```'
$changed = 0
Get-ChildItem (Join-Path $root 'docs') -Recurse -Filter *.md | ForEach-Object {
    $path = $_.FullName
    $text = (Get-Content -Raw $path) -replace "`r`n", "`n"
    $new = [regex]::Replace($text, $marker, {
        param($m)
        $source = Join-Path $root $m.Groups['file'].Value
        $code = (Get-Content -Raw $source) -replace "`r`n", "`n"
        $start = $code.IndexOf("#region docs:$($m.Groups['name'].Value)`n")
        if ($start -lt 0) { throw "$($m.Groups['file'].Value) has no #region docs:$($m.Groups['name'].Value)" }
        $start = $code.IndexOf("`n", $start) + 1
        $end = $code.IndexOf('#endregion', $start)
        $region = $code.Substring($start, $end - $start).Trim("`n")
        "<!-- sample: $($m.Groups['file'].Value)#$($m.Groups['name'].Value) -->`n``````csharp`n$region`n``````"
    }, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($new -ne $text) {
        Set-Content -NoNewline -Path $path -Value ($new -replace "`n", "`r`n")
        $changed++
        Write-Host "Rewrote $($_.Name)"
    }
}
Write-Host "$changed doc(s) rewritten."
