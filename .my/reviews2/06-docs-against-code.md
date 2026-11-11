# Docs against the code: review and proposals

Written 2026-11-11 at `b2ea4570`, over `README.md` and the 53 files under `docs/`.
Checked by search against today's source and the `PublicAPI.Unshipped.txt` files; nothing was compiled or run, and no worktree was made.
The command line was read from the `--help` of the Debug build of 2026-11-10 20:55, which is newer than the last commit to the CLI, the Scraper and the workspace (`61373e39`).

## Outcome

The guides written or rewritten this week are right: the platform guides, `configuration.md`, the BDD, test-data, replay and headless guides name only what exists.
Two older pages are wrong from top to bottom: `docs/controls/Shell.md` and the first page object in `docs/getting-started/quick-start.md`.
Beside them the docs carry one claim no code backs (a Selenium web head), ten links into a `docs2/` folder that is gone, and a source-file list that still points at `src/Bruinet.Uat` for files that moved to `src/Bruinet.Bdd`.
Every `bruinet` command, option and script the docs name exists.

## What was measured

| Check | Checked | Stale |
| --- | --- | --- |
| C# samples (fenced `csharp` blocks, 18 files) | 62 | 16 name members that do not exist; 4 quote a file or interface and have drifted |
| `bruinet` invocations, with their options | 98 | 0 |
| Scripts under `tools/Scripts` named in the docs | 9 | 0 |
| Environment variables named (`BRUINET_*`, `APPIUM_*`) | 65 | 0 (two are named as dead, one is an example) |
| Relative links | 265 | 11 |
| Repository paths named in text or commands | 219 | 14, and two mentions of `docs2/` |
| Names from the change list's old side | 7 hits | 4 real, 3 false alarms |

Of the 62 samples, 18 declare a whole type and have no ellipsis; 11 contain `...`; the rest are fragments of a few lines.

## Finding 1: `docs/controls/Shell.md` describes a control that no longer exists

All 15 samples on the page fail. Today's `Shell<TParent>` has a constructor `Shell(IMauiScope<TParent> scope)` and two members, `Tabs` (`ShellTabs<TParent>`, a collection of `ShellTab<TParent>`) and `Flyout` (`ShellFlyout<TParent>`: `Open`, `Close`, `IsOpen`, `WaitOpen`, `AssertOpen`).
No page links to `Shell.md`, the docs index included.

| Line | Says | Should say |
| --- | --- | --- |
| 5 | `Shell<TScope>` | `Shell<TParent>` |
| 35 | `public override bool IsLoaded(int? timeoutMs = null) => _shell.IsLoaded();` | a page's `IsLoaded()` takes no argument, and `Shell` has no `IsLoaded`; a page states `ReadyLocator` or nothing |
| 51-56, 115, 125 | `IsTabSelected`, `WaitTabSelected` on the shell | the tab's own state, through `shell.Tabs` |
| 76-80 | `Shell(IMauiScope<TScope> scope, string automationId = "AppShell")` | `Shell(IMauiScope<TParent> scope)`; there is no automation id argument |
| 88-97 | `GetTab(title)`, `GetSelectedTab()`, cached tabs | `Tabs`, a collection; an item by key |
| 142, 152 | `shell.IsLoaded()`, `shell.WaitLoaded(timeoutMs: 10000)` | neither exists on `Shell` |
| 41-66, 105, 132, 159-185 | `NavigateTo`, `GoToContainersTab`, `AssertTabIsSelected`, `AssertShellLoaded`, `PressTab(Keys.Shift)` on a page | these are members the page would have to write itself; the page never shows them |
| 210-211 | `Tab<TScope>`, `TabViewControl<TScope>` | neither type exists; `ITabControlObject<TScope>` does |
| 219 | "tabs are located via XPath on their Title" | not so: `ShellTabs` is built from `ShellChromeLocators` |

## Finding 2: the quick start's first page object does not compile

`docs/getting-started/quick-start.md:39-62` is the first code a new reader copies.

| Line | Says | Should say |
| --- | --- | --- |
| 40-42 | usings without `Bruinet.Maui.Controls.Buttons` and `Bruinet.Maui.Interfaces` | add both |
| 46-47 | `LoginPage(MauiTestContext context) : base(context, "Login")` | `LoginPage(IMauiTestContext context) : PageObjectBase<LoginPage>(context)`; the name is `public override string Name => "Login";` |
| 51-52 | `public Entry Username => Get<Entry>(Locator.ByAutomationId("UsernameEntry"));` | `public Entry<LoginPage> Username => Entry("UsernameEntry");` (no `Get<T>` exists; controls are generic in their scope) |
| 53 | `public Button SignIn => Get<Button>(…)` | `public Button<LoginPage> SignIn => Button("SignInButton");` |

