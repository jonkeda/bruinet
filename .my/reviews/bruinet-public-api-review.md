# Bruinet's public API: review and proposals

Reviewed 2026-11-09 at Bruinet `f9ad9a96` (branch `move`), from the 34 `PublicAPI.Unshipped.txt` files under `src/` and spot checks in the source.
Nothing was built or run for this review.

## Outcome

The control vocabulary is the strong part: every control speaks the same `Do / Get / Is / Assert… / Wait…` verbs, returns its scope for chaining, and fails with exceptions that name the locator and the reason.
The weak part is size and sameness across packages. About a thousand types and ten thousand declared members are public, every `PublicAPI.Shipped.txt` is empty, and much of that surface is the engine's own machinery rather than what a test author uses.
The same idea also carries different names in different packages, which is what a reader moving from Html to Maui or Wpf trips over first.

Nothing is shipped yet, so every proposal below is still free. After the first `Shipped` line each one costs a major version by `VERSIONING.md`.

## What is good and should stay

- One verb set per capability interface in `Bruinet.Core.Interfaces` (`IClickableControlObject`, `IToggleControlObject`, `ISelectorControlObject`…), the same on every platform.
- Fluent returns of `TScope`, so a page object reads as one sentence.
- Typed failures: `ElementNotFoundException`, `ElementNotReadyException` with `NotReadyReason`, `ScopeNotReadyException`, `StaleElementException`, `LocatorNotSupportedException` with a suggestion.
- The public API is declared per package and checked by the analyzer, so a change to it is always visible in a diff.
- Heads are swappable behind one interface (`IHtmlDriver` with Playwright and bUnit; `IMauiDriver` with Appium, FlaUI and Headless).

## Findings

### 1. Too much is public

- `Bruinet.Bdd` declares about 160 public types. A step author needs the attributes, `BddStepDefinitionsBase`, `BddTable`, the hooks and the argument converters; the parser, binder, catalogs, config editors and call frames are the runner's.
- `Bruinet.Bdd.Runner` exposes some forty wire messages (`BddRunnerOpen`, `BddRunnerStep`, `BddRunnerStop`…) of a protocol only Bruinet's own two ends speak.
- `Bruinet.Core.Calls` (`ControlCall`, `AttemptContext`, `Poller`, `Settle`, `ScopeGate`, `Confirmer`, `ObservationLog`) is the call model's engine. It is public only because the platform packages need it.
- Every driver and element class is public (`PlaywrightHtmlElement`, `BunitHtmlElement`, `AppiumMauiElement`, `FlaUIMauiElement`, `FlaUIWpfElement`…), while the rule for tests is that they never touch a driver or an element.
- Three empty `public static class Placeholder` types ship in `Bruinet.Core` (`Attributes`, `Locators`, `Logging`).
- `Bruinet.Core.Utilities.WaitHelper.Pause(int)` is a public sleep in a framework whose rule is no sleeps.
- `Bruinet.Core.Utilities.MarkdownTable*`, `Bruinet.Core.Scraping.*` and `Bruinet.Core.Composition.*` are tooling support that every test project now sees in IntelliSense.

### 2. The same thing has different names per package

| Concept | Html / Blazor | Maui, Android, Wpf, WinForms, Stride |
| --- | --- | --- |
| Page base | `HtmlPageObjectBase<TSelf>`, `BlazorPageObjectBase<TSelf>` | `PageObjectBase<TSelf>` |
| A button | `ButtonControl<TScope>` | `Button<TScope>` |
| Control root | `ControlBase<TScope>` | `ViewBase<TScope>` (Maui, Android), `ControlBase<TScope>` (Wpf, WinForms, Stride) |
| Collections namespace | `Html.Collections` | `Maui.Containers`, `NativeAndroid.Collections` |
| Controls namespaces | grouped (`Controls.Buttons`, `Controls.Text`) | grouped in Maui and Android, flat in Wpf, WinForms, Stride |

