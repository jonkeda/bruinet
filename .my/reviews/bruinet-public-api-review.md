# Bruinet's public API: review and proposals

First written 2026-11-09 at `f9ad9a96`. Brought up to date 2026-11-10 at `7f972d8c`, after the night's run moved WPF, WinForms and Stride onto the call model (U1 to U7).
Read from the 34 `PublicAPI.Unshipped.txt` files under `src/` and spot checks in the source; nothing was built or run for this review.

## Outcome

The control vocabulary is the strong part: every control speaks the same `Do / Get / Is / Assert… / Wait…` verbs, returns its scope for chaining, and fails with exceptions that name the locator and the reason.
After last night all six stacks have the same shape, so most of the naming differences between packages are gone and Html and Blazor are now the odd ones out.
What remains is size and copies: about ten and a half thousand declared members are public, every `PublicAPI.Shipped.txt` is empty, and the per-stack base types exist six times.

Nothing is shipped yet, so every proposal below is still free. After the first `Shipped` line each one costs a major version by `VERSIONING.md`.

## What last night changed

- `Bruinet.Core.Legacy` is deleted; every stack is on the call model. Proposal 3 is overtaken.
- `Bruinet.Core.Calls` is public by the owner's decision (AD-010 as amended, Q5). That part of proposal 1 is decided against and withdrawn.
- Core has `IScope` and `ITestPage` in `Bruinet.Core.Scopes`; each stack's scope and page interface derives from them.
- WPF, WinForms and Stride have `PageObjectBase<TSelf>`, `ViewBase<TScope>`, grouped control namespaces, containers and collections, as Maui and native Android do.
- `Bruinet.FlaUI` holds what the FlaUI stacks share (`DesktopApp`, `UiaElementBase`, `LocatorExtensions`); `IExpandCollapsePatternElement` is gone. The UIA half of proposal 5 is made.
- The Stride wire types exist once, in `Bruinet.Stride.Communication`; the server uses the client's. Proposal 6 is made in substance, without a package of its own.
- The consumer's side of each of these is a row under Made in `.github/skills/api-upgrade/references/changes.md`.

## What is good and should stay

- One verb set per capability interface in `Bruinet.Core.Interfaces` (`IClickableControlObject`, `IToggleControlObject`, `ISelectorControlObject`…), the same on every platform.
- Fluent returns of `TScope`, so a page object reads as one sentence.
- Typed failures: `ElementNotFoundException`, `ElementNotReadyException` with `NotReadyReason`, `ScopeNotReadyException`, `StaleElementException`, `LocatorNotSupportedException` with a suggestion.
- The public API is declared per package and checked by the analyzer, so a change to it is always visible in a diff.
- Heads are swappable behind one interface per stack, and the six stacks now share one model.

## Findings that still hold

### 1. Too much is public

- `Bruinet.Bdd` declares about 160 public types. A step author needs the attributes, `BddStepDefinitionsBase`, `BddTable`, the hooks and the argument converters; the parser, binder, catalogs, config editors and call frames are the runner's.
- `Bruinet.Bdd.Runner` exposes some forty wire messages (`BddRunnerOpen`, `BddRunnerStep`, `BddRunnerStop`…) of a protocol only Bruinet's own two ends speak.
- Every driver and element class is public (`PlaywrightHtmlElement`, `BunitHtmlElement`, `AppiumMauiElement`, `FlaUIMauiElement`, `FlaUIWpfElement`, `FlaUIWinFormsElement`, `StrideElement`), while the rule for tests is that they never touch a driver or an element.
- Three empty `public static class Placeholder` types still ship in `Bruinet.Core` (`Attributes`, `Locators`, `Logging`).
- `Bruinet.Core.Utilities.WaitHelper.Pause(int)` is still a public sleep in a framework whose rule is no sleeps.
- `Bruinet.Core.Utilities.MarkdownTable*`, `Bruinet.Core.Scraping.*` and `Bruinet.Core.Composition.*` are tooling support that every test project sees in IntelliSense.

