# Checks that the packages pack.ps1 made can be installed: every Bruinet package a packed one
# depends on is there, and a consumer restores and builds every library from the folder alone.
# Working directory: Bruinet root.
#
#   ./tools/Scripts/check-packages.ps1 -Packages artifacts -Version 0.2.0
#
# One consumer project per target framework the libraries need (net10.0, the Windows ones,
# net48), each naming every library whose best framework that is. Bruinet's packages come only
# from -Packages, everything else from nuget.org, into a package cache of its own, so neither a
# package from an earlier pack nor this repository's build settings can make it pass.

param(
    [Parameter(Mandatory)] [string] $Packages,
    [Parameter(Mandatory)] [string] $Version,
    [string] $Work = (Join-Path ([IO.Path]::GetTempPath()) "bruinet-check-packages-$PID")
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Packages = (Resolve-Path $Packages).Path
$suffix = ".$Version.nupkg"

function Read-Package([string] $path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' } | Select-Object -First 1
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $frameworks = @($zip.Entries |
            Where-Object { $_.FullName -match '^(lib|ref)/([^/]+)/' } |
            ForEach-Object { ($_.FullName -split '/')[1] } |
            Sort-Object -Unique)
    }
    finally { $zip.Dispose() }

    [pscustomobject] @{
        Id         = $nuspec.package.metadata.id
        IsTool     = [bool] $nuspec.SelectSingleNode("//*[local-name()='packageType'][@name='DotnetTool']")
        Depends    = @($nuspec.SelectNodes("//*[local-name()='dependency']") | ForEach-Object { $_.GetAttribute('id') } | Sort-Object -Unique)
        Frameworks = $frameworks
    }
}

# The framework a consumer of the package targets: plain net10.0 where the package has it.
function Select-Framework($package) {
    # A package's folder names the Windows version it was built for: net10.0-windows7.0 is plain
    # net10.0-windows, net10.0-windows10.0.19041 needs that version in the consumer too.
    $frameworks = $package.Frameworks
    if ($frameworks -contains 'net10.0') { return 'net10.0' }
    $windows = $frameworks | Where-Object { $_ -match '^net10\.0-windows' } | Sort-Object | Select-Object -First 1
    if ($windows -eq 'net10.0-windows7.0') { return 'net10.0-windows' }
    if ($windows -match '^net10\.0-windows(10\.0\.\d+)(\.0)?$') { return "net10.0-windows$($Matches[1]).0" }
    if ($frameworks -contains 'netstandard2.0') { return 'net10.0' }
    if ($frameworks -contains 'net48') { return 'net48' }
    throw "$($package.Id) has no framework this check knows: $($package.Frameworks -join ', ')"
}

$found = @(Get-ChildItem $Packages -Filter "*$suffix" | ForEach-Object { Read-Package $_.FullName })
if ($found.Count -eq 0) { throw "No packages at version $Version in $Packages" }
$ids = @($found | ForEach-Object Id)

# 1. Every Bruinet dependency is in the folder.
$missing = @(foreach ($package in $found) {
    foreach ($name in $package.Depends) {
        if (($name -like 'Bruinet.*' -or $name -eq 'MauiHeadless') -and $ids -notcontains $name) { "$($package.Id) needs $name" }
    }
})
if ($missing.Count -gt 0) {
    throw "Packages depend on Bruinet packages that were not packed:`n  $($missing -join "`n  ")"
}

# 2. A consumer per framework restores and builds them.
$libraries = @($found | Where-Object { -not $_.IsTool })
$groups = $libraries | Group-Object { Select-Framework $_ } | Sort-Object Name

if (Test-Path $Work) { Remove-Item $Work -Recurse -Force }
New-Item -ItemType Directory $Work | Out-Null

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="bruinet" value="$Packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="bruinet">
      <package pattern="Bruinet.*" />
      <package pattern="MauiHeadless" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $Work 'nuget.config')

# Stops the consumers picking up a Directory.Build.props or Directory.Packages.props above them.
'<Project />' | Set-Content (Join-Path $Work 'Directory.Build.props')
'<Project />' | Set-Content (Join-Path $Work 'Directory.Build.targets')
'<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>' |
    Set-Content (Join-Path $Work 'Directory.Packages.props')

$previousCache = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $Work 'packages'
$failed = @()
try {
    foreach ($group in $groups) {
        $framework = $group.Name
        $name = 'Consumer.' + ($framework -replace '[^A-Za-z0-9]', '_')
        $folder = Join-Path $Work $name
        New-Item -ItemType Directory $folder | Out-Null

        $references = ($group.Group | Sort-Object Id | ForEach-Object { "    <PackageReference Include=`"$($_.Id)`" Version=`"$Version`" />" }) -join "`n"
        $framework48 = if ($framework -eq 'net48') { "    <PackageReference Include=`"Microsoft.NETFramework.ReferenceAssemblies`" Version=`"1.0.3`" PrivateAssets=`"all`" />" } else { '' }
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
$references
$framework48
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $folder "$name.csproj")
        'namespace Consumer; public static class Empty { }' | Set-Content (Join-Path $folder 'Empty.cs')

        Write-Host "== $framework : $(($group.Group | ForEach-Object Id | Sort-Object) -join ', ')"
        dotnet build (Join-Path $folder "$name.csproj") -v:minimal /nr:false
        if ($LASTEXITCODE -ne 0) { $failed += $framework }
    }
}
finally {
    $env:NUGET_PACKAGES = $previousCache
}

if ($failed.Count -gt 0) {
    throw "A consumer could not restore or build the packages for: $($failed -join ', '). The folder is kept: $Work"
}

Remove-Item $Work -Recurse -Force
Write-Host "All $($libraries.Count) libraries restore and build from $Packages at $Version."
