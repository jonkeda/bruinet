# Scripts For An App's Test Runs

Bruinet's `tools/Scripts` folder has scripts an app repository runs on its own tests, with Bruinet
as a submodule. The app keeps a short wrapper that passes its project paths and names; the work is
here, so every app gets the same fixes.

| Script | What it does |
|---|---|
| `run-maui-headless.ps1` | Builds a UI-test project for plain `net10.0` and runs it with `BRUINET_HEAD=headless`. |
| `run-maui-android.ps1` | Builds an embedded APK and the UI tests, reinstalls the app, reverses ports, and runs on one Android device through Appium. |
| `RunGuards.psm1` | `Stop-LeftoverApp` stops app processes an earlier run left; `Test-BruinetSubmodule` warns when the submodule is missing or at another commit than the branch records. |
| `check-test-guard.ps1` | Fails when a change takes tests away, unless it gives a reason. |
| `repeat-run.ps1` | Runs a test project several times and fails when a test fails or is missing in any run. |
| `coverage-areas.ps1` | Line coverage per source area (a module folder) from a Cobertura file. |
| `mutate-folder.ps1` | Stryker.NET on one source folder of a project Stryker cannot compile, through a harness library. |

## Run guards

Every UI run script starts with both guards. A leftover app breaks the next attach, and a
submodule at the wrong commit builds against the wrong Bruinet source.

```powershell
Import-Module (Join-Path $PSScriptRoot '../Brinell/tools/Scripts/RunGuards.psm1') -Force
Stop-LeftoverApp -Name 'MyApp*' | Out-Null
Test-BruinetSubmodule -RepositoryRoot (Join-Path $PSScriptRoot '..') -Path 'Brinell' | Out-Null
```

`run-maui-headless.ps1` runs both when it gets `-AppProcess` and `-RepositoryRoot`;
`run-maui-android.ps1` runs the submodule check when it gets `-RepositoryRoot`.

## Test guard

Removing a test is not always wrong, but the change must say why. Give the reason as `-Reason`, as
the `TEST_GUARD_REASON` environment variable, or as a `Test-Guard-Reason: <why>` line in a commit
message. Test cases are counted per row, so merging facts into one theory is not a loss.

```powershell
./Brinell/tools/Scripts/check-test-guard.ps1 -Root . -Exclude 'Brinell/*' -Base origin/main -Head HEAD
```

`-Exclude` leaves out paths another pipeline guards, such as the submodule itself. In a GitHub
workflow, check out the submodule first; a private Bruinet repository needs a token secret for that.

## Repeat runs

```powershell
./Brinell/tools/Scripts/repeat-run.ps1 -Project MyApp.Tests -Framework net10.0-windows10.0.19041.0 `
    -Runs 2 -Build -VaryOrder -AppProcess @()
```

`-VaryOrder` turns xUnit collection parallelism on in odd runs and off in even runs, so tests that
depend on order show up. The summary lands in `repeat-summary.md` beside the `.trx` files.

## Coverage per area

```powershell
./Brinell/tools/Scripts/coverage-areas.ps1 -Cobertura coverage.cobertura.xml -SourceRoot 'MyApp/Modules' `
    -Area Orders, Customers -SourceFolder MyApp/Modules -Rules @{ orders = @{ Include = '^(Orders|Checkout)/' } }
```

An area is the folder of the same name unless `-Rules` maps it to `Include` and `Exclude` patterns.
A line counts once per file, however many classes list it.

## Mutation testing of a folder

```powershell
./Brinell/tools/Scripts/mutate-folder.ps1 -Root . -Source MyApp/Common/Formatters `
    -Tests MyApp.Tests/Common/Formatters -Namespace MyApp.Common.Formatters -AppNamespacePrefix 'MyApp.'
```

Tests that use the app's code outside the folder cannot compile in the harness, so they are left
out and listed. The repository's `dotnet-tools.json` must hold `dotnet-stryker`.
