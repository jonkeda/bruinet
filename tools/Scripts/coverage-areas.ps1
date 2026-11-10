#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Line coverage per source area (a module, feature or folder) from one Cobertura file.

.DESCRIPTION
    Coverage tools report per assembly and per class; an app's test plan is per area: a module folder
    such as MyApp/Modules/Orders. This reads a Cobertura file (coverlet, dotnet-coverage, or a merge
    of several) and writes one object per area: Area, LinesCovered, LinesValid, LineRate, Percent,
    Files. A line counts once per file, however many classes (async state machines, lambdas) list it.

    An area is the folder of the same name below -SourceRoot, ignoring case, unless -Rules maps it:
    a hashtable from the area's name (lower case, letters only) to @{ Include = '<regex>';
    Exclude = '<regex>' } on the path below -SourceRoot. That covers areas that span folders or share
    one.

.PARAMETER Cobertura
    The Cobertura XML file.

.PARAMETER SourceRoot
    The folder the areas live in, as a path suffix with forward slashes, for example
    'MyApp/Modules'. Paths in the report are matched on it wherever the repository was checked out.

.PARAMETER Area
    The areas to report.

.PARAMETER Rules
    Areas whose files are not simply the folder of the same name.

.PARAMETER SourceFolder
    The folder on disk for -SourceRoot, used to resolve an area name to its folder's spelling.
    Without it, an area without a rule matches its folder name case-insensitively in the report.

.EXAMPLE
    ./Brinell/tools/Scripts/coverage-areas.ps1 -Cobertura TestResults/coverage/coverage.cobertura.xml `
        -SourceRoot 'MyApp/Modules' -Area Orders, Customers -SourceFolder MyApp/Modules
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Cobertura,
    [Parameter(Mandatory)][string] $SourceRoot,
    [Parameter(Mandatory)][string[]] $Area,
    [hashtable] $Rules = @{},
    [string] $SourceFolder
)

$ErrorActionPreference = 'Stop'
$rootPattern = '(?:^|/)' + [regex]::Escape($SourceRoot.Trim('/')) + '/(?<rest>.+)$'

function Get-AreaRule([string] $Name) {
    $key = $Name.ToLowerInvariant() -replace '[^a-z]', ''
    if ($Rules.ContainsKey($key)) { return $Rules[$key] }
    if ($SourceFolder) {
        $folder = Get-ChildItem $SourceFolder -Directory | Where-Object { $_.Name.ToLowerInvariant() -eq $key } | Select-Object -First 1
        if (-not $folder) { throw "Unknown area '$Name': no rule and no folder of that name in $SourceFolder." }
        return @{ Include = '^' + [regex]::Escape($folder.Name) + '/' }
    }
    return @{ Include = '(?i)^' + [regex]::Escape($Name) + '/' }
}

# The path below the source root for a class's file name, or $null when it lies outside it.
function Get-RelativePath([string] $FileName, [string[]] $Sources) {
    $candidates = @($FileName) + @($Sources | ForEach-Object { ($_ -replace '[\\/]+$', '') + '/' + $FileName })
    foreach ($candidate in $candidates) {
        if (($candidate -replace '\\', '/') -match $rootPattern) { return $Matches['rest'] }
    }
    return $null
}

[xml] $report = Get-Content -LiteralPath $Cobertura -Raw
$sources = @($report.SelectNodes('//sources/source') | ForEach-Object { $_.InnerText })

$lines = @{}
foreach ($class in $report.SelectNodes('//class')) {
    $relative = Get-RelativePath $class.GetAttribute('filename') $sources
    if (-not $relative) { continue }
    if (-not $lines.ContainsKey($relative)) { $lines[$relative] = @{} }
    foreach ($line in $class.SelectNodes('lines/line')) {
        $number = $line.GetAttribute('number')
        $hit = [long]$line.GetAttribute('hits') -gt 0
        $lines[$relative][$number] = $hit -or [bool]$lines[$relative][$number]
    }
}

foreach ($name in $Area) {
    $rule = Get-AreaRule $name
    $exclude = if ($rule.ContainsKey('Exclude')) { $rule.Exclude } else { $null }
    $files = @($lines.Keys | Where-Object { $_ -match $rule.Include -and -not ($exclude -and $_ -match $exclude) })
    $valid = 0; $covered = 0
    foreach ($file in $files) {
        $valid += $lines[$file].Count
        $covered += @($lines[$file].Values | Where-Object { $_ }).Count
    }
    $rate = if ($valid) { $covered / $valid } else { $null }
    [pscustomobject]@{
        Area         = $name
        LinesCovered = $covered
        LinesValid   = $valid
        LineRate     = $rate
        Percent      = if ($null -ne $rate) { [math]::Round($rate * 100, 1) } else { $null }
        Files        = $files.Count
    }
}
