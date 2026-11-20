#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The Scraper end to end, unattended: explore an app, check the guards held, draft page objects,
    let an agent improve them (optional), verify them against the app, and write one report.

.DESCRIPTION
    Each run works on a new project in its own folder under TestResults/scrape-loop, never on the
    repository. The stages:

      1. App      start the target app on a free port
      2. Explore  scrape explore --headless --yes, with the profile's guards
      3. Guards   the test site's counters: what Explore was allowed to do, and no more
      4. Draft    scrape draft: page objects that build, or put back
      5. Agent    Copilot CLI or Claude Code, with only the Bruinet MCP server's tools (skipped for none)
      6. Verify   scrape verify: every member found once in the running app
      7. Report   loop-report.md and loop-report.json, written even when a stage fails

    Exit code 0 when every stage passed in every run, 1 otherwise.

.PARAMETER Target
    test-site (the Scraper's test site, with its dangerous page and counters) or blazor-sample; or a
    public site from Scraper.Sites: plain, react, vue2, blazor; or sites, the four in turn with one
    summary report. A public site that does not answer is reported as site down, not a failure; its
    guard check is that no click's label carries a danger word, since it has no counters.

.PARAMETER Agent
    copilot (the default; it costs AI credits), claude, or none (free and deterministic).

.PARAMETER Profile
    careful (the default guards: every counter must stay 0) or test-tenant (delete and update off,
    nothing blocked, not-unsafe clicks: delete and update must be reached, nothing else).

.PARAMETER Repeat
    How many runs, one after the other, to catch what fails only sometimes.

.PARAMETER AgentMinutes
    The agent stage's time limit; the project's AgentMinutes (10) when 0.

.PARAMETER WebsocketApps
    Overrides the config's WebsocketApps: links (Explore's default), warn or ignore. The Blazor sample
    runs with warn unless this says otherwise, so its clicks are explored.

.PARAMETER Factory
    Runs the factory instead of explore, draft and verify: the app's places are seeded and walked, the app is cut
    into modules, and loops draft a class and a test per place, build the suite and run the tests on the app
    (scrape scraper run). The project is a test project from nothing, with the plain site's draft templates
    (tools/Scripts/factory). The stages are App, Places, Guards, Modules, Run, Suite, Replay and, on the test
    site, Faults: what each planted fault of the site ended as.

.PARAMETER TurnOff
    A danger-word category to turn off in the project's config while the guard check still expects
    the profile: breaks a guard on purpose, to prove the loop catches it.

.EXAMPLE
    pwsh tools/Scripts/scrape-loop.ps1 -Target test-site
    pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Profile test-tenant -Repeat 3
    pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Agent copilot
    pwsh tools/Scripts/scrape-loop.ps1 -Target sites -Agent none
    pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Factory
