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

- **A missing element** (found in C2). Core's docs say `IsVisible`, `IsEnabled` and `GetAttribute`
  answer null; the generated Html and Maui controls answer false and throw `ElementNotFoundException`,
  Wpf and WinForms answer null. Proposal: change the docs to what the generated controls do (false,
  and a throw like every `Get*`), and Wpf and WinForms with C8; until then Html skips the three tests.
- **Html's progress, date, time and scroll controls** (found in C2) have their own APIs (`DateOnly`,
  percentages) and do not implement the Core interfaces, so no contract reaches them. Proposal: add
  the Core interface beside their own members (`GetProgress` as a fraction, `GetDate` as `DateTime`),
  then bind them; until then they wait for C7.

- **Answered 2026-11-02 (the owner took the proposal): generated code in the targets** (found in C4). Blazor's `.gen.cs` wrappers are three quarters of what no test runs;
  the generator is tested on its own (`Bruinet.Generator.Tests`). Proposal: measure the targets on hand-written code
  (`ExcludeByFile` `**/*.gen.cs` in `coverage.runsettings`), and report generated code apart.

- **Answered 2026-11-02 (the owner took the proposal): C4's and C5's targets not met** (the AI run, 2026-11-02). Blazor 58.3% (75 asked), Maui 50.9% (80 asked). Of
  Blazor's gap 193 of 240 lines are generated wrappers; of Maui's, the hand-written gap in Slider, Picker and Stepper
  is mostly platform code (UI Automation, Android) only a device head runs. Proposal: count the targets on
  hand-written code (the generated-code question above), and run the device paths in C8's desktop session, on the
  Windows head, with C5's contracts (not yet run there: `--filter "Head!=MauiHeadless"` keeps them out of a device run).
- **Answered 2026-11-02 (the owner took the proposal): the picker's start** (found in C5's review). The contract needs a chosen item at the start; the sample's picker
  starts with none, so the binding chooses one with `SelectByIndex` first. Proposal: the sample's view model starts with
  an item chosen, and a separate check that an unchosen picker reads -1 and null.

## Progress

| Step | State | Numbers |
| --- | --- | --- |
| Baseline | 2026-11-01 | lines 53.8%; Core 78, Maui 49, Html 46, Blazor 45 |
| C1 | built 2026-11-01: `tests/Bruinet.ControlContracts`, 16 contracts, 114 members each called; every test virtual, a head skips one with its reason; not yet bound to a head | 0 (C2 binds it) |
| C2 | built 2026-11-01: 10 bindings in `Bruinet.Html.Bunit.Tests/Contracts` (element, clickable, focusable, text, text input, text area, check box, range input, select, radio group); 91 tests, 81 pass, 10 skipped with why (hover and long press, focus: no pointer or script on bUnit; a missing element: open question). Found: bUnit's Click on a radio or check box sent no change, so RadioGroupControl could not select (fixed in BunitHtmlElement) | Html 46.3 to 57.6%; RadioGroup 0 to 72, Selector 27 to 69, Range 26 to 48, Clickable 37 to 86, TextInput 51 to 76, Label, CheckBox, Toggle 100. Under 80% on range and selector: their generated members beyond the interface (`WaitValueGreaterThan`, ...) |
| C3 | built 2026-11-01: the pages and bindings moved to `tests/Bruinet.ControlContracts.Html` (bindings generic over the head), served by `tests/Bruinet.ControlContracts.Html.Site`; `Bruinet.Html.UITests/Tests/Contracts` runs them in Chromium: 88 pass, 3 skipped (a missing element). Hover, long press and focus run here. No difference from bUnit but layout: an empty span with no size is not visible in a browser (the page sizes it). Html.UITests 27 to 118 tests, 17 s to 1 m 20 s | Html 57.6 to 59.1%; Clickable 86 to 98, Focusable 100 |
| C4 | built 2026-11-01 (the AI run): `Pages/BlazorControls.razor` (image, nav menu, iframe, audio over `media/tone.wav`) and `BlazorControlsBinding` in `Bruinet.ControlContracts.Html`, on bUnit (5 pass, 4 skipped: script, layout, media) and in Chromium (9 pass). Found: `NavMenuControl` named the first item active whatever was active (its selector list took the state on the last part only); fixed. Measured with `coverage.ps1 -Projects Bruinet.Blazor.Tests, Bruinet.Html.Bunit.Tests -Browser` | Blazor 45 to 58.3%: under the 75% asked. Of the 240 lines not run, 193 are generated `.gen.cs` wrappers (`Wait*`, `Assert*`, `Is*` of members whose `.tpl.cs` logic runs); the hand-written `.tpl.cs` code misses 8 lines. Not met: see the open question on generated code |
| C5 | built 2026-11-02 (the AI run): `Bruinet.Maui.UITests/Tests/Contracts/MauiContracts.cs` binds the kit to the sample app's own pages: slider and stepper (range), check box and switch (toggle), entry (editable text and focus), picker (selector); 59 tests, all pass on MauiHeadless; the whole suite 387 pass, 33 skipped. Found: the sample's slider started at 1, not 50 (`Value` set before the bounds; MAUI clamps); fixed in `RangeView.xaml`, one slider test that relied on it corrected. Not bound: clickable (no page has a disabled button or reports the kind of click), date, time, progress and label (no Core interface on Maui, as on Html): left to C7. Not run on the Windows head (it drives the desktop) | Maui 49 to 50.9%: under the 80% asked. The hand-written code left in `Slider` (40 of 57 lines), `Picker` (30 of 69) and `Stepper` is mostly platform code (UI Automation, Android) that only a device head runs; MauiHeadless runs the shared logic. Not met: see the open question on device paths |
