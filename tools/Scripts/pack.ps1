# Packs the Bruinet library projects that are part of Bruinet.sln, and the bruinet tool.
# Working directory: Bruinet root.
#
#   ./tools/Scripts/pack.ps1 -Version 0.2.0 -Output artifacts
#
# Packs project by project instead of the solution: the solution also holds sample apps
# and the Presenter, which are not packages and do not build in Release as part of it.

param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Output = 'artifacts'
)

$ErrorActionPreference = 'Stop'

$libraries = @(
    'Bruinet.Bdd.Abstractions',
    'Bruinet.Core',
    'Bruinet.Stride.Automation',
    'Bruinet.Mocking',
    'Bruinet.Uat',
    'Bruinet.Wpf',
    'Bruinet.WinForms',
    'Bruinet.Blazor',
    'Bruinet.Html',
    'Bruinet.Html.Playwright',
    'Bruinet.Stride',
    'Bruinet.Maui',
    'Bruinet.Maui.CommunityToolkit',
    'Bruinet.Maui.FlaUI',
    'Bruinet.Uia.Contracts'
)

foreach ($name in $libraries) {
    dotnet pack "src/$name/$name.csproj" -c Release -o $Output -v:minimal /nr:false "-p:Version=$Version" -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $name" }
}

# bruinet, the command line (AD-028): a dotnet tool, versioned with the libraries.
dotnet pack 'tools/Bruinet.Cli/Bruinet.Cli.csproj' -c Release -o $Output -v:minimal /nr:false "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Pack failed: Bruinet.Cli' }
