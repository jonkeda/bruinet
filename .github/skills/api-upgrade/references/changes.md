# Bruinet API changes

What a project using Bruinet must change, per change. `Find` is a regex for consumer code
(`*.cs`, and where noted `*.feature`, `*.steps`, `*.config`, `*.json`).

## Made

Newest first. Apply oldest first.

| Commit | Change | Find | Write instead |
| --- | --- | --- | --- |
| | (none yet) | | |

## Planned

From `.my/reviews/bruinet-public-api-review.md` (2026-11-09, at `f9ad9a96`). None of these is made.
Names under `Write instead` are the review's proposal and may differ when the change is made; the
row is corrected when it moves to `## Made`.

| Proposal | Change | Find | Write instead |
| --- | --- | --- | --- |
| 1 | `Bruinet.Core.Calls` made internal | `using Bruinet\.Core\.Calls`, `\b(ControlCall|AttemptContext|Poller|ScopeGate|Confirmer|ObservationLog|CallBudget)\b` | none - a custom control uses its base's `RunDo…`/`RunGet…`/`RunAssert…`/`Confirm` helpers |
| 1 | Driver and element classes made internal | `\b(Playwright|Bunit)Html(Driver|Element)\b`, `\b(Appium|FlaUI|MauiHeadless)Maui(Driver|Element)\b`, `\bFlaUI(Wpf|WinForms)(Driver|Element)\b`, `\bAppiumNativeAndroid(Driver|Element)\b` | none - go through the page object; a fixture names the head by its context options, not the driver type |
| 1 | `Bruinet.Bdd` parser, binder, catalogs and config editors made internal | `\bBdd(Binder|BindResult|MarkdownParser|CommandCatalog|CommandPattern|CatalogSet|Callable\w*|ConfigParser|ConfigWriter|Config\w*Edit|PhrasesDocument)\b` | none in step code; a tool that read these uses the `bruinet` command line or the MCP tools |
| 1 | `Bruinet.Bdd.Runner` messages made internal | `using Bruinet\.Bdd\.Runner`, `\bBddRunner\w+\b` | none - only Bruinet's runner and its clients speak it |
| 2 | `Placeholder` classes removed from Core | `\bPlaceholder\b` with `using Bruinet\.Core\.(Attributes|Locators|Logging)` | delete the reference; they were empty |
| 2 | `WaitHelper.Pause` removed | `WaitHelper\.Pause\(` | a `Wait…` on the control or page whose state the pause was waiting for |
| 2 | `WaitTextEquals` removed (Html `ControlBase`) | `\.WaitTextEquals\(` | `.WaitText(` |
| 3 | `Bruinet.Core.Legacy` types moved to namespace `Bruinet.Core.Legacy` and marked obsolete | `\b(IElement<|IDriver<|IElementScope|IPageObject|ICollectionObject<|IContainerObject<|ControlObjectBase<|LegacyWaitHelper|PageReadinessSnapshot)\b` | add `using Bruinet.Core.Legacy;` now; move the page or control to the call model's bases before the package is dropped |
| 4 | Html and Blazor page base renamed | `\b(Html|Blazor)PageObjectBase<` | `PageObjectBase<` |
| 4 | Html and Blazor control names lose the `Control` suffix (if that direction is chosen) | `\b(Button|Link|List|Table|Label|Progress|RangeInput|RadioGroup|Select|TextArea|TextInput|CheckBox|RadioButton|DateInput|TimeInput|Audio|Video|Image|IFrame|NavMenu|TabContainer|ScrollContainer)Control<` | the same name without `Control`; where it then clashes with an app type, alias the Bruinet namespace |
| 4 | Maui and NativeAndroid `ViewBase<TScope>` renamed | `\bViewBase<` | `ControlBase<` |
| 4 | Wpf, WinForms, Stride controls move to grouped namespaces | `using Bruinet\.(Wpf|WinForms|Stride)\.Controls;` | add the group's `using` (`.Controls.Buttons`, `.Controls.Text`…) the compiler asks for |
| 5 | `ItemKey`, `ItemKeyKind` moved to Core | `using Bruinet\.(Html\.Collections|Maui\.Containers|NativeAndroid\.Collections);` where only `ItemKey` is used | `using Bruinet.Core.Collections;` |
| 5 | `IExpandCollapsePatternElement`, FlaUI `LocatorExtensions` moved to one shared package | `Bruinet\.(Wpf|WinForms)\.Interfaces\.IExpandCollapsePatternElement`, `Bruinet\.(Wpf|WinForms|Maui)\.FlaUI\.LocatorExtensions` | the shared package's namespace; add its reference if the consumer references the platform package only transitively |
| 6 | Stride wire types moved to `Bruinet.Stride.Contracts` | `using Bruinet\.Stride(\.Automation)?\.Communication;` | `using Bruinet.Stride.Contracts;`; the game project references the new package |
| 7 | BDD step attributes moved out of Core | `using Bruinet\.Core\.Testing;` in files with `\[Bdd(Step|Given|When|Then|State|Namespace)` | `using Bruinet.Bdd;`; a page-object project that only declared steps references the BDD abstractions package |
| 7 | `MauiHead`/`MauiHeads`, `WebHead`/`WebHeads` moved to their platforms | `\b(MauiHeads?|WebHeads?)\b` with `using Bruinet\.Core\.Testing` | `using Bruinet.Maui.Testing;` / `using Bruinet.Html.Testing;` |
| 7 | `Scraping`, `Composition`, `MarkdownTable` moved to tool packages | `using Bruinet\.Core\.(Scraping|Composition|Utilities);` | `[TestPage]` and `[TestModuleScan]` keep working from their new namespace: add the `using` the compiler asks for; the rest has no consumer use |
| 8 | `IsExists()` renamed | `\.IsExists\(\)` | `.Exists()` |
| 8 | Every `Get…` takes `timeoutMs` | overrides of `GetDate|GetTime|GetProgress` in custom controls | add `int? timeoutMs = null` to the override; callers are unchanged |
| 8 | One `Locator` property on element exceptions | `\.(LocatorInfo|LocatorString|ControlLocator)\b` | `.Locator` (a `Locator?`; `.Locator?.ToString()` where the string was read) |
| 8 | `IPressableControl<>` renamed | `\bIPressableControl<` | `IPressableControlObject<` |
| 9 | Options and settings types take `TimeSpan` | assignments to `TimeoutSettings\.\w+`, `\.With\((defaultWait|pageLoad|elementFind|elementState|animation|pollingInterval):`, and `Timeouts` sections in settings JSON | `TimeSpan.FromMilliseconds(n)` in code; JSON keeps numbers of milliseconds if the reader accepts them - check the row when made |
| 10 | `ITestLogger` takes one record per event | `: ITestLogger\b` (a consumer's own logger) | implement the new methods from the record's fields; callers of the logger are inside Bruinet |
| 11 | One options type per platform | `\b(HtmlOptions|HtmlTestContextOptions|WebAppHostOptions|BlazorOptions|BruinetHtmlConfiguration|BruinetBlazorConfiguration|MauiOptions|MauiDriverOptions|MauiTestContextOptions|BruinetMauiConfiguration)\b`, and their sections in settings JSON | not designed yet; this row is rewritten when it is |
| 12 | Narrator, Contracts and scraper support marked `[Experimental]` | build error `BRUINET0xx` in projects that use `Bruinet\.(Narrator|Contracts)` | `<NoWarn>` for that id in the consumer's test project, as an accepted risk |
