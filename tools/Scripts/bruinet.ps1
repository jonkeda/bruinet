#!/usr/bin/env pwsh
# Runs bruinet from this repository's source, for scripts and people inside Bruinet's own repo.
# Anywhere else, install it: dotnet tool install -g Bruinet.Cli, then run bruinet.
#
#   pwsh tools/Scripts/bruinet.ps1 --help
#   pwsh tools/Scripts/bruinet.ps1 scrape check --solution Scraper.Sites --project Bruinet.Html.React

$project = Join-Path $PSScriptRoot '..' 'Bruinet.Cli' 'Bruinet.Cli.csproj'
& dotnet run --project $project -v:q -- @args
exit $LASTEXITCODE
