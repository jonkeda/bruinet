# The MauiHeadless Head

A MAUI suite can run its page objects, controls and feature files without a device: MauiHeadless renders
the app's pages in the test process, and Bruinet drives them through the same `IMauiDriver`. A run picks
its head with `BRUINET_HEAD`; the device heads (FlaUI on Windows, Appium on Android and iOS) stay the
judge at every gate. The decision is
[AD-032](../../.docs/decisions/ad-032-a-maui-suite-runs-without-a-device.md).

## Which Head When

MauiHeadless is the fast loop, not the judge. It runs a MAUI suite in seconds, with no app built, no
window and no emulator, on Windows, macOS or Linux: Bruinet's device suite runs 328 tests in about
20 s, and the Todo sample's 42 scenarios in about 40 s. But it has no layout, no pixels, no
virtualization and no platform input. A button a layout hides, a row scrolled half off screen, or a page
under a push can pass headless and fail on a device.

| Run | When | Typical time |
| --- | --- | --- |
| **MauiHeadless** | every change while you work; the first stage of CI | seconds |
| **Windows** (FlaUI) | every merge: the gate | about 2 min for Bruinet's MAUI suite, under 1 min for a small app |
| **Android** (Appium) | nightly, before a release, and when a change touches lists, navigation, gestures, layout or Android code | minutes per test on an emulator |
| **iOS** (Appium) | before a release, on a Mac | as Android |

- **Run Windows before you merge, every time.** It judges two things headless cannot:
  - **the app on the platform:** rendering, layout and the platform's own controls. In one app on
    2026-10-31 it found buttons that rendered empty, which had passed headless.
  - **Bruinet's driver for the platform:** in the same app, most of what the device runs found were
    faults in the Windows and Android drivers (row keys, a page kept after a push, partly visible rows),
    not in the app. A driver fault fails a gate as surely as an app fault, so the gate runs the driver
    your tests depend on.
- **Pick Android runs by what a change reaches.** A list, a gesture or a navigation change runs its
  tests on the emulator; [test selection](test-selection.md) finds them. Everything runs nightly.
