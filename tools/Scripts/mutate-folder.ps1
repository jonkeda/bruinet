#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Mutation testing with Stryker.NET of one source folder of a project Stryker cannot compile.

.DESCRIPTION
    Stryker cannot compile some projects itself, such as a multi-target MAUI app: its analysis and
    its own compile without the XAML generator inputs both fail. This script builds a harness in
    %TEMP% instead: a plain net10.0 library that links the folder's source files, and a test project
    that links the folder's unit tests. Tests that use the app's code outside the folder (any
    namespace starting with -AppNamespacePrefix other than -Namespace) cannot compile there, so they
    are left out and listed.

    A mutant that survives is a change no test noticed: a missing test, or code no test needs. For
    a project Stryker can compile, use mutate.ps1's approach (Stryker on the project) instead.

.PARAMETER Root
    The repository root. Paths below are relative to it.

.PARAMETER Source
    The folder to mutate, for example MyApp/Common/Formatters.

.PARAMETER Tests
    The folder of its unit tests, for example MyApp.Tests/Common/Formatters.

.PARAMETER Namespace
    The folder's namespace.

.PARAMETER AppNamespacePrefix
    The prefix of the app's own namespaces, for example 'MyApp.'.

.PARAMETER Dependency
    Files outside the folder that its code needs: hashtables @{ File = '<path>'; Namespace = '<ns>' },
    linked into the library with the namespace as a global using.

.PARAMETER TestPackage
    The harness test project's packages: hashtables @{ Name = '<id>'; Version = '<version>' }.
    Default: xUnit 2.9.3 with its runner and the test SDK.

.PARAMETER ToolManifest
    The dotnet-tools.json that holds dotnet-stryker. Default: <Root>/dotnet-tools.json, then
    <Root>/.config/dotnet-tools.json.

.PARAMETER Output
    Where Stryker writes its reports. Default: <Root>/TestResults/mutation/<last folder of Source>.

.PARAMETER Mutate
    Only these files, as globs under the folder, for example 'Parser.cs'. Default: every file.

.PARAMETER Break
    The lowest mutation score, in percent, that passes. Default: 0, a report only.

.EXAMPLE
    ./Brinell/tools/Scripts/mutate-folder.ps1 -Root . -Source MyApp/Common/Formatters `
        -Tests MyApp.Tests/Common/Formatters -Namespace MyApp.Common.Formatters -AppNamespacePrefix 'MyApp.'
#>
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $Tests,
    [Parameter(Mandatory)] [string] $Namespace,
    [Parameter(Mandatory)] [string] $AppNamespacePrefix,
    [hashtable[]] $Dependency = @(),
    [hashtable[]] $TestPackage = @(
        @{ Name = 'Microsoft.NET.Test.Sdk'; Version = '18.10.1' },
        @{ Name = 'xunit'; Version = '2.9.3' },
        @{ Name = 'xunit.runner.visualstudio'; Version = '4.0.0' }
    ),
    [string] $ToolManifest,
    [string] $Output,
    [string[]] $Mutate = @(),
    [int] $Break = 0
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path $Root).Path
$sourcePath = Join-Path $Root $Source
$testsPath = Join-Path $Root $Tests
if (-not (Test-Path $sourcePath)) { throw "No source folder $sourcePath." }
if (-not (Test-Path $testsPath)) { throw "No unit tests in $testsPath." }
$area = Split-Path $sourcePath -Leaf
if (-not $Output) { $Output = Join-Path $Root "TestResults/mutation/$area" }
if (-not $ToolManifest) {
    $ToolManifest = @((Join-Path $Root 'dotnet-tools.json'), (Join-Path $Root '.config/dotnet-tools.json')) |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $ToolManifest) { throw "No dotnet-tools.json with dotnet-stryker in $Root." }
}

$extraCompile = ''
$extraUsings = ''
foreach ($item in $Dependency) {
    $extraCompile += "`n    <Compile Include=`"$(Join-Path $Root $item.File)`" LinkBase=`"Dependencies`" />"
    $extraUsings += "`n    <Using Include=`"$($item.Namespace)`" />"
}

$prefixPattern = '^using (' + [regex]::Escape($AppNamespacePrefix) + '[\w.]+);'
$testFiles = @()
$skipped = @()
foreach ($file in Get-ChildItem $testsPath -Recurse -Filter *.cs) {
    $outside = Select-String $file.FullName -Pattern $prefixPattern |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Where-Object { $_ -ne $Namespace -and -not $_.StartsWith("$Namespace.") }
    if ($outside) { $skipped += $file.Name } else { $testFiles += $file.FullName }
}
if (-not $testFiles) { throw "No test in $testsPath compiles without code outside the folder." }

$harness = Join-Path ([IO.Path]::GetTempPath()) "bruinet-mutation/$area"
Remove-Item $harness -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $harness 'src'), (Join-Path $harness 'tests') | Out-Null

# Stop the harness from inheriting the repository's build files.
'<Project />' | Set-Content (Join-Path $harness 'Directory.Build.props')
'<Project />' | Set-Content (Join-Path $harness 'Directory.Build.targets')
'<Project />' | Set-Content (Join-Path $harness 'Directory.Packages.props')

$library = "Harness.$area"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>$Namespace</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$sourcePath/**/*.cs" LinkBase="$area" />$extraCompile$extraUsings
    <InternalsVisibleTo Include="$library.Tests" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $harness "src/$library.csproj")

$packages = ($TestPackage | ForEach-Object { "    <PackageReference Include=`"$($_.Name)`" Version=`"$($_.Version)`" />" }) -join "`n"
$compileTests = ($testFiles | ForEach-Object { "    <Compile Include=`"$_`" />" }) -join "`n"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
$packages
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
$compileTests
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../src/$library.csproj" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $harness "tests/$library.Tests.csproj")

Write-Host "== ${area}: $($testFiles.Count) test files$(if ($skipped) { "; left out, they need code outside the folder: $($skipped -join ', ')" })"
dotnet test (Join-Path $harness "tests/$library.Tests.csproj") -v q --nologo
if ($LASTEXITCODE -ne 0) { throw 'The harness tests fail without mutations; fix that first.' }

$relative = [IO.Path]::GetRelativePath($Root, $sourcePath) -replace '\\', '/'
$arguments = @('--test-project', (Join-Path $harness "tests/$library.Tests.csproj"), '--project', "$library.csproj",
    '--reporter', 'html', '--reporter', 'json', '--reporter', 'progress',
    '--break-at', $Break, '--output', $Output)
# Only the folder's own files, not the linked dependencies.
foreach ($glob in $(if ($Mutate) { $Mutate } else { @('**/*.cs') })) { $arguments += '--mutate', "**/$relative/$glob" }

# Run inside the harness: from the repository root Stryker would analyse the repository's solution.
Copy-Item $ToolManifest (Join-Path $harness 'dotnet-tools.json') -Force
Push-Location (Join-Path $harness 'tests')
try {
    dotnet tool restore | Out-Null
    dotnet tool run dotnet-stryker -- @arguments 2>&1 | Write-Host
    $exit = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exit -ne 0) {
    Write-Error "Mutation score of $area under $Break%. The surviving mutants are in $Output."
    exit 1
}
Write-Host "Mutation testing of ${area}: done. Reports in $Output."
