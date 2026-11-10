# Bruinet's public API: review and proposals

First written 2026-11-09 at `f9ad9a96`. Brought up to date 2026-11-10 at `7f972d8c`, after the night's run moved WPF, WinForms and Stride onto the call model (U1 to U7); proposal 13 added the same day at `6e23ddea`.
Proposals 2 and 5's Core half made, and proposal 13's check added, the same day (`0eee767f`, `24e3dcea` and `73cf06fa`).
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
- `Bruinet.Core.Utilities.MarkdownTables` is tooling support that every test project sees in IntelliSense; `Scraping` has moved to the Scraper, and `Composition` turned out to be test composition every stack uses.

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

- `Bruinet.Core` knew its platforms and its runner. The step attributes and the two fact attributes have moved out; `MauiHead` and `WebHead` stay, as the run's head setting that platform-neutral code reads.
- Namespace `Bruinet.Stride.Automation` is now in two assemblies: the client's `StrideAutomationDriver` and `StrideElement` in `Bruinet.Stride`, the server's types in `Bruinet.Stride.Automation`.
- Heads are packaged two ways: separate assemblies for Maui and Html (`Bruinet.Maui.Appium`, `Bruinet.Html.Playwright`), namespaces inside the platform assembly for the rest (`Bruinet.Wpf.FlaUI`, `Bruinet.WinForms.FlaUI`, `Bruinet.NativeAndroid.Appium`).
- `Bruinet.Narrator.Windows` ships `Bruinet.Recording.Windows.WindowsCaptureRecorder`; `Bruinet.Uia.Contracts` ships namespace `Bruinet.Uia`.
- `Bruinet.Narrator` names its types `Bdd…` (`BddBox`, `BddSpot`, `BddVideoScript`), and holds both `SpeechSecrets` and `VoiceSecrets` with near-identical shape.

### 5. Signatures are uneven