#>
param(
    [ValidateSet('test-site', 'blazor-sample', 'plain', 'react', 'vue2', 'blazor', 'sites')][string]$Target = 'test-site',
    [ValidateSet('none', 'copilot', 'claude')][string]$Agent = 'copilot',
    [ValidateSet('careful', 'test-tenant')][string]$Profile = 'careful',
    [int]$Repeat = 1,
    [int]$AgentMinutes = 0,
    [string]$TurnOff,
    [switch]$Factory,
    [ValidateSet('', 'links', 'warn', 'ignore')][string]$WebsocketApps = '',
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$OutDir = if ($OutDir) { $OutDir } else { Join-Path $root 'TestResults' 'scrape-loop' "$stamp-$Target" }
New-Item -ItemType Directory -Force $OutDir | Out-Null

# The four public sites, one after the other, each with its own report, and one summary.
if ($Target -eq 'sites') {
    $rows = @()
    foreach ($site in 'plain', 'react', 'vue2', 'blazor') {
        $siteOut = Join-Path $OutDir $site
        & $PSCommandPath -Target $site -Agent $Agent -Repeat $Repeat -AgentMinutes $AgentMinutes -OutDir $siteOut
        $json = Get-Content (Join-Path $siteOut 'loop-report.json') -Raw | ConvertFrom-Json
        foreach ($r in $json.Runs) {
            $stages = ($r.Stages.PSObject.Properties | ForEach-Object { "$($_.Name) $(if ($_.Value.Passed) { 'ok' } else { 'FAIL' })" }) -join ', '
            $verify = if ($r.Stages.Verify) { $r.Stages.Verify.Detail } else { '' }
            $rows += "| $site | $($r.Run) | $(if ($r.SiteDown) { 'site down' } elseif ($r.Passed) { 'passed' } else { '**FAILED**' }) | $stages | $($verify -replace '\|', '\|') |"
        }
    }
    $failed = @($rows | Where-Object { $_ -match '\*\*FAILED\*\*' }).Count
    @("# Scrape loop: sites", '', "Agent $Agent, $Repeat run(s) each: **$(if ($failed) { 'FAILED' } else { 'passed' })**.", '',
      '| Site | Run | | Stages | Verify |', '| --- | --- | --- | --- | --- |') + $rows -join "`n" | Set-Content (Join-Path $OutDir 'loop-report.md')
    Write-Host "Report: $(Join-Path $OutDir 'loop-report.md')"
    exit $(if ($failed) { 1 } else { 0 })
}

# What each target is: how to start it, where Explore starts, how many pages it has at least. A public
# site (Site) runs nowhere locally: its project and config are Scraper.Sites/<Project>.
$targets = @{
    'test-site'     = @{ Project = 'Site.UITests'; Start = 'index.html'; MinPages = 4; Counters = $true; Blazor = $false }
    'blazor-sample' = @{ Project = 'BlazorSample.UITests'; Start = ''; MinPages = 3; Counters = $false; Blazor = $true }
    'plain'         = @{ Project = 'Bruinet.Html.Plain'; Site = $true; MinCaptures = 1 }
    'react'         = @{ Project = 'Bruinet.Html.React'; Site = $true; MinCaptures = 2 }
    'vue2'          = @{ Project = 'Bruinet.Html.Vue2'; Site = $true; MinCaptures = 2 }
    'blazor'        = @{ Project = 'Bruinet.Html.Blazor'; Site = $true; MinCaptures = 2; Blazor = $true }
}
$spec = $targets[$Target]

# Words no click on a public site may carry: a check of Explore's judge from outside it.
$dangerous = '\b(log ?out|sign ?out|delete|remove|save|submit|send|pay|checkout|buy|order|confirm|approve|reject|import|upload|reset)\b'

function Invoke-Build([string]$project) {
    $out = & dotnet build (Join-Path $root $project) -v:q -nologo /nr:false 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Building $project failed:`n$($out -join "`n")" }
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop(); $port
}

