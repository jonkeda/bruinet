#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Brings a project's PublicAPI.Unshipped.txt up to date with its code (AD-033).

.DESCRIPTION
    Builds the project once per target framework with an error log, adds every public member the
    build reports as undeclared (RS0016) and removes every declared one it cannot find (RS0017).
    Unlike the IDE's code fix and dotnet format, it also covers the members of generated .gen.cs
    files. Read the diff of PublicAPI.Unshipped.txt before committing it: it is the API change.

.PARAMETER Project
    The project file, e.g. src/Bruinet.Maui/Bruinet.Maui.csproj.

.EXAMPLE
    ./tools/Scripts/update-public-api.ps1 src/Bruinet.Maui/Bruinet.Maui.csproj
#>
param(
    [Parameter(Mandatory)] [string] $Project
)

$ErrorActionPreference = 'Stop'
$Project = Resolve-Path $Project
$folder = Split-Path $Project
$unshipped = Join-Path $folder 'PublicAPI.Unshipped.txt'
$frameworks = (dotnet msbuild $Project -getProperty:TargetFrameworks).Trim()
if (-not $frameworks) { $frameworks = (dotnet msbuild $Project -getProperty:TargetFramework).Trim() }

$added = [System.Collections.Generic.HashSet[string]]::new()
$removed = [System.Collections.Generic.HashSet[string]]::new()
foreach ($framework in $frameworks -split ';' | Where-Object { $_ }) {
    $log = Join-Path ([System.IO.Path]::GetTempPath()) "public-api-$framework.sarif"
    dotnet build $Project -f $framework -v:q /nr:false -p:TreatWarningsAsErrors=false "-p:ErrorLog=$log%2Cversion=2.1" | Out-Null
    $results = (Get-Content $log -Raw | ConvertFrom-Json).runs.results
    foreach ($result in $results | Where-Object ruleId -eq 'RS0016') {
        [void] $added.Add($result.properties.customProperties.APIName)
    }
    foreach ($result in $results | Where-Object ruleId -eq 'RS0017') {
        if ($result.message -match "Symbol '(.+)' is part of the declared API") { [void] $removed.Add($Matches[1]) }
    }
    Remove-Item $log
}

$lines = Get-Content $unshipped | Where-Object { -not $removed.Contains($_) }
$header = @($lines | Where-Object { $_ -like '#*' })
$body = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in @($lines | Where-Object { $_ -and $_ -notlike '#*' }) + @($added)) { [void] $body.Add($line) }
Set-Content $unshipped -Value ($header + @($body))
Write-Host "PublicAPI.Unshipped.txt: $($added.Count) added, $($removed.Count) removed."