- **When a device run finds a difference between the heads,** fix it in Bruinet, or add it to
  [Differences Between The Heads](#differences-between-the-heads) with a test that pins it
  (`[DeviceFact("why")]`). Over time headless can be trusted further, and device runs find less that is
  new.
- **A gate never passes on headless alone.**

The decision and its evidence are in
[AD-032](../../.docs/decisions/ad-032-a-maui-suite-runs-without-a-device.md).

## Running It

```bash
BRUINET_HEAD=headless dotnet test tests/Bruinet.Maui.UITests
./tools/Scripts/test-maui-headless.ps1
```

The script runs the device suite and both Todo suites, a `.trx` each; CI runs it on `windows-latest`
and `macos-latest`. Unset, or any value but `headless`, is the device. Off Windows the suites build for
plain `net10.0` without FlaUI (`BruinetWindowsHead` in `Directory.Build.props`); run
`dotnet workload install maui` once there. More in [Running MAUI tests](../run/MAUI.md).

## Giving A Suite A MauiHeadless Head

1. **Give the app a plain `net10.0` target**, beside its platform ones, and reference the app from the
   test project pinned to it. The `net10.0` build is a library the test process loads, without
   `Platforms\`, `MauiProgram.cs` or the packaged assets. Add the Windows target only on Windows, so
   the app restores on a Mac:

   ```xml
   <TargetFrameworks>net10.0-android;net10.0-ios;net10.0</TargetFrameworks>
   <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net10.0-windows10.0.19041.0;$(TargetFrameworks)</TargetFrameworks>
   <OutputType Condition="'$(TargetFramework)' != 'net10.0'">Exe</OutputType>
   ...
   <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
     <Compile Remove="MauiProgram.cs;Platforms\**" />
     <MauiXaml Remove="Platforms\**" />
   </ItemGroup>
   <!-- MauiIcon, MauiSplashScreen, MauiImage, MauiAsset under Condition="'$(TargetFramework)' != 'net10.0'" -->
   ```

   ```xml
   <ProjectReference Include="..\MyApp\MyApp.csproj" SetTargetFramework="TargetFramework=net10.0" />
   ```

   The samples do this: `samples/Bruinet.Samples.Maui.App`, `...ShellApp` and
   `samples/Todo/src/Bruinet.Samples.Todo.App`. An app that would rather keep its own project file
   untouched can instead build the `net10.0` library in a project beside it that links the app's
   sources and XAML, with the same things left out.
2. **Compose the app once.** Move the registrations out of `MauiProgram` into a method both call, as
   the Todo app's `TodoAppServices.AddTodoApp`, so the headless app is the device app.
3. **Override `CreateHeadlessOptions()`** on the suite's `MauiTestFixtureBase`:

```csharp
protected override MauiTestContextOptions CreateHeadlessOptions()
{
    // Debug parses XAML at run time and knows only loaded assemblies: load what the builder would.
    _ = typeof(CommunityToolkit.Maui.Layouts.StateContainer).Assembly;
    return MauiHeadlessHead.Options(
        services => services.GetRequiredService<AppShell>(),           // the first page
        options => options.Application = services => new App(...),     // the app's Application
        context => context.Services.AddMyApp(settings, platform));      // the app's composition
}
```

Page objects, controls and scenarios need no change. The head reports `MauiPlatform.Headless` and
otherwise answers as Windows does: WinUI's ids for the back button and title, off-screen elements in the
tree. A page object that asks `Platform == Windows` for a reason that also holds headless says
`Platform is MauiPlatform.Windows or MauiPlatform.Headless`.

An app that calls Essentials directly (`Preferences.Default`, `FileSystem.AppDataDirectory`,
`DeviceInfo.Idiom`) installs MauiHeadless's before it starts:
`HeadlessEssentials.Install(new HeadlessEssentialsOptions { AppDataDirectory = ..., AppName = ... })`.
Platform behaviors such as the toolkit's `TouchBehavior` are removed from pages, content views, list rows
and ListView cells; a control of the app's own that adds one and has its own implicit style is named in
`options.BehaviorHosts`, as Exact Construction names its `ClickableContainer`.

## What Needs A Device

Mark a test `[DeviceFact("why")]` (or `[WindowsOnlyFact(Device = "why")]`) and a scenario `@device`: on
the MauiHeadless head they are skipped with the reason, so the run still lists them. A device-only member
ends its call at once with `DeviceOnlyException` naming the member.

| Needs a device | Why |
| --- | --- |
| Layout: bounds, scroll offsets (`ReadScrollPosition`), what is below the fold | No layout; elements report 1 x 1 bounds |
| Pixels: screenshots, recordings | Nothing is drawn |
| Platform input: hover, right-click, free swipes, the clipboard | No platform |
| Controls the platform draws: a picker's popup, MediaElement's transport controls | Not in MAUI's tree |
| `#if ANDROID`, `#if IOS` code | Not compiled into a plain `net10.0` build |
| Tests of FlaUI, Appium or the UI Automation bridge themselves | Not in the process |

## What Behaves Differently

- **Visible** means on the shown page of the driver's window, with no `IsVisible="False"` ancestor. An
  off-screen carousel card is found and is not shown; scrolling succeeds at once.
- **A list's end asks for more**, as a device's does when scrolled there: a search or `ScrollToEnd` past
  the last row lets the app's work finish (a first page still loading), then raises the list's
  `RemainingItemsThresholdReached` when it pages (`RemainingItemsThreshold` 0 or more). New rows are a
  step forward; none is the end of the list.
- **A tap goes where a touch would.** It runs the tapped view's recognizers, else those of the single
  content it wraps (a ContentView or Border filled by one child), else those of the nearest ancestor up
  to the page. A tap inside a list row that nothing above takes selects the row, and a ListView cell
  raises `ItemTapped`.
- **The app runs only when a call lets it.** Each call drains MauiHeadless's dispatcher queue, so a control's
  wait (`Assert*`, `WaitArrived`) is what moves the app on. A wait outside the driver must let it run (the
  Todo fixture's reset passes `whileWaiting: IsIdle`), and a fixture lets the last test's work settle
  before the next (`WaitForQuiet`).
- **The virtual clock does not move under the head.** App timers and `DispatchDelayed` work fire only
  when a test advances MauiHeadless's clock; a test that needs them is a device test for now.
- **Fields** behave as the platform's do: text typed into a field with a `TextTransform` is held and
  shown transformed, and an app's own `Focus()` and `Unfocus()` move focus (MAUI asks the platform, and
  without one does nothing: MauiHeadless answers its `FocusChangeRequested`).
- **A pull** (`PullToRefresh`, a swipe down) on a view that keeps a `RefreshView` inside, however deep,
  pulls that one, as on Windows; a view holding two refuses, naming them.
- **Gestures and toolkit state** go to the app's own verb sinks (`GestureAutomation.Sink` in
  `Bruinet.Maui.AppSupport`), called in process: the half of the Windows bridge that lives in the app,
  without UI Automation.