# Starts the target app; its process and base URL.
function Start-App {
    if ($spec.Site) {
        $config = Get-Content (Join-Path $root 'Scraper.Sites' $spec.Project 'scraper.config.md') -Raw
        if ($config -notmatch '\| StartUrl \| (?<url>\S+) \|') { throw "No StartUrl in $($spec.Project)'s scraper.config.md." }
        $url = $Matches.url
        try { $status = (Invoke-WebRequest $url -TimeoutSec 20 -SkipHttpErrorCheck).StatusCode } catch { $status = 0 }
        return @{ Process = $null; Url = $url; Down = ($status -lt 200 -or $status -ge 400); Status = $status }
    }
    if ($Target -eq 'test-site') {
        $dll = Join-Path $root 'tests/Bruinet.Scraper.TestSite/bin/Debug/net10.0/Bruinet.Scraper.TestSite.dll'
        $info = [System.Diagnostics.ProcessStartInfo]::new('dotnet', "`"$dll`"")
        $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.UseShellExecute = $false
        $process = [System.Diagnostics.Process]::Start($info)
        $line = $process.StandardOutput.ReadLine()
        if ($line -notmatch '^Listening on (?<url>\S+)$') { throw "The test site did not start: $line" }
        return @{ Process = $process; Url = $Matches.url }
    }
    $port = Get-FreePort
    $url = "http://localhost:$port/"
    $app = Join-Path $root 'samples/Bruinet.Samples.Blazor.App'
    $info = [System.Diagnostics.ProcessStartInfo]::new('dotnet', "run --no-build --project `"$app`" --urls $url")
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true; $info.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($info)
    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        try { if ((Invoke-WebRequest $url -TimeoutSec 5 -SkipHttpErrorCheck).StatusCode -eq 200) { return @{ Process = $process; Url = $url } } } catch { Start-Sleep -Milliseconds 500 }
        if ($process.HasExited) { break }
    }
    throw "The Blazor sample did not answer at $url."
}

function Stop-App($app) {
    if ($null -eq $app -or $null -eq $app.Process -or $app.Process.HasExited) { return }
    try { $app.Process.StandardInput.Close() } catch { }
    if (-not $app.Process.WaitForExit(5000)) { $app.Process.Kill($true) }
}

function Write-Project([string]$project, [string[]]$references) {
    # The factory's suite runs its tests: a test project, with the browser and the fixture a plain site's templates use.
    $tests = if ($Factory) { @"

  <ItemGroup>
    <ProjectReference Include="$(Join-Path $root 'src/Bruinet.Html.Playwright/Bruinet.Html.Playwright.csproj')" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <Using Include="Xunit" />
  </ItemGroup>
"@ } else { '' }
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>$(if ($Factory) { "`n    <IsPackable>false</IsPackable>`n    <IsTestProject>true</IsTestProject>" })
  </PropertyGroup>
  <ItemGroup>
    $($references -join "`n    ")
  </ItemGroup>$tests
</Project>
"@ | Set-Content (Join-Path $project "$($spec.Project).csproj")
    if ($Factory) {
        Copy-Item (Join-Path $PSScriptRoot 'factory' 'draft-templates.md') (Join-Path $project 'draft-templates.md')
        # The fixture is the suite's own: in its namespace, so every test of it sees it.
        (Get-Content (Join-Path $PSScriptRoot 'factory' 'SiteContext.cs') -Raw).Replace('__NAMESPACE__', $spec.Project) | Set-Content (Join-Path $project 'SiteContext.cs')
    }
}

# A new UI test project for the target in the run's folder, restored from nuget.org as Bruinet is.
function New-TargetProject([string]$folder, [string]$url) {
    $project = Join-Path $folder 'tests' $spec.Project
    New-Item -ItemType Directory -Force $project | Out-Null
    Copy-Item (Join-Path $root 'nuget.config') (Join-Path $folder 'nuget.config')
    $references = @("<ProjectReference Include=`"$(Join-Path $root 'src/Bruinet.Html/Bruinet.Html.csproj')`" />")
    if ($spec.Site) {
        # The site's own config, as committed: the loop's copy changes nothing in Scraper.Sites.
        if ($spec.Blazor) { $references += "<ProjectReference Include=`"$(Join-Path $root 'src/Bruinet.Blazor/Bruinet.Blazor.csproj')`" />" }
        Write-Project $project $references
        Copy-Item (Join-Path $root 'Scraper.Sites' $spec.Project 'scraper.config.md') (Join-Path $project 'scraper.config.md')
        return $project
    }
    if ($spec.Blazor) { $references += "<ProjectReference Include=`"$(Join-Path $root 'src/Bruinet.Blazor/Bruinet.Blazor.csproj')`" />" }
    Write-Project $project $references

    $explore = @('| DelayMs | 50 |')
    $sections = @()
    if ($Profile -eq 'test-tenant') {
        $explore += '| ClickPolicy | not-unsafe |', '| BlockMethods | |'
        $sections += "## Danger words`n`n| Category | On |`n| --- | --- |`n| delete | false |`n| update | false |"
    }
    $websockets = if ($WebsocketApps) { $WebsocketApps } elseif ($spec.Blazor) { 'warn' } else { '' }
    if ($websockets) { $explore += "| WebsocketApps | $websockets |" }
    if ($TurnOff) {
        if ($sections.Count -eq 0) { $sections += "## Danger words`n`n| Category | On |`n| --- | --- |" }
        $sections[0] += "`n| $TurnOff | false |"
    }
    $testIds = if ($spec.Blazor) { "`n## Test ids`n`n| Attribute |`n| --- |`n| data-testid |`n| data-automation-id |`n" } else { '' }
    @"
# Scraper Config

Made by scrape-loop.ps1 for $Target ($Profile).

## App

| Field | Value |
| --- | --- |
| Platform | $(if ($spec.Blazor) { 'blazor' } else { 'html' }) |
| StartUrl | $url$($spec.Start) |
| Namespace | $($spec.Project).Pages |
$testIds
## Explore

| Field | Value |
| --- | --- |
$($explore -join "`n")

$($sections -join "`n`n")
"@ | Set-Content (Join-Path $project 'scraper.config.md')
    return $project
}

# bruinet scrape; its exit code and its JSON.
function Invoke-Scrape([string[]]$arguments) {
    $out = & dotnet $server scrape @arguments --json 2>$null
    $text = $out -join "`n"
    $json = $null
    try { $json = $text | ConvertFrom-Json -Depth 20 } catch { }
    return @{ Exit = $LASTEXITCODE; Json = $json; Text = $text }
}

# bruinet scrape for the factory: its exit code and what it said, both streams.
function Invoke-Factory([string[]]$arguments) {
    $out = & dotnet $server scrape @arguments --solution $folder --project $spec.Project 2>&1
    return @{ Exit = $LASTEXITCODE; Text = (($out | ForEach-Object { "$_" }) -join "`n") }
}

function Last-Line([string]$text) { (($text -split "`n") | Where-Object { $_.Trim() } | Select-Object -Last 1) }

# The factory's stages on a project that is there: places, guards, modules, the loops, the suite, replay.
function Invoke-FactoryStages($app, [string]$project) {
    $env:SCRAPE_SITE_URL = $app.Url
    $env:MSYS_NO_PATHCONV = '1'

    # The planted faults: pages of the test site no link leads to, named in a map, and a class a person wrote wrongly.
    $faults = @()
    if ($spec.Counters) {
        $faults = @(
            @{ Name = 'two buttons of one name'; Place = '/pages/faults/two-buttons.html' },
            @{ Name = 'a tab with a count'; Place = '/pages/faults/tab-count.html' },
            @{ Name = 'a slow page'; Place = '/pages/faults/slow.html' },
            @{ Name = 'a page that needs a record'; Place = '/pages/record.html?id=7'; Record = $true },
            @{ Name = 'a wrong hand-written class'; Place = '/pages/catalog.html'; Own = 'Pages/CatalogPage.cs' })
        $map = Join-Path $folder 'faults-map.md'
        $mapLines = @('# Pages no link leads to', '') + @($faults | Where-Object { -not $_.Own } | ForEach-Object { '- `' + $_.Place + '`' })
        $mapLines -join "`n" | Set-Content $map
        New-Item -ItemType Directory -Force (Join-Path $project 'Pages') | Out-Null
        @"
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace $($spec.Project).Pages;

/// <summary>The catalog, as a person wrote it: one member that is right and one that names what the page does not have.</summary>
public sealed class CatalogPage(IHtmlTestContext context) : PageObjectBase<CatalogPage>(context, "/pages/catalog.html")
{
    public LabelControl<CatalogPage> Heading => new(this, Locator.ByCss("h1"));

    public ButtonControl<CatalogPage> Checkout => new(this, Locator.ByCss("[data-testid='no-such-button']"));
}
"@ | Set-Content (Join-Path $project 'Pages' 'CatalogPage.cs')
    }

    $t = Get-Date
    $seed = Invoke-Factory $(if ($faults.Count) { @('places', 'seed', '--map', $map) } else { @('places', 'seed') })
    $walk = Invoke-Factory @('places', 'walk', '--headless')
    ($seed.Text, $walk.Text) -join "`n" | Set-Content (Join-Path $folder 'places-walk.log')
    $list = Join-Path $project '175-Places.json'
    $places = if (Test-Path $list) { @((Get-Content $list -Raw | ConvertFrom-Json).places).Count } else { 0 }
    Add-Stage 'Places' ($walk.Exit -eq 0 -and $places -ge $spec.MinPages) "$places places; $(Last-Line $walk.Text)" $t
    if ($walk.Exit -ne 0 -or $places -lt $spec.MinPages) { throw 'stop' }

    if ($spec.Counters) {
        $t = Get-Date
        $script:counters = Invoke-RestMethod "$($app.Url)_counters"
        $moved = @($script:counters.PSObject.Properties | Where-Object Value -gt 0 | ForEach-Object Name)
        Add-Stage 'Guards' ($moved.Count -eq 0) "moved: $(if ($moved.Count) { $moved -join ', ' } else { 'none' })" $t
        if ($moved.Count) { throw 'stop' }
    }

    $t = Get-Date
    $modules = Invoke-Factory @('scraper', 'modules', '--write')
    $setup = Invoke-Factory @('scraper', 'setup', '--set', 'Run=all', '--set', 'Walkers=2')
    Add-Stage 'Modules' ($modules.Exit -eq 0 -and $setup.Exit -eq 0) (Last-Line $modules.Text) $t
    if ($modules.Exit -ne 0 -or $setup.Exit -ne 0) { throw 'stop' }

    # The loops: a batch that stops by a rule is the factory doing its work, and is read from its state.
    $t = Get-Date
    $ran = Invoke-Factory @('scraper', 'run', '--no-walk', '--batch', '12', '--headless')
    $ran.Text | Set-Content (Join-Path $folder 'factory-run.log')
    $stateFile = Join-Path $project 'state.json'
    $state = if (Test-Path $stateFile) { Get-Content $stateFile -Raw | ConvertFrom-Json -Depth 20 } else { $null }
    $loops = if ($state) { @($state.loops) } else { @() }
    $kept = @($loops | ForEach-Object { $_.kept } | Where-Object { $_ })
    $reasons = @($loops | ForEach-Object { $_.reasons } | Where-Object { $_ })
    $byCode = ($reasons | Group-Object code | Sort-Object Count -Descending | ForEach-Object { "$($_.Name) $($_.Count)" }) -join ', '
    Add-Stage 'Run' ($loops.Count -gt 0 -and $kept.Count -gt 0) "$($loops.Count) loops, $($kept.Count) files kept$(if ($byCode) { "; reasons: $byCode" }); $(Last-Line $ran.Text)" $t
    if ($loops.Count -eq 0 -or $kept.Count -eq 0) { throw 'stop' }
    $review = Invoke-Factory @('scraper', 'review')

    # The suite as the factory left it: it builds, and every test it kept passes once more, together.
    $t = Get-Date
    $out = & dotnet test $project -v:q -nologo /nr:false 2>&1
    $summary = ($out | Where-Object { "$_" -match '(Passed|Failed)!\s+-' } | Select-Object -Last 1)
    Add-Stage 'Suite' ($LASTEXITCODE -eq 0) "$(if ($summary) { "$summary".Trim() } else { ($out | Select-Object -Last 3) -join ' ' })" $t
    if ($LASTEXITCODE -ne 0) { throw 'stop' }

    $t = Get-Date
    $replay = Invoke-Factory @('scraper', 'replay')
    Add-Stage 'Replay' ($replay.Exit -eq 0 -and $replay.Text -match 'nothing changed') (Last-Line $replay.Text) $t

    # What each planted fault ended as: a class the factory kept, a reason it gave, a page it owes a record for, or a
    # person's class it left alone. A fault with no outcome was not met at all.
    if ($faults.Count) {
        $t = Get-Date
        $status = Invoke-Factory @('scraper', 'status')
        $outcomes = @()
        foreach ($fault in $faults) {
            $key = if ($fault.Record) { ($fault.Place -replace '\d+$', '{id}') } else { $fault.Place }
            $class = Get-ChildItem (Join-Path $project 'Pages') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -TotalCount 1) -match "<scraped page=`"$([regex]::Escape($key))`" variant=`"form`"" } | Select-Object -First 1
            $reason = $reasons | Where-Object { $_.place -eq $key } | Select-Object -Last 1
            $outcome = if ($fault.Own) {
                $own = Join-Path $project $fault.Own
                if ((Test-Path $own) -and (Get-Content $own -Raw) -match 'no-such-button' -and -not $class) { "left alone: a person's class is not the factory's to change$(if ($reason) { " ($($reason.code))" })" } else { $null }
            }
            elseif ($class) { "kept as $($class.BaseName)" }
            elseif ($reason) { "reason $($reason.code): $("$($reason.evidence)".Substring(0, [math]::Min(110, "$($reason.evidence)".Length)))" }
            elseif ($fault.Record -and $status.Text -match '(?<n>[1-9]\d*) pages of one record') { "owed as the page of one record: no template says how such a page is opened" }
            else { $null }
            $outcomes += "$($fault.Name): $(if ($outcome) { $outcome } else { 'NO OUTCOME' })"
        }
        Add-Stage 'Faults' (-not ($outcomes | Where-Object { $_ -match 'NO OUTCOME' })) ($outcomes -join '; ') $t
    }

    return @{ State = $state; Reasons = $reasons; Kept = $kept }
}

function Invoke-Agent([string]$solution, [string]$project, [string]$logFile) {
    $minutes = if ($AgentMinutes -gt 0) { $AgentMinutes } else { 10 }
    $prompt = "Use only the bruinet-bdd MCP tools. In the scraped project $($spec.Project), work on the drafted page objects (the *.scraped.cs files): detect_patterns, and for each repeated pattern write a control with write_code; give members better names where the draft's are poor; then check_code until it passes. Do not start or stop recordings, and do not explore."
    $mcp = @{ mcpServers = @{ 'bruinet-bdd' = @{ type = 'local'; command = 'dotnet'; args = @($server, 'mcp', '--solution', $solution); tools = @('*') } } } | ConvertTo-Json -Depth 5 -Compress
    if ($Agent -eq 'copilot') {
        $command = 'copilot'
        $arguments = @('-p', $prompt, '--additional-mcp-config', $mcp, '--allow-tool', 'bruinet-bdd', '--deny-tool', 'shell', '--deny-tool', 'write')
    } else {
        $config = Join-Path $solution 'mcp.json'
        $mcp.Replace('"type":"local"', '"type":"stdio"') | Set-Content $config
        $command = 'claude'
        $arguments = @('-p', $prompt, '--mcp-config', $config, '--strict-mcp-config', '--allowed-tools', 'mcp__bruinet-bdd', '--output-format', 'json')
    }
    $job = Start-Job -ScriptBlock { param($c, $a, $d) Set-Location $d; & $c @a 2>&1 } -ArgumentList $command, $arguments, $root
    if (-not (Wait-Job $job -Timeout ($minutes * 60))) {
        Stop-Job $job; Remove-Job $job -Force
        return @{ Passed = $false; Detail = "The agent took longer than $minutes minutes and was stopped." }
    }
    $output = Receive-Job $job; Remove-Job $job
    $output | Set-Content $logFile
    return @{ Passed = $true; Detail = "Log: $logFile" }
}

# A file list with hashes, to see what the agent changed.
function Get-Files([string]$folder) {
    $files = @{}
    Get-ChildItem $folder -Recurse -File -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|Recordings)[\\/]' } |
        ForEach-Object { $files[$_.FullName.Substring($folder.Length + 1)] = (Get-FileHash $_.FullName).Hash }
    return $files
}