- `ItemKey` and `ItemKeyKind` exist three times (Html, Maui, NativeAndroid), with 10, 13 and 9 members.
- `CollectionObjectBase`, `ItemObjectBase`, `AppRoot` and `ObjectBase` are redeclared per platform with the same name and a different shape.
- `IExpandCollapsePatternElement` and `LocatorExtensions` are copied between `Bruinet.Wpf` and `Bruinet.WinForms`.
- `MauiHeadless.MauiTestContext` and `Bruinet.Maui.Context.MauiTestContext` share a name; so do `MauiHeadless.Inspection.ElementNotFoundException` and Core's.

### 3. Package and namespace boundaries leak

- `Bruinet.Core` knows its platforms and its runner: `Core.Testing.MauiHead`, `WebHead`, `BrowserFactAttribute`, `DeviceFactAttribute`, and all the `Bdd…Attribute` step attributes live in Core, not in `Bruinet.Bdd`.
- `Bruinet.Core.Legacy` puts its types in `Bruinet.Core.Interfaces`, the same namespace as Core, and none of them is marked `[Obsolete]` (two `[Obsolete]` in the whole of `src/`).
- The Stride wire contract (`AutomationCommand`, `AutomationResponse`, `ElementState`, `ElementBounds`, `WindowInfo`) is declared twice, in `Bruinet.Stride` and `Bruinet.Stride.Automation`, and the two already differ (`AutomationCommand.Wait(…)` exists only on the test side).
- Heads are packaged two ways: separate assemblies for Maui and Html (`Bruinet.Maui.Appium`, `Bruinet.Html.Playwright`), namespaces inside the platform assembly for the rest (`Bruinet.Wpf.FlaUI`, `Bruinet.WinForms.FlaUI`, `Bruinet.NativeAndroid.Appium`).
- `Bruinet.Narrator.Windows` ships `Bruinet.Recording.Windows.WindowsCaptureRecorder`; `Bruinet.Uia.Contracts` ships namespace `Bruinet.Uia`.
- `Bruinet.Narrator` names its types `Bdd…` (`BddBox`, `BddSpot`, `BddVideoScript`), and holds both `SpeechSecrets` and `VoiceSecrets` with near-identical shape.

### 4. Signatures are uneven

- Time is `int? timeoutMs` in about 1,500 places and `TimeSpan` in about 70; `TimeoutSettings` is all bare `int`.
- Reads disagree on taking a timeout: `GetValue(int? timeoutMs)`, `GetPlaceholder(int? timeoutMs)` and `GetSelectedText(int? timeoutMs)` do; `GetDate()`, `GetTime()`, `GetProgress()` and every `Is…()` do not.
- `ITextControlObject` has `AssertText…` and `WaitText…` but no `GetText`; `GetText` sits on each platform's base class instead.
- `WaitText(expected)` and `WaitTextEquals(expected)` are both public on Html's `ControlBase` and do the same thing.
- `expected` is `bool?` on every `Assert…`/`Wait…` (`AssertVisible(bool? expected)`), required on the interface and defaulted to `true` on the class, and what `null` means is not in the signature.
- `IsExists()` reads badly; `IPressableControl<TScope>` is the one capability interface without the `Object` suffix.
- `LocatorStrategy` has `DataAutomationId` and `ControlTypeAndName` with no `Locator.By…` factory for either.
- The locator on an exception is `Locator` on `ElementNotReadyException` and `StaleElementException`, `LocatorInfo` plus `LocatorString` on `ElementNotFoundException`, and a string `ControlLocator` on `AssertionException`.
- `Comparison` is a flags enum with a member called `Equals`; `Bruinet.Core.Exceptions.AssertionException` collides by name with NUnit's.
- `ITestLogger` has thirteen methods of five to nine positional strings each, so every new field is a breaking change for three implementations and any consumer's own logger.

### 5. Configuration has too many doors

Html alone has `BruinetHtmlConfiguration`, `HtmlOptions`, `HtmlTestContextOptions`, `WebAppHostOptions`, `BrowserOptions` in Core, then `BlazorOptions` and `BruinetBlazorConfiguration` on top.
Maui has `BruinetMauiConfiguration`, `MauiOptions`, `MauiDriverOptions` and `MauiTestContextOptions`.
Which one a test author sets, and which wins, cannot be read from the types.

