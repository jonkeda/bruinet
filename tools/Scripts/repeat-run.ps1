#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs a test project several times and reports which tests fail, how often, and why.

.DESCRIPTION
    Measures flakiness instead of guessing at it. Runs the project N times, one run after the
    other, and writes repeat-summary.md next to the runs' .trx files:

      - every test that failed at least once, with how many runs it failed in;
      - "flaky" (failed in some runs) or "always" (failed in every run);
      - a *suggested* class from the failure message: framework-race, environment, driver-gap,
        or app-bug-or-test. The suggestion is a heuristic to sort the list; the class is decided
        by reading the failure.

    A test that is missing from some runs counts as unstable too. With -VaryOrder, odd runs turn
    xUnit's collection parallelism on and even runs turn it off, so the runs differ in scheduling
    and order-dependent tests show up.

    Exits 1 when a test failed in any run, so a script or CI can gate on it.

    Written for the stale-readiness work (a baseline before a change, and a comparison
    after it). UI tests drive the desktop, so the script refuses to start while another test
    host is running: two runs at once fight over the desktop and produce failures that are not real.

.PARAMETER Project
    The test project (folder or .csproj) to run.

.PARAMETER Runs
    How many times to run it. Default: 5.

.PARAMETER Filter
    Optional dotnet test filter, for example "FullyQualifiedName~Todo".

.PARAMETER Framework
    Optional target framework, for a multi-target test project.

.PARAMETER OutDir
    Where to put the .trx files and the summary. Default: TestResults/repeat-<timestamp>.

.PARAMETER Build
    Build once before the first run. By default the runs use the existing build (--no-build).

.PARAMETER VaryOrder
    Alternate xUnit collection parallelism between runs (xUnit only).

.PARAMETER AppProcess
    Name patterns of the app processes an earlier run may have left. Default: Bruinet.Samples.*.

.PARAMETER AllowProcess
    Sample processes the runs need running, by name: a web host such as
    Bruinet.Samples.Blazor.App, which the Html UI tests expect to be up. Any other sample process
    still stops the script.

.EXAMPLE
    .\tools\Scripts\repeat-run.ps1 -Project samples/Todo/tests/Bruinet.Samples.Todo.UITests -Runs 5
    .\Brinell\tools\Scripts\repeat-run.ps1 -Project MyApp.Tests -Framework net10.0-windows10.0.19041.0 -Runs 2 -Build -VaryOrder -AppProcess 'MyApp*'
#>
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [int]$Runs = 5,
    [string]$Filter,
    [string]$Framework,
    [string]$OutDir,
    [switch]$Build,
    [switch]$VaryOrder,
    [string[]]$AppProcess = @('Bruinet.Samples.*'),
    [string[]]$AllowProcess = @()
)

$ErrorActionPreference = 'Stop'

# No MSBuild nodes that outlive a run: a reused node inherits the output handle, and
# `dotnet test | Out-Null` then waits for it to close, so the script stalls after the first run
# (seen 2026-09-19, after a build in the same PowerShell command).
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

if (Get-Process -Name testhost -ErrorAction SilentlyContinue) {
    throw "A test host is already running. Wait for it to finish (or stop it) first: two UI runs at once produce failures that are not real."
}

# A sample app left over from an earlier run breaks the next run's attach (FlaUI GetMainWindow
# fails with "Unexpected HRESULT"): every test of that fixture fails, and none of it is real.
$orphans = Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $name = $_.ProcessName; @($AppProcess | Where-Object { $name -like $_ }).Count -gt 0 -and $AllowProcess -notcontains $name }
if ($orphans) {
    throw "Sample app(s) still running from an earlier run: $(($orphans | ForEach-Object { "$($_.ProcessName) ($($_.Id))" }) -join ', '). Stop them first."
}