- Time was `int? timeoutMs` in about 1,800 places and `TimeSpan` in about 80, with `TimeoutSettings` all bare `int`; proposal 9 settled it: verbs in `int? timeoutMs`, options and settings in `TimeSpan`.
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
| 1 | Make the engine internal: the driver and element classes, `Bdd`'s parser, binder and catalogs, `Bdd.Runner` messages. | Done: drivers and elements internal (`5a3234b4`; `MauiHeadlessDriver` and `StrideGameDriver` stay public); `Bdd`'s engine and the runner messages marked `[Experimental("BRUINET010")]` instead, the owner's choice, since five of Bruinet's own packages use them. `Core.Calls` is out of it: public by decision. |
| 2 | Delete the three `Placeholder` types, `WaitHelper.Pause`, and one of `WaitText`/`WaitTextEquals`. | Done in `24e3dcea`: `WaitHelper` internal, `WaitTextEquals` gone from all six stacks, `ITextControlObject` declares `WaitText`. |
| 3 | Mark `Bruinet.Core.Legacy` obsolete or drop it. | Done: deleted in `7d03c22b`. |
| 4 | One name per concept: `PageObjectBase<TSelf>`, one control root, control names with or without `Control` everywhere. | Done as far as decided: Html's page base is `PageObjectBase` and its control root `ViewBase`; Blazor keeps `BlazorPageObjectBase` on top of it; Html and Blazor keep the `Control` suffix (owner, 2026-11-10). Left: `Maui.Containers` versus `.Collections`, Stride's `ImageElement`, and the two `MauiHeadless` name clashes. |
| 5 | Move `ItemKey`, `ItemKeyKind` and the shared half of the collection bases to Core; one shared UIA package. | UIA half done (`Bruinet.FlaUI`). `ItemKey` and `ItemKeyKind` are Core's (`Bruinet.Core.Collections`); each stack's key reader is internal. Left: the shared half of `CollectionObjectBase`, `AppRoot` and `ObjectBase`, which needs a design. |
| 6 | One declaration of the Stride wire types. | Done in `97616d64`, in `Bruinet.Stride.Communication`. Left: the split `Bruinet.Stride.Automation` namespace. |
| 7 | Take platforms and the runner out of Core: step attributes, `MauiHead`/`WebHead`, `Scraping`, `Composition`, `MarkdownTable`. | Done as far as it goes: the step attributes are in `Bruinet.Bdd.Abstractions` (`e9d3b2c4`), `DeviceFact` and `BrowserFact` on their platforms (`a0ffa1eb`), `Scraping` in the Scraper. Staying in Core, with reason: the heads (the run's `BRUINET_HEAD`, read by Bdd, Uat and Core), `Composition` (`[TestModuleScan]`, `TestPages`, used by every stack, Uat, Bdd and consumers) and `MarkdownTables` (Core, Bdd and the Scraper). |
| 8 | Settle the signature rules and apply them through the `.tpl.cs` generators. | Done: all eight rules of `proposal-8-signature-rules.md`, approved by the owner 2026-11-10, one commit each with its change-list row; the generator names the `Exists` query `Exists()`. |
| 9 | Decide `timeoutMs` versus `TimeSpan` once. My proposal: `int? timeoutMs` on control verbs, `TimeSpan` in options and settings types. | Done as proposed (the owner, 2026-11-10): `TimeoutSettings`, Stride's, Blazor's and bUnit's options are `TimeSpan`; `ToMs()` bridges to a verb; JSON keeps milliseconds. |
| 10 | `ITestLogger` takes one record per event instead of positional strings. | Done as designed in `proposal-10-logger-record.md` (approved 2026-11-10): `Write(LogEvent)` and `Flush()`; the error factory is `Failed`, since `Error` is the record's exception property. |
| 11 | One options type per platform that a test author sets. | Open; needs a design of its own. |
| 12 | Before the first release, move the cleaned surface to `PublicAPI.Shipped.txt` and mark `[Experimental]` what is public but not promised. | Started: `BRUINET010` marks `Bdd`'s engine (`docs/experimental.md`). Left: Narrator, Contracts and the scraper support, then the move to `Shipped`. |
| 13 | A change list and skill so the projects that use Bruinet can be refactored after each change: per change, what to search for and what to write instead. | In working order: rows to date, and `tools/Scripts/check-api-changes.ps1` (CI) fails a break without one. Left: U8.3 to U8.6 of `.my/upgrade/plan.md` (recipes, a proof on a consumer, the consumers table, links from the docs). |

### Proposal 13 comes first

The API is already used by other projects, so every open proposal above breaks someone.
Proposal 13 is what makes that cheap, and it has to be in working order before proposals 1, 2, 4, 5, 7 and 8 are made, not after.

- Each proposal, when made, adds its row to `.github/skills/api-upgrade/references/changes.md` in the same commit; the Planned rows there already say what each one will ask of a consumer.
- The list caught up with this morning's four commits; all four additions have default bodies, so none breaks a consumer.
- The list has not yet been proven on a project that is not Bruinet's own; Tomorrow uses Html only and built unchanged.
- `check-api-changes.ps1` fails a change that removes or alters an API line, or adds an interface or abstract member, without touching the list.

## Unknown

- Which `Bdd` engine types consumers use directly. Tomorrow uses seven: the step-author types, `BddDocument`, `BddBoundScenario`, `BddTagConventions`, `BddMarkdownParser` and `BddParseResult`.
- Whether `bool? expected` with `null` meaning "skip" is relied on by data-driven steps; if so it should be documented on the interface, not removed.
- `Bruinet.Presenter`, `Bruinet.Scraper`, `Bruinet.Bdd.Workspace` and `Bruinet.Mocking.Analyzers` declare no public API file and were not reviewed; if any is packed, it needs one.
- `Bruinet.Mocking.*`, `Bruinet.Contracts`, `Bruinet.TestData`, `Bruinet.FlaUI` and `MauiHeadless` were read by type list only, not member by member.
- The new WPF, WinForms and Stride surfaces were checked for the points above, not reviewed member by member.

## Next action

Proposals 1, 2, 4, 7, 8, 9 and 10 are made as far as they go, 5 and 12 started, 13 in working order.
Two need a design: the shared collection base (5) and one options type per platform (11); 11 is the larger, since every stack has three or four options types today.