## Proposals

In the order I would do them; the first three shrink what the later ones have to touch.

| # | Proposal | Why first or later |
| --- | --- | --- |
| 1 | Make the engine internal: `Core.Calls`, the driver and element classes, `Bdd`'s parser, binder and catalogs, `Bdd.Runner` messages; grant the platform and tool assemblies `InternalsVisibleTo`. | Biggest cut for no rename; `InternalsVisibleTo` is already used between `Bdd`/`Uat` and `Recording`/`Narrator`. |
| 2 | Delete the three `Placeholder` types, `WaitHelper.Pause`, and one of `WaitText`/`WaitTextEquals`. | Free today, a major version later. |
| 3 | Mark everything in `Bruinet.Core.Legacy` `[Obsolete]` and move it to namespace `Bruinet.Core.Legacy`, or drop the package once WPF, WinForms and Stride are on the call model. | The call-model move is already planned (U0 baselines); the legacy surface should not outlive it unmarked. |
| 4 | One name per concept on every platform: `PageObjectBase<TSelf>`, `ControlBase<TScope>`, and control names without the `Control` suffix (or with it, everywhere). | The package namespace already says Html or Maui; this is the change a reader feels most. |
| 5 | Move `ItemKey`, `ItemKeyKind` and the shared half of `CollectionObjectBase`/`ItemObjectBase` to Core; put `IExpandCollapsePatternElement` and the FlaUI `LocatorExtensions` in one shared UIA package. | Removes three and two copies; `Bruinet.Uia.Contracts` is the natural home for the UIA ones. |
| 6 | A `Bruinet.Stride.Contracts` assembly for the wire types, referenced by both sides. | The two copies already differ. |
| 7 | Take platforms and the runner out of Core: step attributes to a small `Bruinet.Bdd.Abstractions` (or `Bruinet.Bdd`), `MauiHead`/`WebHead` to their platforms, `Scraping`, `Composition` and `MarkdownTable` to the tool packages. | Core becomes what a page object needs and nothing else. |
| 8 | Settle the signature rules and apply them through the `.tpl.cs` generators: every `Get…` takes `timeoutMs`; `GetText` on `ITextControlObject`; `Exists()` for `IsExists()`; one `Locator` property on every element exception; a `Locator.By…` for each strategy. | The controls are generated, so one template change fixes a platform. |
| 9 | Decide `timeoutMs` versus `TimeSpan` once and write it down. My proposal: keep `int? timeoutMs` on control verbs, where terseness matters, and use `TimeSpan` in options and settings types. | The mixed state is the only wrong answer; a full move to `TimeSpan` touches 1,500 members for little gain. |
| 10 | `ITestLogger` takes one record per event (`LogAction(in ControlEvent e)`) instead of positional strings. | Adding a field stops being a break. |
| 11 | One options type per platform that a test author sets, with the rest internal or nested under it. | Needs a design of its own; do it after 1 so fewer types are in play. |
| 12 | Before the first release, move the cleaned surface to `PublicAPI.Shipped.txt` and add `[Experimental]` to what is public but not promised (Narrator, Scraper support, Contracts). | Turns `VERSIONING.md` from intent into a check. |

## Unknown

- Which internal types the Tomorrow suites or other consumers use directly; proposal 1 needs a build of `tests/Tomorrow.App.UITests` and `tests/Tomorrow.Studio.UITests` against it to find out.
- Whether `bool? expected` with `null` meaning "skip" is relied on by data-driven steps; if so it should be documented on the interface, not removed.
- `Bruinet.Presenter`, `Bruinet.Scraper`, `Bruinet.Bdd.Workspace` and `Bruinet.Mocking.Analyzers` declare no public API file and were not reviewed; if any is packed, it needs one.
- `Bruinet.Mocking.*`, `Bruinet.Contracts`, `Bruinet.TestData` and `MauiHeadless` were read by type list only, not member by member.

## Next action

Pick which of proposals 1 to 3 to do first; they need no design and each is one Bruinet commit on `move`.