`Bruinet.Core.Calls` is no longer on this list: it is public on purpose, so a custom stack can stand on it.

### 2. Html and Blazor are named differently from the other five

| Concept | Html / Blazor | Maui, Android, Wpf, WinForms, Stride |
| --- | --- | --- |
| Page base | `HtmlPageObjectBase<TSelf>`, `BlazorPageObjectBase<TSelf>` | `PageObjectBase<TSelf>` |
| A button | `ButtonControl<TScope>` | `Button<TScope>` |
| Control root | `ControlBase<TScope>` | `ViewBase<TScope>` |

- Maui keeps its collections in `Maui.Containers`; the other five use `.Collections`.
- Stride's image control is `ImageElement<TScope>`; the others call theirs `Image` or `ImageView`.
- `MauiHeadless.MauiTestContext` and `Bruinet.Maui.Context.MauiTestContext` share a name; so do `MauiHeadless.Inspection.ElementNotFoundException` and Core's.

### 3. The per-stack bases are copied six times

- `ItemKey` and `ItemKeyKind` are declared in six packages (three before last night), Maui's with two more members than the rest.
- `CollectionObjectBase`, `AppRoot` and `ObjectBase` are declared in the same six.
- The port made the stacks alike by copying, which was the plan's choice; it also means the next change to an item key or a collection is six edits.

### 4. Package and namespace boundaries leak

- `Bruinet.Core` knows its platforms and its runner: `Core.Testing.MauiHead`, `WebHead`, `BrowserFactAttribute`, `DeviceFactAttribute`, and all the `Bdd…Attribute` step attributes live in Core, not in `Bruinet.Bdd`.
- Namespace `Bruinet.Stride.Automation` is now in two assemblies: the client's `StrideAutomationDriver` and `StrideElement` in `Bruinet.Stride`, the server's types in `Bruinet.Stride.Automation`.
- Heads are packaged two ways: separate assemblies for Maui and Html (`Bruinet.Maui.Appium`, `Bruinet.Html.Playwright`), namespaces inside the platform assembly for the rest (`Bruinet.Wpf.FlaUI`, `Bruinet.WinForms.FlaUI`, `Bruinet.NativeAndroid.Appium`).
- `Bruinet.Narrator.Windows` ships `Bruinet.Recording.Windows.WindowsCaptureRecorder`; `Bruinet.Uia.Contracts` ships namespace `Bruinet.Uia`.
- `Bruinet.Narrator` names its types `Bdd…` (`BddBox`, `BddSpot`, `BddVideoScript`), and holds both `SpeechSecrets` and `VoiceSecrets` with near-identical shape.

### 5. Signatures are uneven

- Time is `int? timeoutMs` in about 1,800 places and `TimeSpan` in about 80; `TimeoutSettings` is all bare `int`.
- Reads disagree on taking a timeout: `GetValue(int? timeoutMs)`, `GetPlaceholder(int? timeoutMs)` and `GetSelectedText(int? timeoutMs)` do; `GetDate()`, `GetTime()`, `GetProgress()` and every `Is…()` do not.
- `ITextControlObject` has `AssertText…` and `WaitText…` but no `GetText`; `GetText` sits on each platform's base class instead.
- `WaitText(expected)` and `WaitTextEquals(expected)` are both public on Html's `ControlBase` and do the same thing.
- `expected` is `bool?` on every `Assert…`/`Wait…` (`AssertVisible(bool? expected)`), required on the interface and defaulted to `true` on the class, and what `null` means is not in the signature.
- `IsExists()` reads badly; `IPressableControl<TScope>` is the one capability interface without the `Object` suffix.
- `LocatorStrategy` has `DataAutomationId` and `ControlTypeAndName` with no `Locator.By…` factory for either.
- The locator on an exception is `Locator` on `ElementNotReadyException` and `StaleElementException`, `LocatorInfo` plus `LocatorString` on `ElementNotFoundException`, and a string `ControlLocator` on `AssertionException`.
- `Comparison` is a flags enum with a member called `Equals`; `Bruinet.Core.Exceptions.AssertionException` collides by name with NUnit's.
- `ITestLogger` has thirteen methods of five to nine positional strings each, so every new field is a breaking change for three implementations and any consumer's own logger.

