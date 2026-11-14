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

      1 Spec          specs/<Spec>.spec.md exists, passes frainet check, and its Status is accepted   👤 accepts
      4 Page objects  the suite builds
      5 Scenarios     frainet trace reaches every rule of the spec from Modules/<Module>
      6 Run           dotnet test --filter Module=<Module>, -Runs times, every rule Passing (frainet trace --results)

    Explore (2) and Controls (3) have no gate of their own here: their work shows in 4 to 6.
    A module whose gates all pass is done. A 👤 gate marks the module waiting and the loop goes on to the next one.
    With -Agent claude, a failed gate that an agent can fix runs `claude -p` with the stage's skill and the gate's
    output, then the gate again, up to -Attempts times; then the module is stuck.

    Writes .testing/100-Loops.md and .testing/runs/<yyyy-MM-dd-HHmm>/report.md (and the runs' .trx files beside it).
    Exit code 0 when every module is done or waiting for a person, 1 when one is stuck.

.PARAMETER Suite
    The suite's folder (with .testing/, specs/ and a *.slnx), such as ../Bouw7.

.PARAMETER Module
    Only this module; all modules of 100-Loops.md when empty.

.PARAMETER Agent
    none (the default: gates only, free and deterministic) or claude (Claude Code headless).

.PARAMETER Runs
    How many green runs in a row stage 6 asks (the proposal's phase gate is 3).

.PARAMETER Attempts
    Agent attempts per failed gate.

.PARAMETER AgentMinutes
    An agent attempt's time limit.

.EXAMPLE
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Module Hours -Runs 3
    pwsh tools/Scripts/site-loop.ps1 -Suite ../Bouw7 -Module Invoices -Agent claude
#>
param(
    [Parameter(Mandatory)][string]$Suite,
    [string]$Module,
    [ValidateSet('none', 'claude')][string]$Agent = 'none',
    [int]$Runs = 1,
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

# Frainet is built once and run from its output, not through dotnet run per call.
$FrainetProject = Join-Path $Bruinet 'Frainet/src/Frainet.Cli/Frainet.Cli.csproj'
dotnet build $FrainetProject -c Release -v:q -nologo | Out-Null
$Frainet = Get-ChildItem (Join-Path $Bruinet 'Frainet/src/Frainet.Cli/bin/Release') -Recurse -Filter 'Frainet.Cli.dll' | Select-Object -First 1

function Invoke-Gate([string]$Name, [scriptblock]$Command) {
    $started = Get-Date
    $output = & $Command 2>&1 | Out-String
    $passed = $LASTEXITCODE -eq 0
    [pscustomobject]@{ Name = $Name; Passed = $passed; Output = $output.Trim(); Seconds = [int]((Get-Date) - $started).TotalSeconds }
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
        $pattern = '(?m)^\|\s*' + [regex]::Escape($row.Module) + '\s*\|.*$'
        $text = [regex]::Replace($text, $pattern, "| $($row.Module) | $($row.Spec) | $($row.Stage) | $($row.State) | $($row.Waiting) |")
    }
    Set-Content $Loops $text.TrimEnd() -NoNewline
    Add-Content $Loops ''
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

$rows = Read-Loops
$build = $null
$stuck = 0
foreach ($row in $rows) {
    if ($Module -and $row.Module -ne $Module) { continue }
    $gates = @()
    $specPath = Join-Path $Suite "specs/$($row.Spec).spec.md"
    $modulePath = Join-Path $TestProject.FullName "Modules/$($row.Module)"

    # 1 Spec
    $gate = Invoke-Gate '1 Spec: frainet check' { if (Test-Path $specPath) { dotnet $Frainet.FullName check $specPath } else { "no $specPath"; $global:LASTEXITCODE = 1 } }
    for ($try = 0; -not $gate.Passed -and $try -lt $Attempts -and (Invoke-Agent 'spec-from-app' $row.Module $row.Spec $gate); $try++) {
        $gate = Invoke-Gate '1 Spec: frainet check' { dotnet $Frainet.FullName check $specPath }
    }
    $gates += $gate
    if (-not $gate.Passed) { $row.Stage = '1 Spec'; $row.State = 'stuck'; $row.Waiting = 'frainet check'; $stuck++ }
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
            $gate = Invoke-Gate '5 Scenarios: frainet trace' { if (Test-Path $modulePath) { dotnet $Frainet.FullName trace $specPath $modulePath } else { "no $modulePath"; $global:LASTEXITCODE = 1 } }
            for ($try = 0; -not $gate.Passed -and $try -lt $Attempts -and (Invoke-Agent 'scenario-from-spec' $row.Module $row.Spec $gate); $try++) {
                $build = Invoke-Gate '4 Page objects: build' { dotnet build $Solution.FullName -v:q -nologo }
                $gate = Invoke-Gate '5 Scenarios: frainet trace' { dotnet $Frainet.FullName trace $specPath $modulePath }
            }
            $gates += $gate
            if (-not $gate.Passed) { $row.Stage = '5 Scenarios'; $row.State = 'stuck'; $row.Waiting = 'frainet trace'; $stuck++ }
            else {
                # 6 Run, -Runs times in a row
                $green = 0
                for ($run = 1; $run -le $Runs; $run++) {
                    $results = Join-Path $RunDir "$($row.Module)-$run"
                    $test = Invoke-Gate "6 Run $run/${Runs}: dotnet test" { dotnet test $TestProject.FullName --no-build -nologo --filter "Module=$($row.Module)" --logger trx --results-directory $results }
                    $gate = Invoke-Gate "6 Run $run/${Runs}: every rule passing" { dotnet $Frainet.FullName trace $specPath $modulePath --results $results }
                    $gate.Seconds += $test.Seconds
                    $gate | Add-Member Summary (($test.Output -split "`n" | Where-Object { $_ -match '(Passed|Failed)!' }) -join ' ')
                    $gates += $gate
                    if ($gate.Passed) { $green++ } else { break }
                }
                if ($green -eq $Runs) { $row.Stage = '6 Run'; $row.State = 'done'; $row.Waiting = "green $Runs run(s), every rule passing" }
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
        $note = if ($g.PSObject.Properties['Summary'] -and $g.Summary) { $g.Summary } else { (($g.Output -split "`n") | Select-Object -Last 1) }
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
$report | Set-Content (Join-Path $RunDir 'report.md')
Write-Host "Report: $(Join-Path $RunDir 'report.md')"
exit ($stuck -eq 0 ? 0 : 1)