if (-not $OutDir) {
    $OutDir = Join-Path 'TestResults' ("repeat-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$frameworkArguments = if ($Framework) { @('-f', $Framework) } else { @() }
if ($Build) {
    dotnet build $Project @frameworkArguments -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

$started = Get-Date
for ($run = 1; $run -le $Runs; $run++) {
    Write-Host "Run $run of $Runs..."
    $arguments = @('test', $Project, '--no-build', '-nologo') + $frameworkArguments + @(
        '--logger', "trx;LogFileName=run-$run.trx", '--results-directory', $OutDir)
    if ($Filter) { $arguments += @('--filter', $Filter) }
    if ($VaryOrder) { $arguments += @('--', "xUnit.ParallelizeTestCollections=$(($run % 2) -eq 1)") }
    & dotnet @arguments | Out-Null
}
$elapsed = (Get-Date) - $started

function Get-SuggestedClass([string]$message) {
    switch -Regex ($message) {
        'not visible within|stale|ElementNotAvailable|0x80040201|replaced|StaleElement' { return 'framework-race' }
        'session|Appium|UiAutomator2|foreground|topmost|occlu|emulator|connection refused|another test' { return 'environment' }
        'NotSupportedException|PlatformNotSupported|has no route|not implemented' { return 'driver-gap' }
        'Assert|Expected|AssertionException' { return 'app-bug-or-test' }
        default { return 'unclassified' }
    }
}

$failures = @{}
$testsSeen = @{}
$runsSeen = @{}
$durations = @{}
$trxFiles = @(Get-ChildItem -Path $OutDir -Filter 'run-*.trx')
foreach ($trx in $trxFiles) {
    [xml]$doc = Get-Content -Raw -Path $trx.FullName
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('t', $doc.DocumentElement.NamespaceURI)
    foreach ($result in $doc.SelectNodes('//t:UnitTestResult', $ns)) {
        $name = $result.testName
        $testsSeen[$name] = $true
        if (-not $runsSeen.ContainsKey($name)) { $runsSeen[$name] = @{} }
        $runsSeen[$name][$trx.Name] = $true
        if ($result.duration) {
            if (-not $durations.ContainsKey($name)) { $durations[$name] = @() }
            $durations[$name] += [TimeSpan]::Parse($result.duration).TotalSeconds
        }
        if ($result.outcome -ne 'Failed') { continue }
        $messageNode = $result.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)
        $message = if ($messageNode) { ($messageNode.InnerText -split "`n")[0].Trim() } else { '' }
        if ($message.Length -gt 200) { $message = $message.Substring(0, 200) + '...' }
        if (-not $failures.ContainsKey($name)) { $failures[$name] = @{ Count = 0; Messages = @{} } }
        $failures[$name].Count++
        $failures[$name].Messages[$message] = $true
    }
}

$lines = @(
    "# Repeat run: $Project",
    '',
    "$Runs runs, $($testsSeen.Count) tests, $([int]$elapsed.TotalMinutes) min. Filter: $(if ($Filter) { $Filter } else { '(none)' }).",
    '',
    'The class column is a suggestion from the failure message, to sort the list. Decide the class by reading the failure.',
    '',
    '| Test | Failed | Kind | Suggested class | First message |',
    '|---|---:|---|---|---|'
)
foreach ($name in ($failures.Keys | Sort-Object { -$failures[$_].Count }, { $_ })) {
    $entry = $failures[$name]
    $kind = if ($entry.Count -ge $Runs) { 'always' } else { 'flaky' }
    $first = @($entry.Messages.Keys)[0]
    $class = Get-SuggestedClass $first
    $cell = $first -replace '\|', '\|'
    $lines += "| ``$name`` | $($entry.Count)/$Runs | $kind | $class | $cell |"
}
if ($failures.Count -eq 0) { $lines += '| (no failures) | | | | |' }

# A test that ran in some runs only: a crash, a test that hides itself, or discovery that varies.
$missing = @($runsSeen.Keys | Where-Object { $runsSeen[$_].Count -lt $trxFiles.Count } | Sort-Object)
if ($missing) {
    $lines += @('', '## Missing from some runs', '', '| Test | Ran in |', '|---|---:|')
    foreach ($name in $missing) { $lines += "| ``$name`` | $($runsSeen[$name].Count)/$($trxFiles.Count) |" }
}

$lines += @('', '## Slowest tests', '', 'Mean and worst over the runs, in seconds.', '', '| Test | Mean | Worst |', '|---|---:|---:|')
$slowest = $durations.Keys |
    ForEach-Object { [pscustomobject]@{ Name = $_; Mean = ($durations[$_] | Measure-Object -Average).Average; Worst = ($durations[$_] | Measure-Object -Maximum).Maximum } } |
    Sort-Object Mean -Descending | Select-Object -First 5
foreach ($test in $slowest) {
    $lines += "| ``$($test.Name)`` | $($test.Mean.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)) | $($test.Worst.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)) |"
}

$summary = Join-Path $OutDir 'repeat-summary.md'
$lines | Set-Content -Path $summary -Encoding utf8
Write-Host "Summary: $summary"

if ($trxFiles.Count -lt $Runs) {
    Write-Host "Only $($trxFiles.Count) of $Runs runs wrote a .trx file."
    exit 1
}
if ($testsSeen.Count -eq 0) {
    Write-Host 'No test ran. Check the project and the filter.'
    exit 1
}
if ($failures.Count -gt 0 -or $missing) {
    Write-Host "Unstable or failing: $($failures.Count) failed at least once, $($missing.Count) missing from some runs."
    exit 1
}
Write-Host "Stable: $($testsSeen.Count) tests passed in all $Runs runs."