if ($Factory -and $Target -ne 'test-site') { throw 'The factory stage runs on the test site: -Target test-site -Factory.' }
Write-Host "scrape-loop: $Target, $(if ($Factory) { 'the factory' } else { "agent $Agent" }), profile $Profile, $Repeat run(s) into $OutDir"
Invoke-Build 'tools/Bruinet.Cli/Bruinet.Cli.csproj'
if ($Target -eq 'test-site') { Invoke-Build 'tests/Bruinet.Scraper.TestSite/Bruinet.Scraper.TestSite.csproj' }
elseif (-not $spec.Site) { Invoke-Build 'samples/Bruinet.Samples.Blazor.App/Bruinet.Samples.Blazor.App.csproj' }
$server = Join-Path $root 'tools/Bruinet.Cli/bin/Debug/net10.0/bruinet.dll'
$repoBefore = & git -C $root status --porcelain

$runs = @()
for ($run = 1; $run -le $Repeat; $run++) {
    $folder = Join-Path $OutDir "run-$run"
    New-Item -ItemType Directory -Force $folder | Out-Null
    $stages = [ordered]@{}
    $app = $null
    $explored = $null
    $counters = $null
    $siteDown = $false
    function Add-Stage([string]$name, [bool]$passed, [string]$detail, [datetime]$started) {
        $stages[$name] = [ordered]@{ Passed = $passed; Detail = $detail; Seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) }
        $mark = if ($passed) { 'ok  ' } else { 'FAIL' }
        Write-Host "  [$run] $mark $name  $detail"
    }
    try {
        $t = Get-Date
        $app = Start-App
        if ($app.Down) {
            # A public site that does not answer says nothing about the Scraper.
            $siteDown = $true
            Add-Stage 'App' $true "site down: $($app.Url) answered $(if ($app.Status) { $app.Status } else { 'nothing' })" $t
            throw 'stop'
        }
        $project = New-TargetProject $folder $app.Url
        Add-Stage 'App' $true "$($app.Url)" $t

        if ($Factory) {
            $made = Invoke-FactoryStages $app $project
            throw 'stop'
        }

        $t = Get-Date
        $explore = Invoke-Scrape @('explore', '--solution', $folder, '--project', $spec.Project, '--headless', '--yes')
        $explored = $explore.Json
        $pages = if ($explored) { @($explored.captures | ForEach-Object page | Sort-Object -Unique).Count } else { 0 }
        # A public site is one page: what counts is reaching its states (MaxPages and MaxMinutes are its budget, not a failure).
        $ok = if ($spec.Site) { $explore.Exit -eq 0 -and $explored.captures.Count -ge $spec.MinCaptures }
              else { $explore.Exit -eq 0 -and $pages -ge $spec.MinPages }
        $least = if ($spec.Site) { "at least $($spec.MinCaptures) captures" } else { "at least $($spec.MinPages)" }
        Add-Stage 'Explore' $ok "$(if ($explored) { "$($explored.stop): $($explored.stopDetail)" } else { $explore.Text }) $pages pages ($least), $(if ($explored) { "$($explored.captures.Count) captures, $($explored.clicks) clicks, $($explored.skipped.Count) skipped, $($explored.blocked.Count) blocked" })" $t
        if (-not $ok) { throw 'stop' }

        if ($spec.Site) {
            # No counters on a public site: every click's label is checked against the danger words here,
            # outside Explore's judge, and what was blocked is listed.
            $t = Get-Date
            $clicked = @($explored.captures | ForEach-Object via | Where-Object { $_ -match "^click '(?<label>.*)' on " } | ForEach-Object { $Matches.label })
            $bad = @($clicked | Where-Object { $_ -match $dangerous })
            Add-Stage 'Guards' ($bad.Count -eq 0) "$($clicked.Count) clicked$(if ($bad.Count) { ", with a danger word: $($bad -join ', ')" } else { ', none with a danger word' }); $($explored.blocked.Count) requests blocked" $t
            if ($bad.Count) { throw 'stop' }
        }
        if ($spec.Counters) {
            $t = Get-Date
            $counters = Invoke-RestMethod "$($app.Url)_counters"
            $moved = @($counters.PSObject.Properties | Where-Object Value -gt 0 | ForEach-Object Name)
            $ok = if ($Profile -eq 'careful') { $moved.Count -eq 0 }
                  else { ($moved | Where-Object { $_ -notin 'delete', 'update' }).Count -eq 0 -and 'delete' -in $moved -and 'update' -in $moved }
            Add-Stage 'Guards' $ok "moved: $(if ($moved.Count) { ($moved | ForEach-Object { "$_ $($counters.$_)" }) -join ', ' } else { 'none' })$(if ($Profile -eq 'test-tenant') { ' (expected: delete and update, nothing else)' })" $t
            if (-not $ok) { throw 'stop' }
        }

        $t = Get-Date
        $draft = Invoke-Scrape @('draft', '--solution', $folder, '--project', $spec.Project)
        $written = if ($draft.Json) { @($draft.Json.pages | Where-Object { $_.outcome -eq 'Written' }).Count } else { 0 }
        $reverted = if ($draft.Json) { @($draft.Json.pages | Where-Object { $_.outcome -eq 'Reverted' } | ForEach-Object className) } else { @() }
        Add-Stage 'Draft' ($draft.Exit -eq 0) "$written written$(if ($reverted.Count) { ", put back: $($reverted -join ', ')" }); $(if ($draft.Json) { "$($draft.Json.makeControls.Count) patterns to make controls of" } else { $draft.Text })" $t
        if ($draft.Exit -ne 0) { throw 'stop' }

        $t = Get-Date
        if ($Agent -eq 'none') {
            Add-Stage 'Agent' $true 'skipped (-Agent none)' $t
        } else {
            $before = Get-Files $project
            $agentRun = Invoke-Agent $folder $project (Join-Path $folder 'agent.log')
            $after = Get-Files $project
            $changed = @($after.Keys | Where-Object { $before[$_] -ne $after[$_] }) + @($before.Keys | Where-Object { -not $after.ContainsKey($_) })
            $repoNow = & git -C $root status --porcelain
            $outside = Compare-Object @($repoBefore) @($repoNow) -PassThru
            $ok = $agentRun.Passed -and -not $outside
            Add-Stage 'Agent' $ok "$Agent changed $($changed.Count) file(s): $($changed -join ', '); $($agentRun.Detail)$(if ($outside) { "; wrote outside the project: $($outside -join ', ')" })" $t
            if (-not $ok) { throw 'stop' }
            $check = & dotnet build $project -v:q -nologo /nr:false 2>&1
            if ($LASTEXITCODE -ne 0) { Add-Stage 'Agent build' $false (($check | Select-Object -Last 5) -join ' ') $t; throw 'stop' }
        }

        $t = Get-Date
        $verify = Invoke-Scrape @('verify', '--solution', $folder, '--project', $spec.Project, '--headless')
        $members = if ($verify.Json) { @($verify.Json.pages | ForEach-Object { $_.members }) } else { @() }
        $failed = @($verify.Json.pages | ForEach-Object { $page = $_; $_.members | Where-Object { $_.verdict -in 'NotFound', 'Several' } | ForEach-Object { "$($page.className).$($_.name) $($_.verdict)" } })
        $unread = @($members | Where-Object { $_.verdict -eq 'Unreadable' }).Count
        Add-Stage 'Verify' ($verify.Exit -eq 0) "$($members.Count - $failed.Count - $unread) of $($members.Count) members found once$(if ($unread) { ", $unread made by the project's own controls, not read" })$(if ($failed.Count) { "; $($failed -join ', ')" })" $t
    }
    catch {
        if ($_.Exception.Message -ne 'stop') { $stages['Error'] = [ordered]@{ Passed = $false; Detail = $_.Exception.Message; Seconds = 0 }; Write-Host "  [$run] FAIL $($_.Exception.Message)" }
    }
    finally {
        Stop-App $app
    }
    $runs += [ordered]@{
        Run = $run; Folder = $folder; SiteDown = $siteDown
        Passed = $siteDown -or (-not ($stages.Values | Where-Object { -not $_.Passed }) -and $stages.Contains($(if ($Factory) { 'Faults' } else { 'Verify' })))
        Stages = $stages; Counters = $counters
        Explore = if ($explored) { [ordered]@{ Stop = $explored.stop; Captures = @($explored.captures); Skipped = @($explored.skipped); Blocked = @($explored.blocked); Report = $explored.report } } else { $null }
    }
}

