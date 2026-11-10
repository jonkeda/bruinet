<#
.SYNOPSIS
    Checks every UI run script needs before it starts: no app left over from an earlier run, and
    the Bruinet submodule at the commit the app's branch expects.

.DESCRIPTION
    Import it from a run script:

        Import-Module (Join-Path $PSScriptRoot '../Brinell/tools/Scripts/RunGuards.psm1') -Force
        Stop-LeftoverApp -Name 'MyApp*'
        Test-BruinetSubmodule -RepositoryRoot (Join-Path $PSScriptRoot '..') -Path 'Brinell'
#>

<#
.SYNOPSIS
    Stops processes left over from an earlier run, by name pattern.

.DESCRIPTION
    A leftover app breaks the next run's attach, and every test of that fixture then fails for a
    reason that is not real. Returns the processes it stopped.

.PARAMETER Name
    Process name patterns, for example 'Exact.Construction*'.
#>
function Stop-LeftoverApp {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]] $Name)

    $leftovers = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)
    foreach ($process in $leftovers) {
        Write-Host "Stopping leftover $($process.ProcessName) ($($process.Id))" -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    return $leftovers
}

<#
.SYNOPSIS
    Warns when a submodule is not at the commit the parent repository records, or not checked out.

.DESCRIPTION
    A Bruinet submodule at another commit than the branch expects builds against the wrong source:
    the build fails, or worse, passes with old behaviour. Returns $true when the submodule is at the
    recorded commit. With -Strict it throws instead of warning.

.PARAMETER RepositoryRoot
    The parent repository's root.

.PARAMETER Path
    The submodule's path below the root. Default: Brinell.

.PARAMETER Strict
    Throw instead of warning.
#>
function Test-BruinetSubmodule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [string] $Path = 'Brinell',
        [switch] $Strict
    )

    $line = git -C $RepositoryRoot submodule status -- $Path 2>$null | Select-Object -First 1
    if (-not $line) {
        $message = "No submodule '$Path' in $RepositoryRoot."
    }
    else {
        # 'git submodule status' marks a missing checkout with '-', another commit with '+',
        # and a merge conflict with 'U'.
        $message = switch ($line[0]) {
            '-' { "The $Path submodule is not checked out. Run: git submodule update --init $Path" }
            '+' {
                $recorded = (git -C $RepositoryRoot ls-tree HEAD -- $Path) -split '\s+' | Select-Object -Index 2
                $actual = $line.Substring(1, 40)
                "The $Path submodule is at $($actual.Substring(0, 9)), but this branch expects $($recorded.Substring(0, 9)). " +
                "Run 'git submodule update $Path', or commit the new submodule commit."
            }
            'U' { "The $Path submodule has a merge conflict." }
            default { $null }
        }
    }

    if (-not $message) { return $true }
    if ($Strict) { throw $message }
    Write-Warning $message
    return $false
}

Export-ModuleMember -Function Stop-LeftoverApp, Test-BruinetSubmodule