- **Dialogs**: `DisplayAlertAsync`, `DisplayPromptAsync` and `DisplayActionSheetAsync` show a `TestDialog`
  over the page; its buttons answer the app's await. A back closes the open dialog first, as a device's
  back key does (`TestDialog.Dismiss`: an alert answers false, a prompt null), then a modal, then a page.
  A dialog raised by a page a modal covers is still open.
- **The active dialog** (`ContentDialog<T>`) is the open alert, prompt or action sheet, else the top
  modal page when its type is in the context's `DialogPages` (`options.DialogPages.Add(typeof(PopupPage))`
  in `MauiHeadlessHead.Options`' `configure`). That is the page a platform draws as a popup: Mopups' and
  an app's own popup pages, which are WinUI popups on Windows. Its buttons are found by their label's
  text and its field by `PromptInput`, and an alert raised while it is open is on top of it.
- **Between tests** `ResetAppState` (or `MauiHeadlessDriver.ClearWindow`, which says what it cleared)
  takes focus away, dismisses every dialog, pops the modals and goes back to the navigation root. The
  app's services and data stay: those are the fixture's. A fixture that logs a non-empty `ClearWindow`
  finds the test that left something open.
- **Locators** answer every strategy a page object uses that needs no layout, `ControlTypeAndName`
  (`new Locator("Button", "Yes")`) included, with the type words of `ControlType`.
- **Shell and navigation chrome** (tab bar, flyout, back button, title) are nodes in the tree, found under
  the Windows ids.
- **Animations** end at once at their final value.
- **A file the app polls** (a reset or network-state file) is read in the test's process: write it with a
  short retry, as Windows refuses to replace a file another handle has open.

## Differences Between The Heads

Each difference is fixed in Bruinet or known and pinned by a test. Found with a Kofflard app on
Windows, Android 16 (emulator) and MauiHeadless; iOS from how XCUITest reports its tree.

| Difference | Windows | Android | iOS | MauiHeadless | Status |
| --- | --- | --- | --- | --- | --- |
| A grouped list's `PositionInSet` | restarts per group | none | none | none | Fixed: repeats are noticed (`ListAndPageRootPinTests`) |
| A row partly on screen | its content is in the tree, not visible | its content is left out | as Windows | all rows visible | Fixed: a search moves past it |
| An item above the viewport | in the tree only when realized | left out | as Windows | always in the tree | Fixed: a search scrolls to the top once |
| A page navigated away from | answers for about a second after leaving the tree | leaves at once | leaves after the push animation | leaves at once | Fixed: a page root must be attached |
| Sticky group headers | cover the rows under them, which stay "on screen" | not pinned | not pinned by default | no layout | Known: turn them off in the app (`ItemsStackPanel.AreStickyGroupHeadersEnabled`) |
| A button's text with `SemanticProperties.Description` | the description | the text | the description | the text | Known: read both, or assert what the app shows |
| A modal page as `ContentDialog` | only a WinUI popup (Mopups, a platform popup) | as Windows | as Windows | a modal page whose type is in `DialogPages` | Known: register the app's popup page types (`PopupPageDialogTests`) |
| A list's next page (`RemainingItemsThresholdReached`) | missed after a jump to the end (a stale last-visible index) | not tried | not tried | raised at the list's end | Fixed: at a list's end Windows nudges it, and a search waits a moment for the page (`PagedListTests`) |
| Rows added at a list's end | the list jumps to its top (`ItemsUpdatingScrollMode` `KeepItemsInView`) | not tried | not tried | no scrolling | Known: a paging list sets `KeepScrollOffset` |
| The Shell title | `title` | `title` (mapped) | `title` (mapped) | `title` (mapped) | Fixed |