# 7. Report
$passed = -not ($runs | Where-Object { -not $_.Passed })
$report = [ordered]@{ Target = $Target; Agent = $Agent; Profile = $Profile; TurnOff = $TurnOff; Passed = $passed; Runs = $runs }
$report | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutDir 'loop-report.json')
$md = @("# Scrape loop: $Target", '', "Agent $Agent, profile $Profile$(if ($TurnOff) { ", $TurnOff turned off on purpose" }), $Repeat run(s): **$(if ($passed) { 'passed' } else { 'FAILED' })**.", '')
foreach ($r in $runs) {
    $md += "## Run $($r.Run): $(if ($r.SiteDown) { 'site down' } elseif ($r.Passed) { 'passed' } else { 'FAILED' })", '', '| Stage | | Seconds | Detail |', '| --- | --- | --- | --- |'
    foreach ($name in $r.Stages.Keys) {
        $s = $r.Stages[$name]
        $md += "| $name | $(if ($s.Passed) { 'ok' } else { '**fail**' }) | $($s.Seconds) | $(($s.Detail -replace '\|', '\|') -replace "`r?`n", ' ') |"
    }
    if ($r.Explore) {
        $md += '', "Explore's report: ``$($r.Explore.Report)``"
        if ($r.Explore.Blocked.Count) { $md += '', 'Blocked:', '' ; $md += $r.Explore.Blocked | ForEach-Object { "- $($_.method) $($_.url): $($_.reason)" } }
    }
    $md += ''
}
$md -join "`n" | Set-Content (Join-Path $OutDir 'loop-report.md')
Write-Host "Report: $(Join-Path $OutDir 'loop-report.md')"
exit $(if ($passed) { 0 } else { 1 })
