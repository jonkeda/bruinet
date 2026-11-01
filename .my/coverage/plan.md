# Coverage of the control libraries

The control libraries are what users of Bruinet call, and about half their code runs in no test.
This plan closes that with control contracts: a test per member of each Core control interface,
written once and run on every head, then mutation testing on the same code to show the tests
would notice a break. Wpf and WinForms come last.

## Where it stands

Measured 2026-11-01 with `./tools/Scripts/coverage.ps1 -Headless -Browser` (AD-033): the unit tests,
the MAUI suites on MauiHeadless, the Html and Blazor UI suites in Chromium.

| Library | Lines | Biggest gaps (lines not run) |
| --- | --- | --- |
| Core | 78% | `CsvTestLogger` 87 (0%), `Timing` 49, `CaseInsensitiveEnumConverter` 32 (0%), `ConsoleTestLogger` 30 (0%) |
| Maui | 49% | `MauiDriverFactory` 166, `RangeControlBase` 114, `AndroidInstalledBuild` 98 (0%), `SelectorControlBase` 93, `WebView` 71 (0%), `Image` 70, `NavigationRecovery` 69 (0%), `TitleBar` 64 (0%), `Slider` 62 |
| Html | 46% | `ProgressControl` 113, `RangeControlBase` 106, `SelectorControlBase` 105, `ControlBase` 92, `TableControl` 69, `TextInputControl` 66, `RadioGroupControl` 65 (0%), `ListControl` 53 |
| Blazor | 45% | `MediaControlBase` 112, `NavMenuControl` 58, `IFrameControl` 51, `ImageControl` 46 |
| Wpf, WinForms | not measured | their suites need a desktop (`-Desktop`, local) |

Not in scope: Appium, NativeAndroid and Maui.Appium (a device or emulator); `Core.Legacy` (the
netstandard2.0 build, the same sources); Stride.

## Steps

| # | Step | Done when |
| --- | --- | --- |
| C1 | The contract kit: an abstract xUnit class per Core interface (`IRangeControlObject`, `ISelectorControlObject`, `IToggleControlObject`, `IEditableTextControlObject`, `IDateControlObject`, `ITimeControlObject`, `IProgressControlObject`, `IClickableControlObject`, `IFocusableControlObject`, `ITabControlObject`, `IExpandableControlObject`, the rest), one test per member, the control and its expected values given by the head | every member of every interface has a test; the kit builds alone |
| C2 | Html on bUnit (`Bruinet.Html.Bunit`): a page per control kind in the test site, the kit's classes bound to it | the kit green on bUnit; Html's range, selector, progress and radio group over 80% |
| C3 | Html in Chromium (`Bruinet.Html.Playwright`): the same pages and bindings on the browser head | green in CI's `-Browser`; any difference from bUnit is a bug in one head, fixed or recorded |
| C4 | Blazor: its controls (media, nav menu, iframe, image) bound the same way | Blazor over 75% |
| C5 | Maui on MauiHeadless: a page per control kind in the test app, the kit bound to it | green in CI's `-Headless`; Maui's range, selector, slider, stepper, picker over 80% |
| C6 | Mutation testing on the controls: `mutate.ps1` gains Html (with `Bruinet.Html.Bunit.Tests`) and Maui (with the MauiHeadless suite), both in-process | scores on the control bases measured; surviving mutants become tests |
| C7 | What the kit does not reach, by hand: Core's loggers and `Timing`, Maui's `MauiDriverFactory` and `NavigationRecovery` (unit tests over fakes), `WebView`, `TitleBar`, `Image` | Core over 85%, Maui over 70% |
| C8 | Wpf and WinForms, last: the kit bound to their sample apps, run locally with `-Desktop` | both measured, then over 70% |

Each step is one or more commits on `move`, the numbers after it written in the progress table below.

## Open questions

- **Where the kit lives.** Proposal: a test-support project `tests/Bruinet.ControlContracts`, not
  packed, since it proves Bruinet's own heads; packing it for users who write a head is a later choice.
- **One page per control kind, or one page with all of them.** Proposal: one page per kind, so a
  failing contract names its control and pages stay small.
- **A coverage floor in CI.** Proposal: none until C5 is done; then fail when a control library
  drops more than two points, never a fixed number.
- **Behaviour that differs by head** (a slider's step on Html versus Maui). Proposal: the head states
  it in its binding, and the kit asserts what the head states, so a difference is written down
  rather than skipped.

## Progress

| Step | State | Numbers |
| --- | --- | --- |
| Baseline | 2026-11-01 | lines 53.8%; Core 78, Maui 49, Html 46, Blazor 45 |
| C1 | built 2026-11-01: `tests/Bruinet.ControlContracts`, 16 contracts, 114 members each called; every test virtual, a head skips one with its reason; not yet bound to a head | 0 (C2 binds it) |