### 6. Configuration has too many doors

Html alone has `BruinetHtmlConfiguration`, `HtmlOptions`, `HtmlTestContextOptions`, `WebAppHostOptions`, `BrowserOptions` in Core, then `BlazorOptions` and `BruinetBlazorConfiguration` on top.
Maui has `BruinetMauiConfiguration`, `MauiOptions`, `MauiDriverOptions` and `MauiTestContextOptions`.
Which one a test author sets, and which wins, cannot be read from the types.

## Proposals

The numbers are the first version's, because the api-upgrade skill's Planned rows cite them.

| # | Proposal | State |
| --- | --- | --- |
| 1 | Make the engine internal: the driver and element classes, `Bdd`'s parser, binder and catalogs, `Bdd.Runner` messages. | Open. `Core.Calls` is out of it: public by decision. |
| 2 | Delete the three `Placeholder` types, `WaitHelper.Pause`, and one of `WaitText`/`WaitTextEquals`. | Open; free today. |
| 3 | Mark `Bruinet.Core.Legacy` obsolete or drop it. | Done: deleted in `7d03c22b`. |
| 4 | One name per concept: `PageObjectBase<TSelf>`, one control root, control names with or without `Control` everywhere. | Half done: five stacks agree. Left: Html and Blazor, and `Maui.Containers`. Since five say `ViewBase`, Html's `ControlBase` is now the one to rename, not the reverse. |
| 5 | Move `ItemKey`, `ItemKeyKind` and the shared half of the collection bases to Core; one shared UIA package. | UIA half done (`Bruinet.FlaUI`). The Core half is more urgent than before: six copies now. |
| 6 | One declaration of the Stride wire types. | Done in `97616d64`, in `Bruinet.Stride.Communication`. Left: the split `Bruinet.Stride.Automation` namespace. |
| 7 | Take platforms and the runner out of Core: step attributes, `MauiHead`/`WebHead`, `Scraping`, `Composition`, `MarkdownTable`. | Open. |
| 8 | Settle the signature rules and apply them through the `.tpl.cs` generators. | Open; all six stacks are generated now, so one rule reaches all of them. |
| 9 | Decide `timeoutMs` versus `TimeSpan` once. My proposal: `int? timeoutMs` on control verbs, `TimeSpan` in options and settings types. | Open. |
| 10 | `ITestLogger` takes one record per event instead of positional strings. | Open. |
| 11 | One options type per platform that a test author sets. | Open; needs a design of its own. |
| 12 | Before the first release, move the cleaned surface to `PublicAPI.Shipped.txt` and mark `[Experimental]` what is public but not promised. | Open. |

## Unknown

- Which driver, element or `Bdd` types consumers use directly; proposal 1 needs a build of Tomorrow's two UI suites against it to find out.
- Whether `bool? expected` with `null` meaning "skip" is relied on by data-driven steps; if so it should be documented on the interface, not removed.
- `Bruinet.Presenter`, `Bruinet.Scraper`, `Bruinet.Bdd.Workspace` and `Bruinet.Mocking.Analyzers` declare no public API file and were not reviewed; if any is packed, it needs one.
- `Bruinet.Mocking.*`, `Bruinet.Contracts`, `Bruinet.TestData`, `Bruinet.FlaUI` and `MauiHeadless` were read by type list only, not member by member.
- The new WPF, WinForms and Stride surfaces were checked for the points above, not reviewed member by member.

## Next action

Proposal 2 is still one commit with no design. After it, the Core half of proposal 5 is the one last night made more pressing.
