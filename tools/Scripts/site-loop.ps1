#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The site loop's orchestrator: per module of a suite, find the first stage whose gate does not pass, run that gate
    (and, with -Agent, an agent with the stage's skill), and write the state and a morning report.

.DESCRIPTION
    A script, not a model: it decides nothing a check can decide. The state is the suite's .testing/100-Loops.md,
    a table a person can read and edit:

      | Module | Spec | Stage | State | Waiting for |

    The gates, in order (the proposal's section 5):

      1 Spec          specs/<Spec>.spec.md exists, passes lirenet check, and its Status is accepted   👤 accepts
      4 Page objects  the suite builds
      5 Scenarios     lirenet trace reaches every rule of the spec from Modules/<Module>
                      and, for runnable specs, every step binds (--filter Category=Bind: no browser, once per loop)
      6 Run           the spec's own tests, -Runs times in a row, every rule Passing (lirenet trace --results): a runnable
                      spec's examples (--filter Spec=<spec>), otherwise the module's tests (--filter Module=<Module>).
                      A module's code tests beside its runnable specs run once per loop, not once per spec per run.

    Explore (2) and Controls (3) have no gate of their own here: their work shows in 4 to 6.
    A module whose gates all pass is done. A 👤 gate marks the module waiting and the loop goes on to the next one.
    With -Agent claude, a failed gate that an agent can fix runs `claude -p` with the stage's skill and the gate's
    output, then the gate again, up to -Attempts times; then the module is stuck.

    Writes .testing/100-Loops.md and .testing/runs/<yyyy-MM-dd-HHmm>/report.md (and the runs' .trx files beside it).
    The report ends with "Improvement review" when the suite's .testing/600-Improvements.md is behind: as many
    journeys done since its last review as its Every row says (the improvement-review skill).
    Exit code 0 when every module is done or waiting for a person, 1 when one is stuck.

.PARAMETER Suite
    The suite's folder (with .testing/, specs/ and a *.slnx), such as ../Bouw7.

.PARAMETER Module
    Only this module; all modules of 100-Loops.md when empty.

.PARAMETER Spec
    Only this spec of 100-Loops.md, such as B7.CON.03; all specs of the modules chosen when empty.

.PARAMETER Rule
    While writing: build, then run only this rule's tests (--filter Rule=<id>), -Runs times. No other gate, and
    100-Loops.md is left as it is. The gate for the journey is the loop without -Rule.

.PARAMETER Agent
    none (the default: gates only, free and deterministic) or claude (Claude Code headless).

.PARAMETER Runs
    How many green runs in a row stage 6 asks (the proposal's phase gate is 3).

.PARAMETER AtOnce
    Stage 6's runs side by side, a test process each, instead of one after another. For a journey that only reads:
    runs that write in the same account at once would break each other.

.PARAMETER Attempts
    Agent attempts per failed gate.

.PARAMETER AgentMinutes
    An agent attempt's time limit.

.EXAMPLE
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Module Hours -Runs 3
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Module Invoices -Agent claude
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Spec B7.CON.03 -Runs 3
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Spec B7.CON.03 -Runs 3 -AtOnce
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Rule B7.CON.03.R4
#>
param(
    [Parameter(Mandatory)][string]$Suite,
    [string]$Module,
    [string]$Spec,
    [string]$Rule,
    [ValidateSet('none', 'claude')][string]$Agent = 'none',
    [int]$Runs = 1,
    [switch]$AtOnce,
    [int]$Attempts = 2,
    [int]$AgentMinutes = 20
)

$ErrorActionPreference = 'Stop'
$Suite = (Resolve-Path $Suite).Path
$Bruinet = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Loops = Join-Path $Suite '.testing/100-Loops.md'
$Stamp = Get-Date -Format 'yyyy-MM-dd-HHmm'
$RunDir = Join-Path $Suite ".testing/runs/$Stamp"
New-Item -ItemType Directory -Force $RunDir | Out-Null
$Solution = Get-ChildItem $Suite -Filter *.slnx | Select-Object -First 1
$TestProject = Get-ChildItem $Suite -Directory -Filter '*.UITests' | Select-Object -First 1

# A suite whose config names Specs runs them (runnable specs): a spec's examples are its tests, run by the Spec trait.
$SuiteConfig = @('bdd.config.md', 'uat.config.md') | ForEach-Object { Join-Path $TestProject.FullName $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
$RunsSpecs = $SuiteConfig -and (Select-String -Path $SuiteConfig -Pattern '^\|\s*Specs\s*\|' -Quiet)
$ProjectText = Get-Content (Get-ChildItem $TestProject.FullName -Filter *.csproj | Select-Object -First 1).FullName -Raw

# Lirenet is built once and run from its output, not through dotnet run per call.
$LirenetProject = Join-Path $Bruinet 'Lirenet/src/Lirenet.Cli/Lirenet.Cli.csproj'
dotnet build $LirenetProject -c Release -v:q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Lirenet did not build: $LirenetProject" }
$Lirenet = Get-Item (dotnet msbuild $LirenetProject -getProperty:TargetPath -p:Configuration=Release).Trim()

function Invoke-Gate([string]$Name, [scriptblock]$Command) {
    $started = Get-Date
    $output = & $Command 2>&1 | Out-String
    $passed = $LASTEXITCODE -eq 0
    [pscustomobject]@{ Name = $Name; Passed = $passed; Output = $output.Trim(); Seconds = [int]((Get-Date) - $started).TotalSeconds }
}

# What a gate's row of the report says: the run's summary, else a failing gate's first error, else its last line.
function Get-GateNote($Gate) {
    if ($Gate.PSObject.Properties['Summary'] -and $Gate.Summary) { return $Gate.Summary }
    $lines = @($Gate.Output -split "`n" | Where-Object { $_.Trim() })
    if (-not $Gate.Passed) {
        $first = $lines | Where-Object { $_ -match '(: error |\berror [A-Z]+\d+|^\s*Failed |finding\(s\))' } | Select-Object -First 1
        if ($first) { return $first.Trim() }
    }
    $lines | Select-Object -Last 1
}

function Read-Loops {
    $rows = @()
    foreach ($line in Get-Content $Loops) {
        if ($line -match '^\|\s*(?<m>[^|]+?)\s*\|\s*(?<s>[^|]*?)\s*\|\s*(?<st>[^|]*?)\s*\|\s*(?<state>[^|]*?)\s*\|\s*(?<w>[^|]*?)\s*\|$' -and
            $Matches.m -notin @('Module', '---')) {
            $rows += [pscustomobject]@{ Module = $Matches.m; Spec = $Matches.s; Stage = $Matches.st; State = $Matches.state; Waiting = $Matches.w }
        }
    }
    $rows
}

function Write-Loops($rows) {
    $text = Get-Content $Loops -Raw
    foreach ($row in $rows) {
        # A module may have several specs: a row is its module and its spec.
        $pattern = '(?m)^\|\s*' + [regex]::Escape($row.Module) + '\s*\|\s*' + [regex]::Escape($row.Spec) + '\s*\|.*$'
        $text = [regex]::Replace($text, $pattern, "| $($row.Module) | $($row.Spec) | $($row.Stage) | $($row.State) | $($row.Waiting) |")
    }
    Set-Content $Loops $text.TrimEnd() -NoNewline
    Add-Content $Loops ''
}

# The outer loop (the improvement-review skill): the suite's 600-Improvements.md says how often (Every, 5 journeys
# unless it says otherwise) and how many journeys were done at its last review. Nothing when the suite has no such file.
function Get-ReviewDue([string]$Improvements, [int]$Done) {
    if (-not (Test-Path $Improvements)) { return $null }
    $every = 5
    $last = $null
    foreach ($line in Get-Content $Improvements) {
        if ($line -match '^\|\s*Every\s*\|\s*(?<n>\d+)') { $every = [int]$Matches.n }
        elseif ($line -match '^\|\s*(?<nr>\d+)\s*\|\s*(?<date>\d{4}-\d{2}-\d{2})\s*\|\s*(?<done>\d+)\s*\|') {
            $last = [pscustomobject]@{ Number = [int]$Matches.nr; Date = $Matches.date; Done = [int]$Matches.done }
        }
    }
    $since = $Done - $(if ($last) { $last.Done } else { 0 })
    if ($since -lt $every) { return $null }
    $after = if ($last) { "review $($last.Number) of $($last.Date)" } else { 'the start: no review yet' }
    "An improvement review is due: $since journeys done since $after (every $every). Skill: improvement-review."
}

function Invoke-Agent([string]$Skill, [string]$ModuleName, [string]$SpecId, $Gate) {
    if ($Agent -eq 'none') { return $false }
    $prompt = @"
You are one stage of the site loop for the suite in $Suite, module $ModuleName, spec $SpecId.
Use the $Skill skill (in $Bruinet/.github/skills/$Skill/SKILL.md) and the suite's AGENTS.md. Do only this stage.
The gate "$($Gate.Name)" failed with:
$($Gate.Output | Select-Object -Last 60)
Fix what makes the gate fail, then stop. Never touch production, never write a secret, Reader never writes.
"@
    $job = Start-Job { param($p, $dir) Set-Location $dir; claude -p $p --permission-mode acceptEdits 2>&1 } -ArgumentList $prompt, $Suite
    if (-not (Wait-Job $job -Timeout ($AgentMinutes * 60))) { Stop-Job $job; Add-Content (Join-Path $RunDir 'agent.log') "[$ModuleName] $Skill timed out"; return $true }
    Receive-Job $job | Out-File -Append (Join-Path $RunDir 'agent.log')
    $true
}

$report = [System.Collections.Generic.List[string]]::new()
$report.Add("# Site loop $Stamp")
$report.Add('')
$report.Add("Suite ``$(Split-Path $Suite -Leaf)``, agent $Agent, $Runs run(s) per module.")
$report.Add('')

# While writing: one rule, built and run, nothing else. The loop's state is the gate's, so it is not written.
if ($Rule) {
    $build = Invoke-Gate 'build' { dotnet build $Solution.FullName -v:q -nologo }
    $gates = @($build)
    for ($run = 1; $build.Passed -and $run -le $Runs; $run++) {
        $test = Invoke-Gate "run $run/${Runs}: Rule=$Rule" { dotnet test $TestProject.FullName --no-build -nologo --filter "Rule=$Rule" --logger trx --results-directory (Join-Path $RunDir "rule-$run") }
        # A rule no test names is not a pass.
        if ($test.Output -match 'No test matches') { $test.Passed = $false }
        $test | Add-Member Summary (($test.Output -split "`n" | Where-Object { $_ -match '(Passed|Failed)!|No test matches' }) -join ' ').Trim()
        $gates += $test
        if (-not $test.Passed) { break }
    }
    $report.Add("## Rule $Rule")
    $report.Add('')
    foreach ($g in $gates) {
        $line = "$(if ($g.Passed) { 'pass' } else { 'FAIL' })  $($g.Name)  $($g.Seconds) s  $(Get-GateNote $g)"
        $report.Add("- $line")
        Write-Host $line
    }
    foreach ($g in $gates | Where-Object { -not $_.Passed }) {
        @($g.Output -split "`n" | Where-Object { $_ -match '^\s+Failed |Error Message|^\s{3,}\S' -and $_ -notmatch '^\s+at ' } | Select-Object -First 12) | ForEach-Object { Write-Host $_.TrimEnd() }
        $report.Add('')
        $report.Add('```text')
        $report.Add((($g.Output -split "`n") | Select-Object -Last 60) -join "`n")
        $report.Add('```')
    }
    $report | Set-Content (Join-Path $RunDir 'report.md')
    Write-Host "Report: $(Join-Path $RunDir 'report.md')"
    exit (@($gates | Where-Object { -not $_.Passed }).Count -eq 0 ? 0 : 1)
}

$rows = Read-Loops
$build = $null
$bind = $null
$moduleTests = @{}
$stuck = 0
foreach ($row in $rows) {
    if ($Module -and $row.Module -ne $Module) { continue }
    if ($Spec -and $row.Spec -ne $Spec) { continue }
    $gates = @()
    $specPath = Join-Path $Suite "specs/$($row.Spec).spec.md"
    $modulePath = Join-Path $TestProject.FullName "Modules/$($row.Module)"
    # This spec runs when the project copies it (by name, or all specs): its examples are then its tests.
    $runsThis = $RunsSpecs -and ($ProjectText.Contains("$($row.Spec).spec.md") -or $ProjectText.Contains('*.spec.md'))
    $Runnable = if ($runsThis) { '--runnable' } else { $null }  # a $null argument is left out

    # 1 Spec
    $gate = Invoke-Gate '1 Spec: lirenet check' { if (Test-Path $specPath) { dotnet $Lirenet.FullName check $specPath } else { "no $specPath"; $global:LASTEXITCODE = 1 } }
    for ($try = 0; -not $gate.Passed -and $try -lt $Attempts -and (Invoke-Agent 'spec-from-app' $row.Module $row.Spec $gate); $try++) {
        $gate = Invoke-Gate '1 Spec: lirenet check' { dotnet $Lirenet.FullName check $specPath }
    }
    $gates += $gate
    if (-not $gate.Passed) { $row.Stage = '1 Spec'; $row.State = 'stuck'; $row.Waiting = 'lirenet check'; $stuck++ }
    elseif (-not (Select-String -Path $specPath -Pattern '^\| Status \| accepted' -Quiet)) {
        $questions = (Select-String -Path $specPath -Pattern '^- .*Proposal:').Count
        $row.Stage = '1 Spec'; $row.State = 'waiting'; $row.Waiting = "👤 accept $($row.Spec) ($questions open questions)"
    }
    else {
        # 4 Page objects: the build, once per loop run
        if (-not $build) { $build = Invoke-Gate '4 Page objects: build' { dotnet build $Solution.FullName -v:q -nologo } }
        $gates += $build
        if (-not $build.Passed) { $row.Stage = '4 Page objects'; $row.State = 'stuck'; $row.Waiting = 'build'; $stuck++ }
        else {
            # 5 Scenarios
            $gate = Invoke-Gate '5 Scenarios: lirenet trace' { if (Test-Path $modulePath) { dotnet $Lirenet.FullName trace $specPath $modulePath $Runnable } else { "no $modulePath"; $global:LASTEXITCODE = 1 } }
            for ($try = 0; -not $gate.Passed -and $try -lt $Attempts -and (Invoke-Agent 'scenario-from-spec' $row.Module $row.Spec $gate); $try++) {
                $build = Invoke-Gate '4 Page objects: build' { dotnet build $Solution.FullName -v:q -nologo }
                $gate = Invoke-Gate '5 Scenarios: lirenet trace' { dotnet $Lirenet.FullName trace $specPath $modulePath $Runnable }
            }
            $gates += $gate
            # Every step binds, checked once per loop with no browser opened: a suite's test with the trait Category=Bind.
            if ($gate.Passed -and $runsThis) {
                if (-not $bind) {
                    $bind = Invoke-Gate '5 Scenarios: every step binds' { dotnet test $TestProject.FullName --no-build -nologo --filter 'Category=Bind' }
                    if ($bind.Output -match 'No test matches') { $bind.Passed = $true }
                    $bind | Add-Member Summary (($bind.Output -split "`n" | Where-Object { $_ -match 'BDDB\d+|do not bind|(Passed|Failed)!|No test matches' } | Select-Object -First 3) -join ' ').Trim()
                }
                $gates += $bind
            }
            if (-not $gate.Passed) { $row.Stage = '5 Scenarios'; $row.State = 'stuck'; $row.Waiting = 'lirenet trace'; $stuck++ }
            elseif ($runsThis -and -not $bind.Passed) { $row.Stage = '5 Scenarios'; $row.State = 'stuck'; $row.Waiting = 'a step does not bind'; $stuck++ }
            else {
                # 6 Run. A runnable spec's own examples, -Runs times in a row; the module's code tests beside it once
                # per loop: they are not this spec's, and repeating them per spec and per run proves nothing more.
                $filter = if ($runsThis) { "Spec=$($row.Spec)" } else { "Module=$($row.Module)" }
                $moduleOk = $true
                if ($runsThis) {
                    if (-not $moduleTests.ContainsKey($row.Module)) {
                        $moduleResults = Join-Path $RunDir "$($row.Module)-module"
                        $moduleGate = Invoke-Gate "6 Run: the module's code tests, once" { dotnet test $TestProject.FullName --no-build -nologo --filter "Module=$($row.Module)" --logger trx --results-directory $moduleResults }
                        $summary = (($moduleGate.Output -split "`n" | Where-Object { $_ -match '(Passed|Failed)!|No test matches' }) -join ' ').Trim()
                        # A module without code tests has nothing to fail.
                        if ($summary -match 'No test matches') { $moduleGate.Passed = $true }
                        $moduleGate | Add-Member Summary $summary
                        $moduleTests[$row.Module] = $moduleGate
                    }
                    $gates += $moduleTests[$row.Module]
                    $moduleOk = $moduleTests[$row.Module].Passed
                }
                $green = 0
                # -AtOnce: the runs side by side, a process each, for a journey that only reads. They still prove the
                # same thing (three runs, each green), in the time of one.
                $sideBySide = @{}
                if ($AtOnce -and $Runs -gt 1) {
                    $project = $TestProject.FullName
                    $started = 1..$Runs | ForEach-Object {
                        $results = Join-Path $RunDir "$($row.Spec)-$_"
                        Start-Job { param($project, $filter, $results) $at = Get-Date; $out = dotnet test $project --no-build -nologo --filter $filter --logger trx --results-directory $results 2>&1 | Out-String; [pscustomobject]@{ Output = $out.Trim(); Passed = $LASTEXITCODE -eq 0; Seconds = [int]((Get-Date) - $at).TotalSeconds } } -ArgumentList $project, $filter, $results
                    }
                    $run = 0
                    foreach ($job in $started) { $run++; $done = Receive-Job $job -Wait -AutoRemoveJob; $sideBySide[$run] = [pscustomobject]@{ Name = "6 Run $run/${Runs}: dotnet test"; Passed = $done.Passed; Output = $done.Output; Seconds = $done.Seconds } }
                }
                for ($run = 1; $run -le $Runs; $run++) {
                    $results = Join-Path $RunDir "$($row.Spec)-$run"
                    $test = if ($sideBySide.ContainsKey($run)) { $sideBySide[$run] } else { Invoke-Gate "6 Run $run/${Runs}: dotnet test" { dotnet test $TestProject.FullName --no-build -nologo --filter $filter --logger trx --results-directory $results } }
                    $gate = Invoke-Gate "6 Run $run/${Runs}: every rule passing" { dotnet $Lirenet.FullName trace $specPath $modulePath $Runnable --results $results }
                    $gate.Seconds += $test.Seconds
                    $gate | Add-Member Summary (($test.Output -split "`n" | Where-Object { $_ -match '(Passed|Failed)!' }) -join ' ')
                    $gates += $gate
                    if ($gate.Passed) { $green++ } elseif (-not $AtOnce) { break }
                }
                if ($green -eq $Runs -and $moduleOk) { $row.Stage = '6 Run'; $row.State = 'done'; $row.Waiting = "green $Runs run(s), every rule passing" }
                elseif ($green -eq $Runs) { $row.Stage = '7 Triage'; $row.State = 'stuck'; $row.Waiting = "run-triage: a code test of $($row.Module) is not passing"; $stuck++ }
                else { $row.Stage = '7 Triage'; $row.State = 'stuck'; $row.Waiting = 'run-triage: a rule is not passing'; $stuck++ }
            }
        }
    }

    $report.Add("## $($row.Module) ($($row.Spec)): $($row.State), $($row.Stage)")
    $report.Add('')
    if ($row.Waiting) { $report.Add("$($row.Waiting)."); $report.Add('') }
    $report.Add('| Gate | Result | Seconds | Note |')
    $report.Add('| --- | --- | --- | --- |')
    foreach ($g in $gates) {
        $note = Get-GateNote $g
        $report.Add("| $($g.Name) | $(if ($g.Passed) { 'pass' } else { 'FAIL' }) | $($g.Seconds) | $($note -replace '\|', '/') |")
    }
    $report.Add('')
    foreach ($g in $gates | Where-Object { -not $_.Passed }) {
        $report.Add("<details><summary>$($g.Name)</summary>")
        $report.Add('')
        $report.Add('```text')
        $report.Add((($g.Output -split "`n") | Select-Object -Last 40) -join "`n")
        $report.Add('```')
        $report.Add('</details>')
        $report.Add('')
    }
}

Write-Loops $rows
$waiting = @($rows | Where-Object { $_.State -eq 'waiting' })
$report.Add('## For a person')
$report.Add('')
if ($waiting.Count -eq 0) { $report.Add('Nothing waits for a person.') }
foreach ($w in $waiting) { $report.Add("- $($w.Module): $($w.Waiting)") }
$reviewDue = Get-ReviewDue (Join-Path $Suite '.testing/600-Improvements.md') (@(Read-Loops | Where-Object State -eq 'done').Count)
if ($reviewDue) {
    $report.Add('')
    $report.Add('## Improvement review')
    $report.Add('')
    $report.Add($reviewDue)
    Write-Host $reviewDue
}
$report | Set-Content (Join-Path $RunDir 'report.md')
Write-Host "Report: $(Join-Path $RunDir 'report.md')"
exit ($stuck -eq 0 ? 0 : 1)
