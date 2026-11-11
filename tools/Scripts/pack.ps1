# Packs every packable project under src/, and the bruinet tool.
# Working directory: Bruinet root.
#
#   ./tools/Scripts/pack.ps1 -Version 0.2.0 -Output artifacts
#
# A project is packed unless it says IsPackable=false (the Scraper, the workspace, the analyzers,
# the Presenter app), so a new library is a package without a list to keep. Packs project by
# project instead of the solution: the solution also holds sample apps, which are not packages.
# Whether a consumer can restore and build what it made is check-packages.ps1.

param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Output = 'artifacts'
)

$ErrorActionPreference = 'Stop'

$projects = Get-ChildItem src -Recurse -Filter *.csproj | Sort-Object FullName
$packed = @()
foreach ($project in $projects) {
    $isPackable = dotnet msbuild $project.FullName -getProperty:IsPackable
    if ($LASTEXITCODE -ne 0) { throw "Could not read IsPackable: $($project.Name)" }
    if ("$isPackable".Trim() -ne 'true') {
        Write-Host "Not a package: $($project.BaseName)"
        continue
    }

    dotnet pack $project.FullName -c Release -o $Output -v:minimal /nr:false "-p:Version=$Version" -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $($project.BaseName)" }
    $packed += $project.BaseName
}

# bruinet, the command line (AD-028): a dotnet tool, versioned with the libraries.
dotnet pack 'tools/Bruinet.Cli/Bruinet.Cli.csproj' -c Release -o $Output -v:minimal /nr:false "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Pack failed: Bruinet.Cli' }

Write-Host "Packed $($packed.Count) libraries and Bruinet.Cli at $Version into $Output."
