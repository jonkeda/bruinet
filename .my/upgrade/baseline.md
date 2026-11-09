# U0: baselines before the move

Measured 2026-11-09 at 7e986a06, on the owner's Windows desktop, each suite run twice with
`dotnet test tests/Bruinet.<Stack>.UITests --no-build` after building the sample apps. Both runs of
every suite gave the same failures, so none is flaky. Every failure below is a failure *before*
the move: a ported stack must fail no more than this, and should fail less.

## Results

| Suite | Total | Failed | UAT | Run time |
| --- | ---: | ---: | --- | --- |
| `Bruinet.Wpf.UITests` | 16 | 2 | 2 of 2 pass | 2 m 53 s, 2 m 52 s |
| `Bruinet.WinForms.UITests` | 65 | 34 | 1 of 1 passes | 1 m 24 s, 1 m 24 s |
| `Bruinet.Stride.UITests` | 30 | 3 | 1 of 1 passes | 8 s, 3 s |

The plan's section 2 counted facts by grep (25, 59, 30); these are the run's own counts, theories
expanded.

## WPF: 2 failures

The two known login failures (seen in September), each waiting out its budget:

| Test | Time | Failure |
| --- | ---: | --- |
| `IsBusyTests.Login_WaitPageReady_CombinesLoadedAndNotBusy` | 15.3 s | "Page should not show busy indicator when ready" |
| `LoginTests.Login_CancelButton_ClearsFormOrNavigatesBack` | 27.7 s | "Username should be empty after cancel" |

Slow: every test takes 10 to 12 s, passing ones included, so most of the 2 m 53 s is the app
started per test (`WpfSampleFixture`), not the test's calls. The move should not be judged by run
time until the fixture shares the app; the per-test work is what to compare.

## WinForms: 34 failures, three kinds and two sequences

All read a value back wrong after setting it; the cause looks to be in `FlaUIWinFormsElement`
(the `RangeValue`, `Value` and selection patterns), not in the app. Not looked into further: U4
replaces that element.

| Kind | Tests | Example |
| --- | ---: | --- |
| TrackBar value not set | 13 (8 in `TrackBarTests`, 4 `Form_VolumeSelection_AllLevels`, `Form_VolumeTrackBar_BoundaryValues`) | `TrackBar_SetValue_SetsValueCorrectly`: expected 75, actual 50 |
| NumericUpDown reads `null` | 7 (4 `NumericUpDown_*`, `Form_Clear_ResetsAllFields`, `Form_Clear_ResetsAllControlValues`, `Form_Login_WithAllInputs`) | `NumericUpDown_SetValue_SetsNumberCorrectly`: expected 9090, actual null |
| ComboBox selected text `""` | 12 (3 `ComboBox_SelectRole_VariousRoles`, `LoginPage_CanSelectRole`, 3 `LoginPage_CanSelectMultipleRoles`, 3 `Form_RoleSelection_AllOptions`, `AdvancedLogin_DemonstratesAssertPattern`, `AdvancedLogin_CompleteWorkflow`) | `LoginPage_CanSelectRole`: expected "Admin", actual "" |
| Whole-form sequences | 2 (`Form_AllInputTypes_TogetherInSequence`, `Form_InteractionSequence_ComplexScenario`): a number differs, from the range or numeric reads above | - |

The ComboBox failures take 2.4 s each, a poll running out; the others fail at once. Slow (over
5 s, passing): `LoginPage_CanLoginWithAllRoles`, `AdvancedLogin_MultipleLogins`,
`Form_MultipleLoginAttempts`, 7.7 s each (three logins in a row).

This is the larger finding: half the WinForms suite fails today. U4's proof is these 34 turning
green on the new element, which makes `Bruinet.ControlContracts`' WinForms bindings (plan step 8)
the first thing to write in U4: they name each pattern member that does not work.

## Stride: 3 failures

| Test | Failure | Likely cause |
| --- | --- | --- |
| `CounterTests.IncrementButton_Click_IncreasesCounter` | expected 1, actual 6 | The game is shared by the suite and the counter is not reset: other tests' clicks count. An order dependence, not an increment bug. |
| `GameplayTests.Player_MoveNorth_PositionChanges` | position stays (0.0, 0.0) | The key press does not reach the player; window focus or the simulated key path. |
| `GameplayTests.Player_MoveSouth_PositionChanges` | position stays (0.0, 0.0) | The same. |

Slow: only `SettingsTests.GameplaySettings_PlayerNameInput_Exists` in the first run (5.9 s, the
game starting); the second run took 3 s in all.

## What U3 to U6 compare against

- A ported suite's failures are a subset of these; a new failure is a regression until shown
  otherwise.
- The per-test time of passing tests, not the suite's wall time (WPF's is app starts).
- The three UAT scenario files (WPF 2, WinForms 1, Stride 1) pass now and must keep passing.