`SetText` and `Click` on lines 57-59 are right.

## Finding 3: the docs promise a Selenium web head that is not there

No project under `src/Bruinet.Html*` or `src/Bruinet.Blazor` uses Selenium; the only web driver is Playwright, and bUnit is the second head. Selenium's types are used by the Appium stacks only. `WebDriverManager` is in `Directory.Packages.props` and no project references it.

| Where | Says | Should say |
| --- | --- | --- |
| `README.md:54` | "Playwright and Selenium in a browser" | "Playwright in a browser" |
| `README.md:15`, `README.md:43` | drivers "FlaUI, Appium, Playwright, Selenium, Stride and Android" | drop Selenium |
| `docs/architecture/overview.md:36` | "HTML and Blazor through Selenium or Playwright" | "through Playwright, or bUnit without a browser" |
| `docs/getting-started/framework-overview.md:14` | "Platform drivers: FlaUI, Playwright, Selenium, Appium…" | drop Selenium; add MauiHeadless and bUnit, as the README's own list has |
| `docs/architecture/stack.md:20` | Web: "Selenium.WebDriver, Selenium.Support, WebDriverManager, Microsoft.Playwright" | Web: Microsoft.Playwright, bUnit; Selenium belongs in the MAUI row, under Appium |

## Finding 4: links and paths to things that moved or went

| Where | Says | Should say |
| --- | --- | --- |
| `docs/specs/README.md:24-33` | ten links to `../../docs2/specs/…` | delete the "Preserved Specs" table: `docs2/` is not in the repository |
| `docs/architecture/structure.md:14`, `docs/guides/migration.md:20` | "`docs2/`: preserved previous documentation tree" | delete the row and the bullet |
| `README.md:133`, `docs/architecture/structure.md:15` | "indexed in `.my/README.md`" | "Plans for work in flight"; the index went with this week's tidy |
| `docs/guides/uat-template-guide.md:265` | `[Prose](#prose)` | the page has no such heading; link the heading that covers prose, or drop the link |
| `docs/guides/uat-step-definitions.md:944-946, 948-953` | `src/Bruinet.Uat/BddMarkdownParser.cs`, `BddCommandCatalog.cs`, `BddBinder.cs`, `BddScenarioFiles.cs`, `BddCatalogSet.cs`, `BddValues.cs`, `BddPhraseDeclarations.cs`, `BddArguments.cs`, `BddArgumentTypes.cs` | the same names under `src/Bruinet.Bdd/` (`BddPreparedCallables.cs` on line 947 too) |
| `docs/guides/uat-step-definitions.md:947`, `:956` | `src/Bruinet.Uat/UatCallables.cs`, `src/Bruinet.Uat/UatExecution.cs` | no file of either name exists anywhere under `src/`; delete the two entries |
| `docs/guides/settings.md:79` | `src/Bruinet.Uat/BddExecutionContextSettingsExtensions.cs` | `src/Bruinet.Bdd/BddExecutionContextSettingsExtensions.cs` |

## Finding 5: names from the change list that are still in the docs

Four hits are real.

| Where | Says | Should say |
| --- | --- | --- |
| `docs/controls/Shell.md:35` | `override bool IsLoaded(int? timeoutMs = null)` | see finding 1 |
| `docs/guides/troubleshooting.md:92`, `:97` | `FlaUIMauiDriver.DescribeGestureBridge()`, `FlaUIMauiDriver.RootReattachments` | the class is internal since `5a3234b4`, so a reader cannot call either; name what a test project can reach (the `BRUINET_UIA_LOG` file the paragraph already names), or say these are for work inside Bruinet |
| `docs/guides/uat-step-definitions.md:952` | `BddArguments.cs` "the bind-time check and the run-time read" | the type is internal since `249a70eb` and the file is in `src/Bruinet.Bdd` |

Three hits are false alarms and need no change: `docs/platform-guides/stride.md:24-25` (`IsGameReady`, `SetSliderValue` are still wire commands of the game server), and `docs/run/maui-ios.md:44` (the test is still named `Button_IsExists_ReturnsTrue`).
No doc names a deleted options or configuration type, `HtmlPageObjectBase`, `ControlBase`, `IsExists()`, `WaitTextEquals`, `AssertionException`, an `ITestLogger` `Log…` method, `Bruinet.Core.Scraping` or `Bruinet.Core.Legacy`.

## Finding 6: samples that quote a file and have drifted from it

These compile in spirit; they no longer match what they cite.