## MauiHeadless On Its Own

MauiHeadless (`src/MauiHeadless`) is also a component-testing framework for MAUI without Bruinet, on xUnit,
NUnit or MSTest:

```csharp
using var ctx = new MauiTestContext();
ctx.Services.AddSingleton<ISettingsService, PreferencesSettingsService>();
ctx.Preferences.Set("name", "Ada");

var page = ctx.Render<SettingsPage>();
page.Find<Entry>("Name").Should().HaveText("Ada");
page.Find<Entry>("Name").SetText("Grace");
page.Find<Button>("SaveButton").Click();

ctx.AdvanceTime(TimeSpan.FromSeconds(1));          // timers and delayed work, in order
page.Find<TestDialog>().Choose("OK");               // a DisplayAlertAsync, answered
```

`Render` takes a page type, an instance or a Shell; `Services` is fixed after the first render
(`Replace` swaps a registration for a fake); `Preferences` and `SecureStorage` are in memory;
`ToSnapshot()` gives the tree as indented text. Its examples are `tests/MauiHeadless.Tests`.

## How MAUI Runs Without A Platform

What MauiHeadless relies on, from MAUI 10 on plain `net10.0`:

- Controls, layouts, bindings, XAML, `NavigationPage`, modals and `Shell.GoToAsync` work with no
  handler. Bindings need a dispatcher, so MauiHeadless installs one per process.
- Appearing and Disappearing are raised only for a page in a `Window` whose parent is an `Application`:
  each render is a window opened through the internal `Application.AddWindow`.
- `Shell.Current` reads `Application.Current`, which MauiHeadless sets to the context's app while its work
  runs, under a lock.
- A Shell's `ContentTemplate` page is built on request (`GetOrCreateContent`) over the nearest handler's
  services, so a Shell root gets a no-op `HeadlessHandler` carrying the test's services. A `NavigationPage`
  must never get one: with a handler, a push waits for the platform.
- `CollectionView` has no item views without a handler: MauiHeadless realizes every item through its
  template, a grouped list's group headers and footers around each group, and a `ListView`'s cells.
- `PlatformBehavior.OnLoaded` reads `Handler.PlatformView` without a null check, so a page carrying one
  throws when it joins a window. MauiHeadless's implicit styles strip them first (`HeadlessBehaviors`), in a
  merged dictionary of their own so an app's theme switch does not clear them. A behavior with a
  `Command` becomes a `TapGestureRecognizer` with it.
- `MainThread` and the Essentials have no plain `net10.0` implementation; MAUI's internal setters route
  `MainThread` to the context's dispatcher and install `HeadlessEssentials`.
- Tree reads run as the UI thread would, with the app current: a row's template may look up the app's
  resources while it is made.
- Taps (`SendTapped`), focus (the `IsFocused` key), bindings and pending dialogs (`_pendingActions`) are
  internal: MauiHeadless reaches them by reflection in one place, `MauiInternals`.
- Any animation asks for an `IAnimationManager`; MauiHeadless registers MAUI's own over a ticker that reports
  animations off.
- A picker selects by equality, so MauiHeadless refuses to pick an item equal to an earlier one, as the
  Windows bridge does.