| Where | Says | The source says |
| --- | --- | --- |
| `docs/guides/coming-from-reqnroll.md:74` | `[BddStepDefinitions]` on `StoredDataPhrases`, citing the file | `[BddStepDefinitions(Namespace = "StoredData")]` |
| `docs/guides/test-data.md:40-51` | `TableTypes => [typeof(TodoDto), typeof(LocalTodoData)]`, three rules | the Todo sample's definition also has `Name`, `typeof(UserDto)` and the local and user rules; say "shortened" or include the real one |
| `docs/guides/uat-template-guide.md:100` | `new(Target: "MAUI", Fixture: "FlaUI")` | the Todo sample passes `Fixture: "TodoUatFixture"` |
| `docs/guides/uat-step-definitions.md:889-891` | `void SetText(string text, int? timeoutMs = null);` | `TScope SetText(string? text, int? timeoutMs = null);` (`IEditableTextControlObject`) |

## Proposals

| # | Proposal | Cost |
| --- | --- | --- |
| 1 | Fix findings 2 to 6 by hand, as listed. | An hour; no decision needed except Selenium (see Unknown). |
| 2 | Rewrite `Shell.md` from the Shell sample app's page objects, or delete it. | Delete: minutes. Rewrite: half a day, less if proposal 4 comes first. |
| 3 | A docs check in the build's test step: every relative link and every repository path in `README.md` and `docs/` exists; every `bruinet …` line parses against the CLI's own command tree; no doc matches a `Find` pattern of the change list's Made rows. | Three tests, a few hours. They would have caught 29 of this review's stale items (11 links, 14 paths, 4 old names). |
| 4 | Samples included from compiled files (below). | One to two days once (my estimate, not measured), then nothing per change. |
| 5 | A row in AD-033 once 3 and 4 stand: "The docs' samples and links are checked". | Minutes. |

### Proposal 4: samples come from files the build compiles

The build already has this pattern: `GeneratedFilesTests` regenerates every `.tpl.cs` and compares it with the committed `.gen.cs`, so the committed file is the snapshot. The same works for docs.

- A sample lives in a `.cs` file the solution builds, between `#region docs:<name>` and `#endregion`.
- The doc's block follows a marker line, `<!-- sample: <path>#<name> -->`.
- A test (beside `SolutionTests` in `Bruinet.Core.Tests`) reads every marker and fails when the block differs from the region, or when a `csharp` block has no marker and no `<!-- sample: none, why -->`.
- `tools/Scripts/update-doc-samples.ps1` rewrites the blocks from the regions, as `update-public-api.ps1` does for the API files.

A rename then breaks the sample file in the build, the fix is made there, and the script carries it into the doc.

What it costs:

- **Moving the samples.** Samples that quote the Todo sample or a UI suite's page object get a region in that file; they are real code already. The rest need a new project, `tests/Bruinet.Docs.Samples`, built and never run.
- **Target frameworks.** MAUI, WPF and WinForms samples need a Windows target; Html, Bdd and TestData do not. One project with two targets and conditional folders, or two projects.
- **Shortened samples.** A region shows real code, so a guide's four-line version of a forty-line class becomes its own small file in the samples project, not a cut of the real one. That is more files, and each stays honest.
- **Fragments.** Two- and three-line fragments (`settings.md:54`, `maui.md:80`) go inside a method in the samples project. Sketches with `...` or `{ ... }` (11 blocks) either become real or are marked `sample: none`.
- **Build time.** One more project in `Bruinet.sln`; not measured.

Ruled out: extracting each block and compiling it as it stands. Only 18 of the 62 blocks declare a whole type, so the other 44 would each need a wrapper the doc does not show, and the check would test the wrappers.

## Unknown

- Nothing was compiled. A wrong overload, a protected member used from outside, or a generic argument the compiler cannot infer would not show in a search.
- Whether a Selenium web head is planned. If it is, finding 3 becomes "say it is planned"; if not, `WebDriverManager` can leave `Directory.Packages.props`.
- Whether `Shell.md` is wanted. It is linked from nowhere.
- `Bruinet.Uat`'s `PublicAPI.Unshipped.txt` holds only `#nullable enable`: the project is an `Exe`, and `src/Directory.Build.props` leaves executables out of the API analyzer. Its samples were checked against source instead. AD-033 says every packable library declares its API, and Uat is packable; that belongs to another review.
- The 14 Gherkin and 25 Markdown scenario samples were not checked against the phrase catalogs; `bruinet data validate` is the tool for that.
- The JSON, XML and XAML samples were read, not bound: `configuration.md`'s keys match the change list's row for `0124ea2a`, the rest is unchecked.
- External links were not followed.
- The command line's help came from yesterday's Debug build, not a build of `b2ea4570`; the CLI's own source did not change between them.

## Next action

Decide Selenium and `Shell.md` (delete or rewrite); then proposal 1 can be made in one commit and proposal 3 added so links, paths and old names are checked from then on.
