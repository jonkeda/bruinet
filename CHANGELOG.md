# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- MCP `draft_feature`: a feature file drafted from a C# UI test, each statement as the suite phrase that matches its words or a commented gap, validated as the gate would, with its gaps, its finally-block cleanup and phrases that differ only in a value. MCP `similar_steps`: the phrases that could be one phrase with a placeholder. A suite built against an older Bruinet now reports that as its reason instead of failing every tool.
- One runner per folder: a `[UatScenarioTheory]` case takes `Module` (from `Area`), `Journey` (the scenario's journey
  tags, else the file's `Journey`) and `Pyramid` (when `Priority` names a tier) from its own feature file, with
  `UatScenarioTraits`; a trait the runner's attributes set wins. `Bruinet.Traits` now targets net8.0 and net9.0 too.
- Scenario undo: `BddExecutionContext.RegisterUndo(...)` runs an undo after the scenario's AfterScenario hooks, last
  first, also when a step failed. A failing undo fails a passed scenario as a setup failure, or is added to the failed
  step's message. `ScenarioStartedUtc` says when the scenario started.
- `MockRequests` steps (`Bruinet.Mocking.Steps.MockRequestSteps`): `no POST was sent to /path`, `one POST request was
  sent to /path`, `2 POST requests were sent to /path`, and `the last POST to /path has` with a `Field`/`Value` table
  (dotted JSON paths or form fields, `(absent)` for not sent). They count only this scenario's requests, on the
  composition's `MockApiServer` or the shared one (`MockApiServer.Shared`).
- Folder-wide format check: `UatSpecFormatTestBase.GetFolderFiles(folders)` and `AssertUatFileFormat(file)` check every
  feature file under a folder with no list; a file opts out with the tag `no-format-check` and a `FormatCheck` reason.
- `Bruinet.Traits` trait names: `[Ui]` writes `Pyramid=UI` and, during the move, the old `Pyramid=Hermetic UI`
  (`[HermeticUi]` now writes the same pair); `[Covers("id")]` names a coverage row, check, finding or decision and may
  repeat; `[Head("Android")]` marks a test that needs one head, with the `UatPlatforms` names; run sets
  `[Architecture]` and `[Review]`. `TraitCheck` counts `UI` and `Hermetic UI` as one tier, and `TierNames.Current`
  maps an old tier name to its new one.
- Write check and setup label: `MockApiServer.WritesSince(sinceUtc)` gives each write since a moment as a
  `Bruinet.Contracts.RecordedWrite`, ready for `ExpectedRequestMockCheck`. A fixture that is an `IUiTestCheck` gets
  `BeginTest` before each `[UiFact]` test and `CheckPassedTest` after it passed; a throw fails the test.
  `UiSetup.Setup(...)` and `SetupAsync(...)` wrap a test's setup, so a failure there is a `UiSetupException`. Such a
  failure, and any failed Given step, has kind `setup` (with its `cause`) in the scenario report, the `failing` and
  `why` MCP tools, and the run's `summary.md` (`failed: setup`).
- Scripts for an app's own test runs (`docs/run/app-scripts.md`): `run-maui-headless.ps1` and `run-maui-android.ps1`,
  `RunGuards.psm1` (`Stop-LeftoverApp`, `Test-BruinetSubmodule`), `coverage-areas.ps1` (line coverage per source folder)
  and `mutate-folder.ps1` (Stryker on one folder through a harness library). Moved from Exact Construction.
- `check-test-guard.ps1` takes `-Root` and `-Exclude`, reads the reason from `TEST_GUARD_REASON` or a
  `Test-Guard-Reason:` commit line, and counts test cases per row, so merging facts into a theory is not a loss.
- `repeat-run.ps1` takes `-Framework`, `-VaryOrder` (alternate xUnit collection parallelism) and `-AppProcess`, reports
  tests missing from some runs, and exits 1 when a test failed or went missing.
- `MauiDriverOptions.ForwardEnvironment(names)` copies the set test-process variables into `LaunchSettings`, and
  AppSupport's `BruinetLaunchSettings.ApplyIntentExtras` copies them from the Android intent into the app's environment.
- AppSupport container peers: `AddBruinetAutomationHandlers(options => …)` with `PeersRequireAutomationId` and
  `InvokeCommand` (Button control type plus Invoke for a container with a command); peers report the view's
  `IsEnabled`; Border and page peers report the class names `Border` and `Page`. `BruinetVerbRules.Publish` declares
  gesture verbs for every element matching a rule. Moved from Exact Construction.
- `Bruinet.Traits`: typed trait attributes for xUnit 2. Tiers `[Unit]`, `[Integration]`, `[HermeticUi]`, `[Live]` and
  `[Video]` write `Pyramid` (Unit, Integration and Video also write the matching `Category`); owners `[Module]`,
    `[Feature]`, `[Component]` and `[CommonArea]`; run sets `[Smoke]`, `[Uat]` and `[Contract]`; and `[Journey]`. `TraitCheck`
  reports tests with no tier, more than one, or no known owner, reading plain `[Trait]` too.
- Bruinet.Contracts: `LocalAccountStore` reads named test accounts (`{PREFIX}_{ACCOUNT}_USERNAME`, `_PASSWORD`,
  `_ORGANIZATION`) from a git-ignored `.env.local`, and `LiveContractGate.SkipReason` decides whether a live
  contract test runs: a live run, no mock backend, a real sign-in, and the test's own account. Moved from Exact Construction.
- **Breaking:** Stride is on the call model. `Bruinet.Stride` has its own element (`IStrideElement`, a handle on a UI element
  by its identity, read fresh from the game each time; gone is stale), driver (`IStrideDriver`, the pipe: a broken pipe is
  `AppUnavailableException`), scope, page and context interfaces, generated controls (`Button`, `ToggleButton`, `EditText`,
  `TextBlock`, `Slider`, `ImageElement`; `tools/Scripts/CreateStride.Bat`), containers and collections (rows are a panel's
  children). Gone: `ComboBox`, `ListBox`, `CheckBox`, `ProgressBar` and `Panel` (the server has no route for their items;
  a panel is a container), `IStrideTestContext.WaitFor` and the click and key delay settings. The context names the game
  window for a recorder (`IRecordable`). The sample suite passes 30 of 30 in 2 s (27 of 30 before).
- New `tests/Bruinet.Stride.Tests`: the client over a fake game.
- **Breaking:** WinForms is on the call model, in WPF's shape (`IWinFormsElement`, `IWinFormsDriver`, `IWinFormsElementScope`,
  `IWinFormsPage`, `IWinFormsTestContext`; generated by `tools/Scripts/CreateWinForms.Bat`). Its own controls: `TrackBar`
  and `NumericUpDown` read and write their number as Value text (UI Automation gives them no RangeValue; the page object
  gives their bounds), `NumericUpDown` commits it with its spinner buttons, `RichTextBox` reads back without RichEdit's
  closing line end, `ComboBox` selects by writing its Value (one blocking call instead of three), `DateTimePicker`
  (`IDateControlObject`) types the date in the culture's short date order with the keyboard, `TabPage`, `TreeNode`.
  The sample suite passes 62 of 65 (31 before); the three left set a date, which Windows refuses on a locked desktop.
- `UiaElementBase.PressKey`, behind `PhysicalInput`; a key Windows refuses fails saying why.
- **Breaking:** WPF is on the call model (AD-010, AD-013). `Bruinet.Wpf` has its own interfaces (`IWpfElement`,
  `IWpfDriver`, `IWpfElementScope : IScope`, `IWpfPage : ITestPage`, `IWpfTestContext`), generated controls
  (`tools/Scripts/CreateWpf.Bat`) in `Controls.Buttons`, `.Text`, `.Toggle`, `.Selection`, `.Range`, `.Container`
  and `.Display`, containers, collections with item keys, and one call per member. Every action is a UI Automation
  pattern of `Bruinet.FlaUI`'s `UiaElementBase`: a text box is written through its Value pattern, a combo box
  expanded, its item selected and collapsed; nothing takes the foreground. A page is known by `ReadyLocator`
  (the element whose AutomationId is its name, by default) and is busy while `BruinetBusy` or a `BusyIndicators`
  element is shown. Gone: the first model's `IElement<T>`/`IDriver<T>` shapes, `TreeView`, waiting lookups and
  setting the foreground before each click. The sample suite passes 16 of 16 in 20 s (2 of 16 failed in 2 m 53 s).
- `Bruinet.FlaUI`: what the FlaUI stacks share, taken out of `Bruinet.Maui.FlaUI` for WPF and WinForms to stand on.
  `DesktopApp` is the app session (launch with its own environment, attach by handle or process, the foreground
  handed back, `BRUINET_AUT_PLACE`, a root window attached again when UI Automation retires it, screenshots of the
  window's own content, the automation tree); `UiaElements` reports a removed element as stale and a closed app as
  unavailable, names an element by its runtime id, and looks up in one attempt. `FlaUIErrors`, `LocatorExtensions`
  and `PhysicalPixels` moved with it (namespace `Bruinet.FlaUI`). `FlaUIMauiDriver` and `FlaUIMauiElement` keep
  their members.
- Core's `IScope` and `ITestPage` (`Bruinet.Core.Scopes`): the element-free half of every stack's scope and page
  (readiness, `WaitReady`, `WaitFirst`; load, busy, title, screenshot). Html's, MAUI's and native Android's scope and
  page interfaces derive from them and keep only their typed lookups and extras (AD-010, amended). Test composition
  and UAT find a page by `ITestPage` (`TestPages.IsPage`); the page bases no longer carry `[TestPage]`.
- The calls layer is public: `ControlCalls<TElement>`, `IControlHost<TElement>`, `ControlCall`, `AttemptContext`,
  `Deadline`, `Poller`, `Confirmer`, `ScopeGate`, `FirstOutcome`, `Observation`, `ObservationLog`. A stack writes its
  control bases against them; Core no longer lets the stacks see its internals. From now on a change to
  `IControlHost` is a breaking change, listed here.
- `ITestContext.Name` and `ITestContext.NearMiss`, with defaults (unnamed, `NearMissSettings.Default`); Html's, MAUI's
  and native Android's contexts no longer declare them.
- A cloud voice's line is paid for once, on every machine: clips are kept in the suite's `speech` folder (`ClipLibrary`, Opus
  at 32 kbit/s), to commit, on top of the local cache. The name leaves out the endpoint, so another region finds a clip.
  A recording says what it reused and paid for, in its `.timeline.json` and `record_video`'s answer; `BRUINET_VOICE_REUSE=1`
  fails on a missing line instead of spending. `bruinet voice` lists the lines with no clip, `--fill` takes what the local
  cache holds, `--ask` buys the rest, `--prune` deletes what no script says.
- `bruinet voice --index` writes `speech-clips.md` beside a suite's video scripts: each line, where it is said and its
  clip, so a person can see which hash-named clip says what. `ClipLibrary.Lines` lists the lines a voice was asked for.

### Removed
- **Breaking:** `WaitTextEquals` on every stack: `ITextControlObject<TScope>` declares `WaitText`, which did the same.
- **Breaking:** `WaitHelper` is internal; the three empty `Placeholder` classes in Core are gone (review proposal 2).
- **Breaking:** `Bruinet.Core.Legacy`, the first model's interfaces and waits: every stack is on the call model (AD-029
  superseded). `TestPages.IsFirstModelPage` and `FirstModelPageInterface` with it; composition and UAT find pages by `ITestPage`.

### Changed
- **Breaking:** the MCP server's tools are two families with one verb per act (review 03, D2): the Scraper's start with
  `scrape_` (`scrape_next` says what to do), `failing` and `last_runs` are `list_results`, `why` is `diff_since_green`
  and runs nothing, `validate_scenario` is `check_scenario`, `review_page` is `write_review`; every rename is a row of
  the api-upgrade skill's `changes.md`, and an old name answers nothing. The server is `bruinet`. Every list takes `max`
  and says `more`; answers leave out nulls and give paths relative to the solution; a go-ahead is `confirm: true`; the
  schema says what is required, the values a parameter takes, and which tools only read. A suite that cannot load is a
  row of `list_suites` and is named under `list_results`' `notLoaded`, so the other suites still answer.
- **Breaking:** the object, app-root, row, container and collection bases of Html, WPF, WinForms, Android and Stride derive
  from shared bases in Core (`ObjectBase<TContext>`, `AppRootBase`, `ItemObjectBase`, `ContainerObjectBase`,
  `CollectionObjectBase`), with `IElementScope<TElement>` and `IItemRootSource<TElement>`; each stack keeps its classes'
  names and constructors. MAUI keeps its own (review proposal 5).
- **Breaking:** one options type per platform (review proposal 11): the stacks' context options share `TestContextOptionsBase`
  (`Timeouts`, `Logger`, `Name`, `NearMiss`, `Artifacts`), and `bruinet.{platform}.config.json` binds to them directly; the
  `Bruinet{X}Configuration` and `{X}Options` types are gone. Code wins over the file, the file over the defaults
  (`docs/guides/configuration.md`). `NativeAndroidDriverOptions` is `NativeAndroidTestContextOptions`.
- **Breaking:** `ITestLogger` takes one `LogEvent` record per event (`Write`, `Flush`); the thirteen positional `Log…` methods,
  eight of which nothing called, are gone. The CSV and console output are unchanged (review proposal 10).
- **Breaking:** time in options and settings is `TimeSpan` (`TimeoutSettings`, `StrideTestContextOptions`, `BlazorOptions`,
  `BunitTestContextOptions`); control verbs keep `int? timeoutMs`, and `TimeSpan.ToMs()` bridges them. Settings JSON takes a
  number of milliseconds, as before, or a TimeSpan string (review proposal 9).
- **Breaking:** `Bruinet.Core.Scraping` moved into the Scraper as `Bruinet.Scraper.Snapshots`; Core no longer declares
  the scraper's snapshot model (review proposal 7).
- **Breaking:** `DeviceFact` moved to `Bruinet.Maui.Testing` and `BrowserFact` to `Bruinet.Html.Testing`, beside the
  platforms they skip for; the heads (`MauiHeads`, `WebHeads`) stay in Core (review proposal 7).
- **Breaking:** the step attributes (`BddGiven`, `BddWhen`, `BddThen`, `BddState`, `BddNamespace`) and `StepNames` moved
  from `Bruinet.Core.Testing` to namespace `Bruinet.Bdd`, in the new package `Bruinet.Bdd.Abstractions` that Core references
  (review proposal 7).
- **Breaking:** `Bruinet.Bdd`'s engine and runner protocol are `[Experimental]` (`BRUINET010`, `docs/experimental.md`):
  public for Bruinet's own packages, not promised to consumers. Step authors' types are not marked (review proposals 1, 12).
- **Breaking:** the driver and element classes of every head are internal; tests reach the app through page objects, fixtures
  pick a head by the context's options (review proposal 1). `MauiHeadlessDriver` and `StrideGameDriver` stay public.
- **Breaking:** Html's `HtmlPageObjectBase<TSelf>` is `PageObjectBase<TSelf>` and its `ControlBase<TScope>` is `ViewBase<TScope>`, the names
  the other five stacks use; Html and Blazor controls keep their `Control` suffix (review proposal 4).
- **Breaking:** `ItemKey` and `ItemKeyKind` are Core's (`Bruinet.Core.Collections`), one type for the six stacks' collections;
  how a stack reads a key from a row is internal to it (review proposal 5).
- `tools/Scripts/check-api-changes.ps1` fails a change that breaks the public API without a row in the api-upgrade
  skill's change list; CI runs it on pull requests.
- The iOS alert button order and the full row frames are driver facts (`IMauiDriver.ListsCancelFirstInTwoButtonAlerts`,
  `ReportsFullRowFrames`); `ContentDialog` and `CollectionObjectBase` no longer branch on `MauiPlatform.iOS` (AD-010).
- The tests that record `Interactions` share one serial collection; a parallel report leaked into another test's log.
- `Timing`'s rows are written by an internal `TimingFile`, over a writer and a clock, so they can be tested. Tests for
  `Timing`, the console logger, `NavigationRecovery`, `MauiDriverFactory`'s errors, `Image` and `WebView` (coverage C7).
- MCP and the IDE start a suite's runner in its config's `WorkingDirectory`, as Presenter does (`BddSuite.WorkingDirectory`).
  They started it in the runner's copy of the output, so a fixture that finds its app from the solution did not find it.
- Presenter's rehearsal speaks in the script's tone with the suite's settings; it used other clips than the recording.
- `run_scenario` and the IDE's run on the runner leave out what the suite skips here (a head, `@browser` on bUnit, a skip
  rule), each `skipped` with the reason, as `dotnet test` does; such a scenario used to run and fail. A Step-through still
  opens it.
- A runner's copy of a suite's output is cleaned up after a host that was killed: making a copy now removes every ended
  process's copies, of every output (`BddShadowCopy.CleanEnded`). They stayed in the temp folder for good (42 GB after a
  benchmark). `step_screenshot`'s page text leaves out Blazor's error banner.
- MCP: a scenario tool's `line` (`run_scenario`, `step_start`, `why`, `get_trace`, `coverage`, `http_calls`) is any line of
  the scenario: its heading, a tag or a step. Agents passed the line a failure names, and half of `why`'s calls failed.
- MCP: `get_failure`, `where_is` and `step_log` are gone: no benchmark run called them, and their answers are in
  `run_scenario` (`failure`, `appSource`, `appEvents`) and `get_trace`. The server's instructions no longer name
  `coverage`, `tests_affected` or `http_calls`; those tools stay, with their descriptions.
- MCP: `step_state` with no page current reads the page the last step drove (as the trace names it), else the one
  page that says it is loaded, so after a `set_state` without `page` it still shows the screen. `step_state` also reads a list's rows, `step_screenshot` copies to the
  solution's `TestResults/step-screenshots` (a tree inline, or bUnit's markup as the text it shows), and `set_state` refuses a state with `{placeholder}`
  left in and knows the test sets on a fresh server.
- `ScreenshotService.Capture` gives the file a context with no pixels wrote under the screenshot's name (bUnit's
  `.html`), not the `.png` path it never wrote.
- `WebAppHost.FindBuiltApp` looks above the folder the tests were built into (`AssemblyFile.FolderOf`), not the
  process's: under a runner's temp copy, no browser-head scenario found its app.
- MauiHeadless: a back with a dialog open dismisses the dialog (`TestDialog.Dismiss`) instead of popping
  the page under it, which left the dialog open for the next test. `ResetAppState` no longer throws
  `DeviceOnlyException`: it clears the window (`MauiHeadlessDriver.ClearWindow`, focus, dialogs, modals,
  pushed pages) and the latter returns what it cleared. A dialog raised by a page a modal covers is
  collected. The `ControlTypeAndName` locator resolves headless.
- MauiHeadless: `MauiTestContextOptions.DialogPages` names the page types a platform draws as a popup;
  pushed as a modal, one is the active dialog for `ContentDialog<T>`, as a WinUI popup is on Windows.
  An open alert stays on top of it. A page with a title is named by it.
- MauiHeadless: a collection search or `ScrollToEnd` past a list's last row no longer ends at once:
  the app's loading is let finish and a paging list's `RemainingItemsThresholdReached` is raised, as a
  device's list does at its end. The Maui sample has a paged list page (`PagedListTests`).
- A collection search or `ScrollToEnd` on a paging list reaches its later pages on Windows: at a
  list's end the Windows head nudges it (a step back and forward), as MAUI decides the next page from
  a last-visible index a jump leaves stale; at the end a search waits a moment for the page to land,
  within its budget; and rows that report no position count as new when they move on screen.
- A pull (`PullToRefresh`, `SwipeDown`) on a view that keeps its `RefreshView` private reaches it
  however deep, headless and through the Windows bridge (it went one `ContentView` deep); a view
  holding two is refused. The Maui sample's containers page has such a list (`NestedRefreshTests`).
- MauiHeadless: an app's own `Focus()` and `Unfocus()` move focus, as a platform answers them (they
  returned false and focused nothing), and a field's `TextTransform` holds and shows typed text
  transformed. The Maui sample's text page has an upper-case field, a `MaxLength` field and a clear
  that refocuses (`FieldRulesTests`).
- MauiHeadless: a page under a modal stays in the tree, found and ready, as on Windows, where a modal
  (or a Mopups popup) covers its page without taking it away; it read as a missing root. The Maui
  sample has a busy page covered by a pushed page and a modal (`CoveredPageTests`).
- MauiHeadless: `MauiTestContextOptions.TimeFollowsWallClock` (`TestDispatcher.FollowsWallClock`) makes
  the virtual clock keep pace with real time; the Bruinet head turns it on, so app timers and
  `DispatchDelayed` work fire as a test waits, as on a device. MauiHeadless on its own is unchanged.
  The Maui sample has a timed page (`TimedUiTests`).
- A Run or a Step-through on the runner keeps a scenario's result at its first failed step, as the
  gate does; it kept one only when every step had run, so a Run that failed early left none.
- The runner's error names an exception's type when its message is a bare name
  (`DirectoryNotFoundException: Bruinet.Samples.Todo.UITests`, not `Bruinet.Samples.Todo.UITests`).
- **Breaking:** `srcnew/` and `testsnew/` are now `src/` and `tests/`: a project that references
  Bruinet's sources by path changes `srcnew` to `src`. `start-appium.ps1` and `run-android-tests.ps1`
  moved to `tools/Scripts`; the Stride pipe probes at the root (`test-key*.ps1`, `test-pipe.ps1`) are gone.
- **Breaking:** `voice.secrets.json` holds each cloud voice in an object of its own: the Azure OpenAI
  voice's fields in `azureFoundry` (they were at the top level) and Azure Speech's in `azureSpeech`
  (it was `speech`). A file in the old shape is not read: move its fields, as
  `voice.secrets.example.json` shows, or save them again in the Presenter.
- The sample apps (`Bruinet.Samples.Maui.App`, `...ShellApp`, `Bruinet.Samples.Todo.App`) build for
  plain `net10.0` themselves, for the MauiHeadless head, and the `*.App.Headless` projects that linked
  their sources are gone. The UI test projects reference the apps with
  `SetTargetFramework="TargetFramework=net10.0"`; the apps add their Windows target only on Windows.
- MauiUnit is renamed MauiHeadless (`src/MauiHeadless`, package `MauiHeadless`), and Bruinet's head `Bruinet.Maui.MauiUnit` is `Bruinet.Maui.Headless`: `BRUINET_HEAD=headless`, `MauiPlatform.Headless`, `MauiHeads.IsHeadless`, `CreateHeadlessOptions`, `HeadlessPreferences` and `HeadlessSecureStorage`. No release shipped the old names.

### Added
- Since last green (AI9): the MCP server's `why` and `bruinet why <file>:<line>` compare a scenario's newest failing run
  with its last green one, step by step: the first step where they part, a kind (`page-changed`, `data-changed`,
  `timing-only`, `scenario-changed`), steps that got much slower, and the files changed since the green run's commit that
  touch that step or an earlier one (changes of comments only set apart); with no failure traced yet, `why` builds and
  runs the scenario first. A failure in `run_scenario` and `get_failure` carries the short form, `sinceGreen`.
  `summary.md` gives the step and kind under a failure. Every scenario that passes or
  fails is now traced, with its commit, and a pass keeps a green copy.
- A per-step trace (AI8): a failed scenario writes `step-traces/<feature>__<scenario>__trace.json` beside its report, with
  each step's control calls (duration, attempts), what the app said, and the page's controls after the failed step. The
  MCP server's `get_trace` reads it, a row per step or one step in full. `BRUINET_TRACE=all` (`run_scenario`'s
  `trace: all`) traces passes too, `off` nothing.
- From a control to its source (AI7): the MCP server's `where_is {page, control}` gives the page object member's line,
  the test id it finds by, and the app's `.razor`, `.cshtml` or XAML line that makes the element (the suite's own app
  first when a web and a MAUI app share ids); a failure's `appSource` gives the same for its locator. No build step: the
  test id is searched in the app's sources.
- States (AI10): `[BddState("the app starts from the {testSet} test set")]` declares one on the method that sets it through
  test data or the service head; it is a `Given` a scenario names. The MCP server's `list_states` gives them and
  `set_state` puts the app in one on the suite's runner, leaving it paused there (`page` for `step_state`); only a declared
  state runs. The built-in `Then the run pauses here` does nothing. The Todo sample declares two.
- HTTP replay (`Bruinet.Mocking.Http`, AI11): a test's app's outgoing calls recorded once against the real services
  (`BRUINET_HTTP_REPLAY=Record`) and answered from the recording after, with no service (`Replay`); `Refresh` lists the
  answers that changed. In the test process through `HttpReplayHandler` or `AddBruinetHttpReplay` (every
  `IHttpClientFactory` client, an Azure SDK client through `HttpClientTransport`); out of it through
  `MockApiServer.Record` and `Replay`. Matching by method, path, query and JSON body field by field; volatile values
  from two recordings; `model` and `oauth` presets; keys written as `***` and a secret scan before saving; streamed
  answers part by part; a hermetic guard. `uat.config.md`'s `## Dependencies` gives each dependency a mode
  (`BRUINET_DEP_<NAME>` overrides one), and `SqliteTemplate` gives each test its own copy of a seeded SQLite file.
  `bruinet http stale|diff|reviewed`, and the MCP server's `run_scenario {replay}` and `http_calls`. See
  [HTTP Replay](docs/guides/http-replay.md) and AD-034.
- The Todo sample's UAT suites record and replay their backend (AI11b): headless and on bUnit, each scenario in its own
  session, hermetic. An id in a path is volatile, volatile values are bound into later answers, a call that got no
  answer and each answer's duration are recorded. `@needs-stub` skips a scenario that reads its stub back while a run
  records or replays.
- The MCP server's `run_scenario` and `last_runs`: an agent runs a scenario (or a file's) on the suite's
  runner and reads how it went (its address, duration, and the failed step's file, line and text),
  and each scenario's newest result.
- A scenario's `__result.json` gives each step's `durationMs`, and the failed step's `failure`: its kind
  (`assertion`, `locator-not-found`, `timeout`, `app-unavailable`, `bind`, `app-exception`, `error`),
  expected and actual, the locator, the binding, and the first five frames of the suite's and the app's
  code. `BddStepResult.Duration` gives a step's time. The MCP server's `get_failure` reads it, and
  `run_scenario` gives it for each failed scenario.
- The MCP server's `step_start`, `step_move` and `step_screenshot`: the preview's Step through for an agent, each
  call answering where the run paused next or how it ended.
- `Bruinet.Logging`: `AddBruinetStepLog()` adds an `ILoggerProvider` to an app's services that keeps what the app
  logs with the step that made it (`StepLog` in Core; `BddStepResult.AppEvents`). The MauiHeadless head adds it; a
  bUnit or service suite that composes the app adds it there. Playwright's context keeps the browser's console
  errors and warnings and its page errors. A step's `__result.json` has them as `appEvents`, a line said again once
  with how many times; the MCP server's `step_log` and `get_failure` give them. Warnings and up;
  `BRUINET_STEP_LOG=all` keeps information, `off` nothing.
- `bruinet mcp-host`: an agent's session keeps one MCP server while Bruinet changes under it. It runs `bruinet mcp`
  from a snapshot of a build folder; on a new build (`mcp.stamp`) it waits for the calls in flight, starts the server
  again, replays the session's `initialize` and sends `notifications/tools/list_changed`.
- The MCP server's `step_state`: the paused Step-through's page, each control with whether it is shown and
  what it shows (a toggle's state, a range's value, a choice, or text), read by the runner (`BddRunnerReadPage`).
- The MCP server's `coverage`: one scenario run through the gate with .NET's code coverage (what
  `Microsoft.NET.Test.Sdk` brings; nothing to install), and per file the lines it ran and missed, as ranges.
- The MCP server's `failing`: every scenario whose newest run failed, in one suite or all, with the kind of
  failure.
- The MCP server's `tests_affected`: `bruinet tests affected` for an agent, the picked tests with why and
  the filter, from git's changes or the given files.
- The MCP server's `rename_step` (the editor's Rename of a callable step: the edits, then applied and
  checked, put back when a file no longer binds) and `rename_page_object` (the Scraper's rename).
- A scenario outline can have several Examples sections: `### Examples: <name>`, each with a tag
  line of its own that joins the outline's tags for its rows (a head tag picks rows for a head).
  Rows are numbered across the sections, so names, filters and timings stay put. `UAT015` names
  the section; two sections with one name are `UAT032`. The preview shows each section (AD-036).
- `docs/guides/coming-from-reqnroll.md`: one scenario in both forms, Reqnroll's attributes beside
  Bruinet's, what works differently and what is not there.
- Narrated recordings pace each action: while a scene records, every control action (a click, a
  choice, text set, a key) takes at least the script's `Pause` (`BRUINET_PAUSE`, 700 ms by
  default), counted from the call's start, so a list that opens is seen open and a method that does
  three things shows three. A slower action waits nothing more, an action that runs another pauses
  once, reads never pause, and a run that does not record never waits (`ActionPacing`, AD-024).
  The pause is outside the call's logged time, so near-misses are unchanged. Scenes that record at
  once (scenarios in two collections) each keep their pause until they end, whichever ends first.
- A step's line now plays while the step runs; a step without a line follows its beat while that
  line still plays (a loading state checked after a tap is still there), and the next line or the
  outro waits for it, so lines never overlap. A script's `Say | before` keeps the old order, the
  step waiting for its line. Scenes kept before
  this are out of date: their hash now holds the pause and `Say`.
- `[UiFact]` (`Bruinet.Uat`): a fact that leaves a screenshot when it fails, taken the moment the
  test method failed and before the test object is disposed, from the test object's own context
  (public or not) or a fixture's. xUnit v2 tells a `[Fact]`'s `After` hook nothing of the outcome,
  so the case runs through its own invoker. Under `dotnet test` a run's suite folder is the test
  assembly (the one of those referencing xUnit that no other references), Bruinet's own suites
  included.
- `Bruinet.Maui.Testing.PageNavigation`: `WaitArrived` (a pushed page on top, as one logged call)
  and `ReturnToRoot` (back to the app's root page between tests), which MAUI's fixture had as
  loops of its own. `ReturnToRoot` goes back again only while a dialog or a pushed page covers the
  root; a root that is only late gets the whole wait and one back after it, as a last resort,
  where before it got up to three backs and could close the app.
- `CallBudget.Within(windowMs)`: a driver's own wait, cut to what the running call has left. The
  waits below a call that had a window of their own (FlaUI's navigate back, navigation depth,
  bridge resend, dropdown open and close, scroll settle and scroll change, `FindChrome`, MAUI's
  `NavigationRecovery`, UAT's `WaitUntil`) no longer outlast the caller's `timeoutMs`.
- `BRUINET_TIMEOUT_SCALE`: a factor (0.1 to 20) on the waits a call takes from `TimeoutSettings`
  (`DefaultWait`, `PageLoad`, `ElementFind`, `ElementState`), for a slow or shared machine; a
  `timeoutMs` a test passes is not scaled.
- `near-misses.md` in the suite's artifact folder (`NearMisses`): every call of the run that
  passed after trouble, worst first, written when one happens, with no switch. A call that used
  more than its budget's share in a single attempt now counts as a near-miss too (a Playwright
  action that waited inside itself), where only a call of several attempts did.
- A failing Html UAT scenario leaves a screenshot: `PlaywrightTestContext` is an
  `IScreenshotSource` (PNGs in the run's `suites/<suite>/screenshots`), and
  `UatScenarioResults.ScreenshotServiceOf` finds one on the fixture or on its public properties
  (a fixture's `Context`), so a suite gets it without code of its own. `ScreenshotService` takes
  any way of saving one (`Action<string>`). Under `dotnet test` the suite folder is named after the
  test assembly, not `testhost`.
- Predicate waits: `WaitAttribute(name, until)` on every Html, MAUI and native Android control, and
  `WaitText(until)` on Html's and native Android's (a MAUI control's text is its own). One call
  within the budget, polled as `WaitText(expected)` is; when the predicate never held, the call
  answers false and its log exit names the last value read. For a state a page object waited for
  in a loop of its own (a count above zero, a value that changed).
- `AfterScenario` on `UatScenarioTestBase` and `IUatScenarioLifecycle` (a no-op by default): runs after
  each scenario, passed or failed, once its recording is finished, so a suite whose scenarios share
  a fixture with other tests can put back what a scenario changed (sign out) without it showing in
  the video. The IDE runner host calls it when it closes a scenario. When the scenario failed or
  was canceled, its own failure is reported and a failing `AfterScenario` is only traced; the host
  then ends the fixture rather than keeping it.
- `Bruinet.NativeAndroid` in MAUI's and Html's shape (AD-010, AD-013, `docs/platform-guides/native-android.md`):
  its own `INativeAndroid*` interfaces; the Core calls layer (act once, confirm, one log pair); scope
  readiness (the app in front, the page's activity and ready view, the `bruinet_busy` busy view);
  generated controls with Android's widget names (`EditText`, `Spinner`, `RecyclerView`, `RatingBar`,
  `TabLayout`, `AlertDialog`, `PermissionDialog`, ...); collections whose rows follow their item
  when a `RecyclerView` recycles; scroll-to-find with `UiScrollable`; named, element-relative
  gestures. `tools/Scripts/CreateNativeAndroid.Bat` generates the controls. The breaking change is
  total: the old hand-written classes (`NativeAndroidControl`, `NativeAndroidPageObjectBase`, the
  MAUI-named controls) are gone; nothing in the repository used them.
- `Bruinet.Appium` (AD-026 R4): Appium's error mapping (now public, with `IsUnknownCommand` and
  `IsNoAnswer`), `AndroidHost` and `AndroidRecorder`, moved out of `Bruinet.Maui.Appium` so the
  MAUI and native Android stacks share them. `Bruinet.Appium.Tests` took their tests.
- `samples/Bruinet.Samples.NativeAndroid.App`, a .NET for Android control gallery, and
  `tests/Bruinet.NativeAndroid.UITests` (30 UI tests and UAT scenarios on the emulator,
  `docs/run/native-android.md`); the `android-control` and `android-ui-test` skills.
- UAT finds a control by Core's `IElementObject<>` as well as by its name's suffix, and no longer
  takes a page's `Self` for a control.
- One command line, `bruinet` (AD-028, `docs/run/cli.md`): a dotnet tool (`Bruinet.Cli`) with
  `scrape` (`new`, `list`, `status`, `check`, `explore`, `record`, `draft`, `verify`, `config`,
  `install-browser`), `data` (what `bruinet-data` did), `scenario` (`preview`, `results delete`),
  `controls generate` (what `Bruinet.Generator` did), `version`, and the servers `lsp` and `mcp`.
  One set of options (`--solution`, `--project`, `--json`, `--yes`, `--headless`) and exit codes
  (0 fine, 1 problems found, 2 could not run). `tools/Bruinet.Bdd.LanguageServer` is now
  `tools/Bruinet.Cli` (assembly `bruinet`); `tools/Bruinet.DataValidator` and `tools/Bruinet.Generator`
  are folders of it, and their tests are in `tests/Bruinet.Cli.Tests`. The IDE extensions start
  `bruinet.dll lsp`; `bruinet-data`, `--mcp` and `--preview` are gone. `tools/Scripts/bruinet.ps1`
  runs it from source.
- The Scraper, restarted in the IDE extensions (AD-027, `docs/guides/scraper.md`): `Bruinet.Scraper`
  records an app's pages through Playwright's Chromium into the UI test project
  (`scraper.config.md`, `Recordings/`), finds repeated parts, suggests controls and locators, diffs
  captures, writes code and builds it. The language server serves it to a Scraper view in VS Code and
  Visual Studio and to agents as MCP tools; the `scrape-pages` skill and `/bruinet-scrape-pages`
  prompt say how. `Bruinet.Core.Scraping` holds `IScrapeSource` and the snapshot model. The WPF
  Scraper (`tools/Bruinet.Scraper`), its per-user SQLite corpus and its Copilot SDK calls are removed.
- Packaging of the IDE extensions: a release attaches the Visual Studio VSIX and the VS Code VSIX,
  each with the language server inside and stamped with the tag's version. CI builds both on
  every pull request (`ide-extensions`). The server warns on the suite config (`UATG010`) when the
  suite's build references a Bruinet assembly at another major or minor version than its own
  (`BddVersionCheck`, `BddSuite.VersionMismatches`).
- Debugging the app too: while a debugger is attached to a test, `DebuggedApps.Announce`
  (`Bruinet.Core.Diagnostics`) writes each app the test starts to
  `%TEMP%\Bruinet\debugged-apps\<test pid>-<app pid>.json`. The MAUI FlaUI driver and the web host
  announce theirs; the web host skips `dotnet run`, whose process is the CLI. A Debug from the
  Visual Studio preview or from VS Code's Test Explorer attaches to each announced app after the
  testhost, so a breakpoint in the app's own code stops too.
- The VS Code extension (`tools/Bruinet.Bdd.VsCode`, TypeScript):
  - an LSP client for feature, step and config files on the same language server;
  - the same Scenario Preview in a webview, its screenshots through the webview's own URIs;
  - Test Explorer on the scenarios' addresses: build, then `dotnet test --filter BddScenario=...`,
    results from the reports, and Debug attaching the C# debugger to the testhost;
  - step breakpoints in the file Visual Studio uses, with gutter dots.
  The server gains `bruinet/scenarios` (each suite's tests and filters) and `bruinet/lastRuns`
  (a file's last results with the failed step).
- The Bruinet MCP server, for Copilot's agent mode and other agents:
  `Bruinet.Bdd.LanguageServer --mcp [--solution <folder>]` (`BddMcpServer`), over stdio. It has the
  tools `list_suites`, `list_steps` (by words), `list_pages`, `list_test_sets`, `validate_scenario`
  (a draft checked with the gate's words) and `find_step_usages`. It calls no model. The prompt
  file `.github/prompts/bruinet-write-scenario.prompt.md` writes a feature file with them, using
  only the suite's steps. The Scraper's corpus tools (`scraper-in-extension.md`) are not part of it:
  that proposal waits for its owner.
- Config and data files in the editor: `uat.config.md` and `bdd.config.md` get diagnostics (`UATG001`-`UATG009`)
  for what the suite would ignore or reject:
  - an unknown section, field or assembly kind, and a Runtime table without a Fixture;
  - a head, boolean, test set or app row that is not valid, and a missing AppsFile;
  - an assembly not built yet, a `Using` namespace nothing is in, and a suite that cannot bind;
  - a `testdata.schema.json` out of date with its definition, which a quick fix regenerates as
    `bruinet-data schema` would.
  Completion and hover cover the fields, their values (heads, booleans, test sets), the assembly
  kinds, the namespaces and the apps' heads. A feature file's `TestData` metadata offers the test
  sets. `BddSuite.TestDataDefinitions` loads the suite's test-data definitions with its build, each
  placed at its project rather than in the shadow copy.
- BDD Explorer (Visual Studio, **View > Other Windows > BDD Explorer**) shows three views of a suite,
  and any entry with a source opens it:
  - **Steps**: the steps grouped by their class, page, control verbs and built-ins, then the
    callable steps by step file, and the hooks of each step-definition class with their tags;
  - **Pages**: each page with its controls (type, where tapping goes) and its page phrases;
  - **Results**: the last run of each scenario.
  The language server renders them (`bruinet/explorerHost`, `bruinet/explorer`), and the window
  refreshes after a build and when a run writes results.
- Quick fixes and refactorings in the editor (`textDocument/codeAction`, `prepareRename`, `rename`):
  - a value with one right answer is rewritten, as `bruinet-data validate --fix` does;
  - old headings are rewritten, and an old file name is renamed (UAT029) when the client can
    rename files;
  - **Create step definition** for a step that binds to nothing puts a stub, with its phrase and a
    placeholder per quoted value and number, into the suite's `[BddStepDefinitions]` class;
  - **Extract** turns selected steps into a `## Step:` of the file with a call in their place;
  - **Inline** puts a call's body in its place, with the call's values;
  - **Rename** a callable step renames its heading and every call, each call keeping its values.
    A new phrase that drops a placeholder is refused.
  `BddSuite.StepDefinitionTypes` lists the loaded suite's step-definition classes.
- CodeLens from C# to scenarios (Visual Studio): "used in N BDD steps · M scenarios" above step
  definitions, page phrases and page-object controls, with the steps in its details.
  `bruinet/usages` in the language server counts across every suite. A new assembly,
  `Bruinet.Bdd.VisualStudio.CodeLens`, runs in Visual Studio's CodeLens process and reaches the
  server through the extension's `ICodeLensCallbackListener`.
- Hover, go to definition and find references in the editor (`Bruinet.Bdd.LanguageServer`). Hover
  shows what a step binds to, its page and app, the method's XML `<summary>`, and each argument as
  written and as read. A call shows the callable and its body. Definition opens a C# step's method
  through the portable PDB (`BddSourceLocator` in `Bruinet.Bdd.Workspace`, which also reads a PDB
  beside a renamed assembly), a control verb's control on the page at that line, a page step's page
  class, and a call's `## Step:` heading. References finds every step in the suite's feature files,
  linked files included, that binds to the same method or calls the same callable.
  `BddSuite.FindType` finds a type of the loaded suite, and the shadow copy now takes the XML doc
  files too.
- Completion in the editor (`textDocument/completion`): keywords, sections, and step texts by
  words (every typed word in the phrase, not only a prefix) as snippets with `{x}` tab stops. The
  current page's steps come first, and the file's callable steps are included. The values of the
  parameter at the caret are offered too: pages, the controls of the page the binder has the
  scenario on at that line, users, apps, enum members, and an outline's Examples columns.
- Outline, folding and semantic colours in the editor (`Bruinet.Bdd.LanguageServer`):
  `textDocument/documentSymbol` gives the heading tree by kind, `foldingRange` folds the sections,
  fences, tables and doc strings, and `semanticTokens/full` colours keywords, quoted values, `{x}`
  and `<x>` placeholders, tags and comments. They come from the file's shape (`BddDocumentStructure`),
  so they work before a build and while the file does not parse.
- Diagnostics in the editor (Visual Studio, `Bruinet.Bdd.LanguageServer`): feature, step and config
  files get the `bruinet-bdd` content type (derived from Markdown) and an LSP client. The server
  publishes the gate's diagnostics word for word: the open file after a pause in typing, and every
  file of its suites on save and after a build. A step that failed in the last run is a `UATRUN`
  error at its line. `BddFileAnalysis` (`Bruinet.Bdd.Preview`) is the one check the preview and the
  server share. The server only computes diagnostics for a client that declares it takes them.
- Debug from the preview (Visual Studio): **Debug** beside each **Run** and **Debug file** beside
  **Run file** run with `VSTEST_HOST_DEBUG` and attach Visual Studio's debugger to the testhost; a
  failed attach cancels the run. Step breakpoints are toggled by clicking a step's keyword in the
  preview, or with **Toggle BDD Step Breakpoint** (editor context menu, **Ctrl+K, F9**) on a step
  line. Either way they are written to `%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json`, and a red dot
  shows in the preview's gutter and in the editor's glyph margin. Breakpoints move with their lines
  as the file is edited.
- Run from the preview (Visual Studio): **Run** on each scenario heading and **Run file** on the title
  build the solution, then run `dotnet test --no-build --filter BddScenario=<address>` with the output
  in the Output pane. One run at a time; a build started during a run is cancelled; **Cancel run**
  stops the test process tree. `bruinet/runTarget` gives the project and filter; a suite whose test
  class keeps `[Theory]` shows the buttons disabled, saying why (`BddSuite.HasScenarioTheory`).
- Step breakpoints (`Bruinet.Bdd`): while a debugger is attached, `BddStepExecutionSession.RunNextAsync`
  stops before a step whose line `%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json` lists, with a
  `BddStepBreak` local (text, arguments, app, page, call chain); a line in a `.steps.md` stops at each
  call, a call's line once before its first step, and `breakOnFailure` after a failed step. The
  check is behind `IBddBreakHook` (`BddExecutionContext.Breaks`); without a debugger nothing is read.
- `[UatScenarioTheory]` (`Bruinet.Uat`): `[Theory]` with a stable address per scenario, the `BddScenario`
  trait `<file relative to the config>#<slug>` (`BddScenarioAddress` in `Bruinet.Bdd`), so
  `dotnet test --filter "BddScenario=..."` runs one scenario. A case whose feature file is the
  project's own has the scenario's line as its source. The Todo MAUI, web, mixed and service suites
  use it; their cases and counts are unchanged.
- `Bruinet.Bdd.Workspace` (tools/): loads a suite one way for every tool. It finds the suites (projects
  with a `uat.config.md` or `bdd.config.md`), reads the config, knows which suites a feature file
  is linked into, and builds the catalogs from a shadow copy of the build output in a collectible
  load context, so a build never meets a locked file and `Reload` releases the old build.
  `bruinet-data` loads suites through it; its output is unchanged.
- `Bruinet.Bdd.Preview` (tools/): renders a feature or step file as HTML for the IDE preview, from the
  engine's parse: steps with the keyword in a gutter, quoted values, `{x}` arguments and `<x>`
  placeholders marked, tables and doc strings, tags, the metadata table, `## Step:` sections, and
  each parse diagnostic at its line (UAT029 with its fix for the old forms). Markdig renders the
  Markdown around, with raw HTML off; every block has `data-line`. The stylesheet's colours are
  CSS variables with light, dark and high-contrast values.
  With the suite's catalogs it binds the file (unsaved text too) with the gate's own check: each
  step says what it binds to and on which page, the page shows where it changes, a step that binds
  to nothing shows the gate's diagnostic word for word, a callable call unfolds into its body with
  the values filled in, and each Scenario Outline row can be opened. `Bruinet.Bdd` gains
  `BddScenarioFiles.Load(path, text)`, `ParsePhrases(path, text)`, `CheckPhrases` and
  `BddLoadedScenarioFile.Check` (the gate's per-file check, which `UatScenarioCheck` now calls), and
  `BddBindResult.Scenarios`: every scenario as far as it bound, errors or not.
- `Bruinet.Bdd.LanguageServer` (tools/): the one .NET process the IDEs talk to, JSON-RPC over stdio with
  LSP framing (StreamJsonRpc). It serves `initialize` (the suites under the solution), full-text
  document sync, `bruinet/preview` (the file rendered, bound in its suite or parsed only with the
  reason; a file linked into several suites offers each) and `bruinet/buildDone` (reload the
  catalogs). `--preview <file> --out <page.html>` renders one file as a page without an IDE.
- `Bruinet.Bdd.VisualStudio` (tools/, its own `.slnx`, built with Visual Studio's MSBuild): the Scenario
  Preview tool window for Visual Studio 2022 17.10+ and 2026. A WebView2 shows the language
  server's preview of the active feature or step file; it renders again after a pause in typing and
  after each build, keeps the caret and the preview in step both ways, follows the VS theme, and
  offers a suite picker for a linked file. **Ctrl+K, V** opens it. The VSIX carries the server.
  `BddPreview.HostPage` is the webview's page (the same for VS Code) and `bruinet/previewHost`
  serves it. Guide: "Scenario Preview In Visual Studio".
- UAT reports name their file, lines and steps: `__result.json` is named after the feature file and the
  scenario (two files with a scenario of the same name no longer share one), and always holds the
  file, the scenario's line, the duration, and each step's line, status and message (`callLine` for
  a callable's step). The binding, catalog and discovery stay behind `IncludeRuntimeTrace`. The
  manifest record gains `suite`, `file` and `line`. The preview shows the newest run of each
  scenario: step statuses with text alternatives, the scenario's result and duration, a failed
  step's message and screenshot; the server finds it through the manifests and sends
  `bruinet/resultsChanged` when a run writes one.
- Docs: AD-020 (a tier is a head: `service`, opt-in heads, UATB013, a scenario counts for each of
  its tiers) and AD-021 (`Bruinet.Bdd` under `Bruinet.Uat`), with their invariants; the guide
  "Features Below The UI". AD-011, AD-014, AD-017 and AD-019 use the new words and file names.
- Hooks on step-definition classes (`Bruinet.Bdd`): `[BddBeforeFeature]` (static, once per feature
  file per run), `[BddBeforeScenario(tags)]`, `[BddAfterScenario(tags)]` and `[BddAfterStep(tags)]`,
  after the test base's own overrides and in class and method order.
- `BddSets.AssertSet(expected, actual, ordered)`: one diff for two lists (missing, extra, the closest
  item's different cells). The server-order step uses it. `BddStepDefinitionRuntime.FromTypes` and
  `CreateCommandCatalog` build a catalog of step-definition classes without pages.
- Todo sample: UI tests and UAT scenarios for users (TOD.11): `UserListPage`, `UserEditPage` and
  `UserList` in the `Users` namespace, `UsersJourneyTests` (8), `TOD.11-manage-users.uat.md`
  (8 scenarios with `Using | Users`), and an expected-failure file without the `Using` that must
  fail binding. The user form's date and time pickers publish `SetDate` and `SetTime` through the
  gesture bridge. `true`/`false` values may be written `on`/`off`.
- UAT `Using` (AD-019): a scenario file's `Using` metadata row and the config's `## Using` table
  name the namespaces a file sees besides its folder's. A `Using` that names nothing is `UATB011`;
  one no step needs is a `UATB012` warning. Pages have namespaces too (`[UatNamespace]` on the page
  class or assembly, now in `Bruinet.Core.Testing` and allowed on a class): one name in two
  namespaces is two pages, a step names the one it sees, or `Accounts/Detail` when it sees both.
  A failure report names each step's scope (`[Invoicing]`, `[page Todo List]`);
  `bruinet-data phrases <file>` lists only what that file sees, by namespace, and
  `bruinet-data references <namespace>` lists who uses one. The Todo suites put their backend,
  device, app-lifecycle and stored-data phrases in namespaces, used by every file through
  `## Using`.
- UAT folder namespaces: a folder under `Scenarios/` is a namespace, and a scenario sees the
  phrases of its own folder and the folders above it. `*.phrases.md` files may sit in any folder
  (copy them with `Scenarios\**\*.phrases.md`); `[UatPhraseClass(Namespace = "Invoicing")]` and
  `[assembly: UatNamespace("Invoicing")]` put a C# phrase class in one. A callable's body binds
  where it is written. A step that matches only another folder's phrases is `UATB010`, naming the
  folder; a phrase of the folder and a wider one for one step are ambiguous. `bruinet-data`
  checks phrase files in subfolders and lists each phrase's namespace.
- UAT page scope: control verbs and page phrases bind against the current page, which the binder
  follows through the steps before running (the page a step names, a returned page object,
  `[UatNavigatesTo]` on a control, a callable's steps). A missing control, a verb the control
  lacks, or a page phrase of another page is now a bind error (`UATB009`) instead of a run
  failure; a page-scoped step with no known page is `UATB008`, naming the step that may have left
  it. A phrase of the page that also matches a wider phrase is ambiguous (`UATB002`).
  `Navigates` on `[UatWhen]` marks a verb that can leave the page (`I tap`, the dialogs' `I choose`).
  `UatScenarioTestBase.RunExpectedBindFailureUatFile` checks a file that must fail binding.
- Todo sample: users (journey TOD.11). A Users page and a user edit page with one field of each
  kind a test drives (`RadioButton`, `CheckBox` + `DatePicker`, `Picker`, `Stepper`, a decimal
  `Entry`, `TimePicker`, `Switch`, `Slider`, `Editor`); users are managed by `Todo.Api`
  (`/api/users`, a validation problem per field, 409 for a taken name), cached in schema v3 and
  fetched first by a sync; the `team` test set. Unit, integration and contract tests only; the UI
  tests and UAT scenarios follow phrase namespaces.
- Callable phrases (AD-017): a tester names a sequence of steps under `## Phrase:` in a scenario
  file, or in `Scenarios/*.phrases.md` for every scenario, and calls it like a phrase. Its values
  are typed by the steps they go to (or an optional `Parameters` table), `<table>` / `<doc string>`
  pass the call's block on, and `## Phrase for each row:` runs the body once per row of the call's
  table. Calls are expanded and checked at bind time; a problem inside one is reported at the
  call with `via` lines to the body. No control flow, three levels deep at most, no shadowing.
  Broken callables (`UATC001`-`UATC008`) fail only their calls. The trace indents a call's steps,
  `bruinet-data validate` checks phrase files and `phrases` lists callables with their types.
- Typed UAT arguments (AD-016): a phrase's `{name}` takes its type from the method parameter, and
  every step value, Examples cell and table cell is read by `BddValues` in one invariant format -
  dates `12-May-2026` (or `today`, `+2d`), numbers `1,250.75`, enums by name with spaces, lists
  `"A", "B" and "C"`. Values are checked when a step binds (`UATA001`-`UATA007`), before any app
  starts. Record and list parameters take the step's table (`Field | Value` for one record), a
  string parameter the phrase does not name takes a `"""` doc string (JSON for a record),
  `BddUserRole` is a user from the `Apps` table, and `[BddArgumentType]` / `IBddArgumentConverter<T>`
  add app types. `IBddClock` gives relative dates the fixture's now.
- Phrases from method names: an empty `[UatGiven]`/`[UatWhen]`/`[UatThen]` makes the method name the
  phrase, parameters in capitals (`I_open_the_todo_TITLE(string title)`).
- Broken phrases (`UATA010`-`UATA013`) no longer stop the catalog: the phrase is marked and only a
  step that binds to it fails.
- `bruinet-data validate` checks scenario files too (parse, bind, arguments), `--fix` rewrites
  values with one right answer, `phrases` lists the phrases with argument types, and `snippet`
  writes a method for a step that binds to nothing.
- UAT subjects (AD-015): a step that starts with an app or user from the `Apps` table runs on
  that app, through a phrase declared with `{user}` first: `Daan taps Save` binds to
  `{user} taps {control}` on Daan's app, and the app stays current. Every built-in `I ...` control
  verb and page step has its `{user}` form; an app adds a second keyword attribute for its own.
  `{user}` is the acting user, and never a control verb's value. `Apps` gains the
  `User` and `Head` columns, and `AppsFile` in `Runtime` reads the table from a shared file.
  New bind errors `UATB005` (an app or user named like a page or control), `UATB006` (a subject
  that picks no app) and `UATB007` (an unknown `{user}`, or one in a suite without an `Apps`
  table).
- UAT mixed suites: `Head | mixed` runs only `@mixed` scenarios, and each app binds against its
  own head's pages (`IBddSessionHost.CompositionFor`, `BddCatalogSet`, AD-014). Each app keeps its
  own current page, and the result JSON records the app of every step.
- `TestAppDefinition`, `ITestApp` and `TestAppSet` (`Bruinet.Core.Testing`): the apps of a
  fixture, started on first use, reset between tests, found by name or user, and captured
  together on a failure (`CaptureAll`, `ScreenshotTestAttribute.SetApps`). `IUatAppsHost` gives a
  UAT fixture its sessions from a set. `MarkdownTables` (`Bruinet.Core.Utilities`) reads the
  tables of `uat.config.md` and `apps.md`.
- `TestComposition.ForFixture(fixture, options)` and `TestCompositionDiscoveryOptions.FromScans`:
  a composition of named modules, one per head.
- MAUI: `MauiTestContextOptions.Name` (the app's name in the call log) and
  `MauiDriverOptions.Placement`, a window placement for one launch; `BRUINET_AUT_PLACE` gains
  `left-half` and `right-half`.
- The Todo sample's mixed projects: `TestSupport.Apps` (`apps.md`, `TodoAppsFixture`,
  `TodoMauiDevice`), `Mixed.UITests` and `Mixed.Uat`: Windows, web and Android apps in one test.
- `[UatGiven]`, `[UatWhen]` and `[UatThen]` (`Bruinet.Core.Testing`): the one way to declare a
  UAT step, on control methods, fixture methods and phrase-class methods alike (AD-011).
- Generated Html and Blazor controls: every Html and Blazor control is a `.tpl.cs` template and a
  generated `.gen.cs`, as on MAUI (`tools/Scripts/CreateHtml.Bat`). The Html capability bases
  implement the same Core interfaces as MAUI's (`IClickableControlObject`,
  `IFocusableControlObject`, `IToggleControlObject`, `IRangeControlObject`,
  `ISelectorControlObject`, `IEditableTextControlObject`, `ITextControlObject`), so the public
  surface matches and every built-in UAT phrase works on Html, `Enter` and `AssertSelectedText`
  included.
- Html `ContainerBase`: `IsExists`, `IsVisible`, `WaitExists`, `WaitVisible`, `AssertExists`,
  `AssertVisible`, as on MAUI containers. `CollectionObjectBase.WaitNoItemWhere`.
- `WebAppHost.FindBuiltApp`: the built assembly of a web app in the repository, for fixtures that
  start one.
- UAT heads: `Head` in `uat.config.md` and the `@maui` / `@web` tags. One scenario file runs on
  every head it is tagged for and is skipped on the others.
- UAT with several apps in one scenario: an `Apps` table in `uat.config.md`, `IBddSessionHost`
  on the fixture, and the built-in step `{app} uses the app`. A failure names the app.
- The Todo sample's UAT on the web head (`Bruinet.Samples.Todo.Web.Uat`): the MAUI suite's
  scenario files, run through the web page objects, and a two-device scenario (Alice and Bob on
  one backend). The web detail page has a Back button, as the MAUI navigation bar does.
- Html on the call model (`.my/html/plan.md`): `Bruinet.Html` owns its interfaces
  (`IHtmlElement` with `InstanceKey`, `IHtmlDriver`, `IHtmlElementScope`, `IHtmlPage`,
  `IHtmlTestContext`) and runs every control, page, container and row member as one call
  (`Bruinet.Core.Calls`), with near-miss warnings and `BRUINET_CALL_LOG` (AD-009, AD-010).
- Html exceptions: `StaleElementException`, `ElementNotReadyException`,
  `ScopeNotReadyException`, `AppUnavailableException`, `RouteUnavailableException`. A closed
  browser is `AppUnavailableException` and ends the call at once.
- Html scope readiness: `ScopeReadiness`, the `data-busy` busy signal (`BusySignal`; Blazor
  pages require it, so a prerendered page is not acted on), `AppRoot`, `FrameScope<T>` for
  iframes, `ContainerBase.ProbeContentReadiness` and `AsksParent`.
- Html collections: `CollectionObjectBase`, `ItemObjectBase` and `ItemKey`. A row follows its
  item by `data-item-key`, and never acts on another.
- `ControlBase.ActAndConfirm`: Html actions that change state confirm their effect and are
  never repeated. `SetText`, `TypeText`, `Append`, `Clear`, `SetChecked`, `Toggle`,
  `SelectByValue`, `SelectByText`, `SelectByIndex`, `SetValue`, `Focus` and `Blur` use it.
- `Bruinet.Html.Testing.WebAppHost`: starts a web app on a free port with explicit launch
  settings.
- The Todo sample's web head (`samples/Todo/src/Bruinet.Samples.Todo.Web`) and its UI tests;
  `X-Todo-Client` (`TODO_CLIENT_ID`) and `Todo.Api`'s request log.
- `html-control` and `html-ui-test` skills.
- `Bruinet.Maui.CommunityToolkit` control objects for CommunityToolkit.Maui: `Expander`,
  `AvatarView`, `RatingView`, `DrawingView`, `Popup`, `StateContainer` and `MediaElement`, each
  with a sample page section and Windows UI tests. Routes and platform gaps are in
  `docs/controls/community-toolkit.md`.
- Sample app `ToolkitVerbSink`: answers bridge verbs for toolkit views that AppSupport cannot know.
- Generator shortcuts: a component declares `protected bool? IsPlayingShortcut() =>
  PlayPauseButton.IsPlaying();` and gets the public trio as single calls on the part, with no
  second unit of work around it.
- Ordered comparisons `GreaterThan`, `AtLeast`, `LessThan`, `AtMost` for
  `[GenerateComparisons]`; `RangeControlBase.GetValue` declares all four
  (`WaitValueGreaterThan`, ...).
- `MediaPlayPauseButton` and `MediaTimeLabel` part controls; `MediaElement.Stop`,
  `WaitOpened`, `GetElapsed`, `GetRemaining` and their comparison variants.
- `Confirm(read, done, timeoutMs)` on `ViewBase` and `RootedScopeBase`: the one way a Core
  method waits for its action's effect. It never repeats the action, and returns a
  `Confirmation<T>` (`Confirmed`, `NotConfirmed`, `Replaced`) whose `Failure(...)` reports a
  replaced element as `StaleElementException`.
- MAUI's own interfaces: `IMauiElement` (with `InstanceKey`), `IMauiDriver`, `IMauiElementScope`,
  `IMauiPage`, `IMauiTestContext`, and `MauiElementExtensions` for the geometry and scope helpers
  (AD-010).
- MAUI exceptions: `StaleElementException`, `ElementNotReadyException` (`NotReadyReason`),
  `ScopeNotReadyException` (with `Readiness`), `AppUnavailableException`, raised by both drivers.
- Scope readiness: `ScopeReadiness` / `ScopeReadinessState`, `ProbeReadiness()` on every scope,
  `ProbeContentReadiness(root)` and `AsksParent` for containers.
- Collections: `ItemKey` on every row (`IMauiItemObject.Key`), `IItemStrategy.HasStableIds`,
  `timeoutMs` on `Item(int)`, `FindItem` and `ItemWhere`.
- Near-miss warnings: a call that passed after three or more replacements, or after using more
  than half its budget, logs `LogResult.Warning`. The thresholds are settings:
  `MauiTestContextOptions.NearMiss` (`NearMissSettings`), read through `IMauiTestContext.NearMiss`. `BRUINET_CALL_LOG` (a folder) makes
  `MauiTestContext` write every call to CSV.
- `tools/Scripts/repeat-run.ps1`: runs a test project N times and summarizes failures by test.
- AD-009 (app bugs stay failures) and AD-010 (MAUI ahead of Core, on purpose).
- Generator: `[AbsenceTolerant]` on a `Get*Core` reads once with the element resolved
  optionally (`RunGetWithOptionalElement`); `Is*Core` forwards extra parameters like `Get*Core`;
  state shortcuts may take parameters. The CLI prints a warning for a Core method that calls a
  `Run*` helper or a part's public member.
- `AvatarInitials` and `AvatarImage` part controls; `ContentDialog` gains `WaitTitle`,
  `AssertTitle`, `AssertButtonTexts` (sequence), `...HasItem`, `...Count`, `WaitMessage`,
  `AssertMessage`.
- Initial release of Bruinet UI testing framework
- `Bruinet.Core` - Core abstractions and interfaces
  - `IDriverAdapter` and `IElementAdapter` for platform abstraction
  - `ITestContext` with platform identification
  - Control interfaces: `IButton`, `ICheckBox`, `ITextBox`, etc.
  - Test attributes: `[UITest]`, `[SmokeTest]`, `[Platform]`, `[Priority]`
  - Exception types for common UI test failures
  - Screenshot service abstractions
  - CSV test logging support
  
- `Bruinet.Wpf` - WPF application automation
  - FlaUI-based driver and element adapters
  - Control implementations: Button, CheckBox, ComboBox, ListBox, etc.
  - Page Object base class with control discovery
  - Visual validation support
  
- `Bruinet.Html` - Web application automation
  - Selenium WebDriver-based implementation
  - HTML control wrappers
  - Support for Chrome, Firefox, Edge browsers
  
- `Bruinet.Maui` - Mobile/MAUI automation
  - Appium-based driver implementation
  - MAUI control wrappers
  - Android and iOS support
  
- `Bruinet.Mocking` - API mocking
  - WireMock.Net integration
  - Fluent API stub builder
  - Request/response recording

### Changed
- MAUI's controls and containers ask the driver what the platform's tree is like instead of
  branching on the platform: `IMauiDriver.OmitsOffscreenElements`, `FoundMeansVisible`,
  `ChildrenPath`, `AlertButtons`, `DrawsRatingStars`, `ActivatesRowsThroughContainers`,
  `IndicatorDots` and `PageControl`, each with the Windows answer as its default, as
  `ShellChrome` already did. A driver for a new platform answers them; no control changes.
- MAUI on Android: an action on a control outside an open dialog is not ready, "covered by a
  dialog", and the call keeps asking, where before the tap landed on the dialog and read as
  "accepted and did not take it". One dialog lookup an action (20 ms on an emulator); a control
  inside the dialog is acted on as before.
- MAUI's Appium driver keeps no clock of its own: the picker dialog's wait, the iOS wheels' wait
  and `FindChrome`/`FindAllChrome` (once Selenium's `WebDriverWait`) are `Settle.Until`, within
  what the running call has left. An Android time picker's fields are typed without a clear first
  (UiAutomator2 replaces the text) and its AM label is looked up once: `SetTime` 5.5 s to 5.0 s
  on an emulator.
- MAUI on Android scrolls with one routine (`AppiumMauiDriver.ScrollUntil`) for scroll-to-find
  and an element's scroll into view, bounded by the running call's budget (R2): a miss used to
  run past it, up to 8 s in a 5 s call. A swipe moves 90% of the scroller (from 70%) and is
  followed by one lookup that asks for the resource id and the content description together;
  the settle after a hit is cut to the call's budget too. A test whose call crosses a long page
  passes a longer `timeoutMs` (the gallery's `ScrollTests`, 10 s).
- A lost Appium session is told by its type: `AppiumErrors.Live` (in `Bruinet.Appium`) reads the
  server's messages once and raises `AppUnavailableException` for an ended or stuck session
  (a command not answered in time, which MAUI did not map before) and `StaleElementException` for
  a replaced element. MAUI's Appium element and driver (find, back, the root check, scripts,
  screenshots, page source, navigation, reset) and native Android's element use it;
  `MauiTestFixtureBase.IsSessionLost` tests the type, through wrapped errors.
- MAUI's session settings are the factory's (`MauiDriverFactory.SessionDefaults`): Android's
  idle wait 100 ms (from 500) and window animations off; iOS quiescence off and its idle wait
  bounded to 1 s (not yet run on a simulator). A suite's own `AdditionalCapabilities` override
  them; the MAUI and Todo fixtures no longer set the idle wait themselves.
- **Breaking for the WPF, WinForms and Stride stacks' users** (AD-029): the first model's
  interfaces and waits moved to a new package, `Bruinet.Core.Legacy`, in the same namespaces:
  `IDriver`, `IElement`, `IElementScope`, `IPageObject`, `IContainerControl`, `IContainerObject`,
  `IPagedScope`, `IRangePatternElement`, `ITestContext<T>`, `PageReadinessSnapshot`,
  `Bruinet.Core.Abstractions.Controls.ControlObjectBase`, the `IElement` geometry helpers
  (`LegacyElementGeometryExtensions`) and `WaitHelper.WaitFor*` (now `LegacyWaitHelper.WaitFor*`).
  Core keeps the non-generic `ITestContext`, `BusySignalPolicy`, `WaitHelper.Pause` and
  `ElementGeometryExtensions.CenterOf`, and adds `Settle.Until` for a wait below a call.
- The recorded interactions are kept per recording target (`Interactions.Begin(target, log)`),
  where they were one log for the process: two collections recording at once no longer write each
  other's taps into their scenes. A report names its target where its element knows it (a
  Playwright element its page, an Appium MAUI element its device); one that does not goes to the
  only recording there is, and to none while several record.
- A control's calls run in Core, once for every stack (AD-010 amended): `ControlCalls<TElement>`
  in `Bruinet.Core.Calls`, with Html's `ControlBase` and MAUI's and native Android's `ViewBase` as
  its `IControlHost`. The `Run*` members keep their names and signatures; the three bases lost
  about 860 lines between them. Html and native Android now count a replaced element as MAUI did
  (an observation after the first attempt carries the element's identity), so the near-miss rule's
  three replacements apply there too. MAUI's `RootedScopeBase` (containers, collections, rooted
  pages) runs its `Run*` members on `ControlCalls` the same way, over its root.
- `PlaywrightTestContext.ResetAppState` clears the current origin's `localStorage` and
  `sessionStorage` as well as the cookies, so a value a page kept does not carry from one scenario
  to the next. The browser context stays, with its HTTP cache: a new context per scenario was
  tried and made Tomorrow's run a fifth slower, every script and font loaded cold.
- Html actions on Playwright leave visibility to Playwright's actionability wait instead of
  checking it first (one round trip fewer per action). A Playwright timeout before the action
  ran is `ElementNotReadyException` naming the control and Playwright's reason (not visible, not
  enabled, what intercepts pointer events), where it was a raw `System.TimeoutException`. A
  timeout after the action ran, while Playwright waited for the navigation it started, is the
  action done and is not repeated.
- An Html page's readiness probe is one round trip on Playwright: `IHtmlPageProbe.ProbePage` reads
  `document.readyState` and the body's `data-busy` in one evaluation, and `HtmlPageObjectBase`
  uses it when its context answers it. A page is loaded when its URL matches and the document is
  `complete`; the browser's network idle (a 10 ms wait that threw while a page loaded) is no
  longer asked. Contexts without the probe (bUnit's, mocks) read idle and the body as before.
- The poll's pause between attempts (`WaitHelper.Pause`) blocks with `Thread.Sleep` instead of
  spinning, so parallel collections no longer keep a core busy each between attempts.
- Gesture bridge targets may share an `AutomationId` (`.docs/contracts/bridge.md`).
  `BruinetUiaBridge.Register` adds instead of replacing; the host withdraws an element's own
  target before re-publishing it. A new meta verb `GetBounds` (4) is answered for every target,
  and `IBruinetVerbTarget.TryGetScreenBounds` is required. The FlaUI client picks the target
  nearest the real element when several match.
- Presenter runs a suite in a runner process of its own (AD-025): `Bruinet.Uat.dll` has an entry
  point and is started in the suite's output with the suite's `runtimeconfig.json` and `deps.json`,
  so a scenario runs from Presenter as under `dotnet test`. Presenter drives it a step at a time
  over a named pipe (`Bruinet.Bdd.Runner`); the runner paces and records. The process ends with
  the run: Presenter no longer grows with every run of a suite with a mock server or a database,
  and the suite can be rebuilt while Presenter is open. `AssemblyFile.Record` and `PathOf` are gone.
- **Breaking: `Uat` names are only for `Bruinet.Uat`.** Bdd is the main and Uat a specialization
  of it (AD-021), so everything outside `Bruinet.Uat` takes `Bdd` names. No old names are kept.
  - The 111 `Uat*` types of `Bruinet.Bdd` and `Bruinet.Core` are `Bdd*`: `BddBinder`,
    `BddCatalogSet`, `BddMarkdownParser`, `IBddClock`, `BddEffectiveStepKeyword`,
    `[BddIgnore]`, `[BddArgumentType]` and the rest. So are the Presenter's types and the Todo
    sample's orderer.
  - The run address trait is `BddScenario=<file>#<slug>`.
  - The tool projects are `Bruinet.Bdd.Workspace`, `Bruinet.Bdd.Preview`,
    `Bruinet.Bdd.LanguageServer`, `Bruinet.Bdd.VisualStudio` (and `.CodeLens`) and
    `Bruinet.Bdd.VsCode`, and their types are `Bdd*`. The test project
    `Bruinet.Presenter.Uat.Tests` is `Bruinet.Presenter.Bdd.Tests`.
  - The IDE extensions are "Bruinet BDD", with new identities, so uninstall the old ones: the
    BDD Explorer, **Toggle BDD Step Breakpoint**, `bruinetBdd.*` settings, the MCP server
    `bruinet-bdd`, and `%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json`.
  - `UatPlatformsAttribute` keeps its name for now.
- The feature-file engine is its own package, `Bruinet.Bdd`: parser and models, values, argument
  types, tables, the catalog and binder, callable steps, namespaces, config, execution and runner,
  the step-definition runtime, `[BddStepDefinitions]`, `BddStepDefinitionsBase`, `[BddIgnore]`,
  `IBddClock` and `IBddSessionHost`, in the namespace `Bruinet.Bdd`. `Bruinet.Uat` depends on it and
  keeps pages, controls, page scope (behind `IStepScopeModel` / `IStepScope` on the catalog's
  `Scope`), apps, name inference and the UAT test base. A project that uses `Bruinet.Uat` types adds
  `using Bruinet.Bdd;` (or `<Using Include="Bruinet.Bdd" />`). Type names are unchanged.
- The folder of feature and step files is `Features/`, not `Scenarios/`: the default of
  `BddScenarioSource`, `UatScenarioTestBase`, `UatSpecFormatTestBase` and the tools. The files stay in
  the projects that own them. `bruinet-data validate` reports an old `Scenarios/` folder beside the
  config as `UAT029`, and `--fix` renames it.
- Feature and step files use the BDD words (D5 of the Bdd plan), with no period where both are
  read: `*.uat.md` is `*.feature.md` with `# Feature:`, `*.phrases.md` is `*.steps.md` with
  `# Steps:`, and `## Phrase:` / `## Phrase for each row:` are `## Step:` / `## Step for each row:`.
  An old heading or file name is `UAT029`; `bruinet-data validate --fix` renames the file and
  rewrites its headings. A service suite's config is `bdd.config.md`; `uat.config.md` stays for
  UI suites, and the tools read either.
- Step attributes use the BDD words (D3 of the Bdd plan), with no aliases: `[UatGiven]`/`[UatWhen]`/
  `[UatThen]` are `[BddGiven]`/`[BddWhen]`/`[BddThen]`, `UatPhraseAttribute` is `BddStepAttribute`,
  `[UatPhraseClass]` is `[BddStepDefinitions]`, `UatPhraseClassBase` is `BddStepDefinitionsBase` and
  `[UatNamespace]` is `[BddNamespace]`, all still in `Bruinet.Core.Testing`. `[UatNavigatesTo]` keeps
  its name. The guide `uat-phrases-and-flows.md` is now `uat-step-definitions.md`, beside a new
  `bdd-glossary.md`.
- `BddTableBinding` reads cells with `BddValues`: table dates are written `12-May-2026`, not
  `2026-05-12`, and failure tables print values that way. A phrase method with a `CommandId` is a
  broken phrase instead of a catalog exception; `UatDiscovery` reports it as a warning.
- `UatPhraseAttribute` is abstract, lives in `Bruinet.Core.Testing`, has a non-null `Keyword`
  and carries `CommandId`, `Literal` and `HasLiteral`. A phrase method that sets any of those
  three fails discovery with an error naming the method (`UATD004` in `UatDiscovery`).
  `UatCommandBinding.Keyword` and `BddPhraseMethodPhrase.Keyword` are non-null.
- **Breaking:** Html's `ControlBase` `Run*` helpers have MAUI's names and signatures (R8):
  `RunWaitWithElement` and `RunWaitWithOptionalElement` take `expected` first, and
  `RunGetWithOptionalElement` is new. `RunWithElement` and `RunAssert(Action<IHtmlElement>)` are
  gone; use `RunGetWithElement`, `RunDoWithElement` and `RunAssertWithElement`.
- **Breaking:** Html and Blazor control members follow the generated names: `LabelControl`'s
  `AssertTextContaining` and `IsTextContaining` are `AssertTextContains` and `WaitTextContains`;
  `LinkControl.Href` is `GetHref()`; `ListControl.ItemCount` and `TableControl.RowCount` /
  `ColumnCount` are `GetItemCount()`, `GetRowCount()`, `GetColumnCount()`; `RangeControlBase` reads
  and sets `double?` (`GetValue`, `SetValue`, `GetMinimum`, `GetMaximum`, `GetStep`, `Increment`,
  `Decrement`); date and time inputs read and set `DateOnly?` / `TimeOnly?` (`GetDate`,
  `SetDate`, `GetMinimum`, ...); `TextAreaControl.AppendText` is `Append`; `SelectMultiple` takes
  an array; `IFrameControl.ElementExistsInside` is `IsElementInside`; `NavMenuControl.HasItem`
  and `AssertHasItem` are `WaitItemsHasItem` and `AssertItemsHasItem`. `ToggleControlBase.Check`
  and `Uncheck` do nothing when the box is already in that state, as on MAUI.
- The Html and Blazor UAT fixtures start the Blazor sample with `WebAppHost`; `HtmlSampleHost`
  and `BlazorSampleHost` are gone.
- `TodoDataPhrases` moved to `Bruinet.Samples.Todo.TestSupport.Uat`, over `ITodoArrangement`, so
  both heads' UAT suites share it. The Todo web fixture runs devices (`TodoWebDevice`) on one
  fake backend, and can restart and change test set.
- **Breaking:** the calls layer, its exceptions and scope readiness moved from `Bruinet.Maui`
  and `Bruinet.Html` into Core, one copy for both stacks (AD-010,
  `.my/core/review-what-can-move-now.md`). `Bruinet.Maui.Calls`, `Bruinet.Html.Calls`,
  `Bruinet.Maui.Exceptions`, `Bruinet.Html.Exceptions` and `Bruinet.Maui.Scopes` are gone:
  - `Bruinet.Core.Calls`: `Confirmation<T>`, `ConfirmationResult` (was
    `Bruinet.Maui.Controls.Base`), `CallBudget`, `NearMissSettings` (was
    `Bruinet.Maui.Context` / `Bruinet.Html.Context`); the internal call types.
  - `Bruinet.Core.Exceptions`: `StaleElementException`, `ElementNotReadyException`,
    `NotReadyReason`, `ScopeNotReadyException`, `AppUnavailableException`,
    `RouteUnavailableException`. One type across stacks: a catch works for either.
  - `Bruinet.Core.Scopes`: `ScopeReadiness` (with `RootReacquired`, on both stacks now),
    `ScopeReadinessState`. Html's `BusySignal` stays in `Bruinet.Html.Scopes`.
- The merged calls layer is the union of both copies. MAUI gains the confirmation's own
  budget context and the cut-off rule for a driver timeout after the deadline; Html gains
  `ItemChanged`, the scroll-to-find throttle and the poll's `stop`. `ControlCall` takes the
  logger, test name and near-miss settings instead of a stack's context (MAUI still logs
  "Test": `IMauiTestContext` has no name yet). `AttemptContext.RemainingOr` is replaced by `CallBudget.RemainingOr`,
  which never answers less than 1 ms inside a call.
- `StaleElementException`'s message is platform-neutral: "is no longer there: it was removed
  or replaced".
- `CommunityToolkit.Maui` 9.0.0 -> 15.0.1 and `Microsoft.Maui.Controls` -> 10.0.90 (required by the
  toolkit). Added `CommunityToolkit.Maui.MediaElement` 10.0.0.
- `Bruinet.Maui.CommunityToolkit` no longer references CommunityToolkit.Maui, is centrally
  versioned, and is in `src/Bruinet.sln`. `CreateMaui.Bat` generates its controls too.
- `MediaElement<TScope>` moved from `Bruinet.Maui.Controls.Media` to
  `Bruinet.Maui.CommunityToolkit.Controls.Media`.
- `MediaElement` forwards to its parts instead of polling around them: `SetPlaying` returns
  the media element rather than the page; `WaitProgressPasses` is replaced by
  `WaitProgressGreaterThan`, and `WaitDurationKnown` by `WaitOpened`.
- Every control now does one unit of work per call. `ToggleControlBase`, `Stepper`,
  `CarouselView`, `Expander`, `RatingView`, `DrawingView` and `MediaPlayPauseButton` confirm their
  effect with `Confirm` instead of a nested poll; a timeout carries the last read's error as
  `InnerException`.
- **MAUI calls (AD-004, AD-009; `.my/stale-readiness/`):** every public call on a control,
  page, container, collection or row is one log entry/exit pair and one budget. Only the call's
  poll waits, and each attempt first asks the scope chain (row, collection, page), then finds
  the element again. Actions resolve by polling, then act once. A Core method that throws
  `ElementNotReadyException` has not acted, so it is asked again within the budget.
- MAUI no longer implements Core's `IElement`, `IDriver`, `IElementScope`, `IPageObject`,
  `ITestContext<T>`, `IContainerControl` or `IContainerObject`, and controls no longer derive
  from `ControlObjectBase`. Core itself is unchanged.
- Finds on MAUI elements, drivers and scopes make one attempt; the timeout overloads are gone.
- `IsLoaded()`, `GetTitle()` and `TakeScreenshot(string?)` lose their unused timeouts.
- `ItemContainerBase` is `ItemObjectBase`, and `IMauiItemContainer` is `IMauiItemObject`.
- MAUI throws `ScopeNotReadyException` where it threw `PageLoadException`, and
  `ElementNotReadyException` where a disabled or hidden control used to raise `TimeoutException`.
  The FlaUI driver reports a disabled toolbar or menu item, or one no bridge target answered
  for, as `ElementNotReadyException`.
- `Item(int)`, `this[int]`, `SelectItem` and `ItemWhere` wait within their budget; `TryItem`,
  `TrySelectItem` and `FindItem` answer about now. `ScrollToItem`, `ScrollToEnd`, `ScrollToTop`
  and `WaitForItems` run on one budget (`DefaultWait` unless given), and a long list needs a
  budget to match.
- A row is found again by its item key, and reports `ItemChanged` instead of answering for
  another item. This covers a row's own members (`Click`, `GetText`) as well as the controls inside
  it: a cached row element that now holds another item is not used.
- `SelectItem` finds the row by polling, then activates it once. Only an activation that reports
  "nothing activated" is asked again; any exception ends the call.
- `IMauiElement.ScrollIntoView(int timeoutMs)` has no default. Every scroll inside a call gets what
  is left of the call's budget (`CallRemainingMs` on controls and scopes), and Appium's Android
  scroll makes at least one step. `ScrollHelper.ScrollIntoView` takes a budget, and
  `ScrollView.ScrollTo` is one call.
- A call whose attempts all raised an unexpected exception fails with `WaitTimeoutException`,
  naming the exception type and how many attempts raised it, with the exception as
  `InnerException`. A single attempt still throws the exception itself.
- A route the platform does not have ends the call at once as `RouteUnavailableException` instead
  of being asked again until the budget runs out: the `IMauiElement` and `IMauiDriver` capability
  defaults, a bridge-declared element asked for something that lives in the UI Automation tree, and
  a locator strategy that cannot be matched against an element already in hand. A bridge verb the
  app did not answer is not a missing route and is still retried.
- `Assert*` members that read through an element find the element again and re-check visibility on
  every attempt, as `Get*` members already did. They used to resolve once and hold it, so an
  element replaced while still alive was compared until the budget ran out.
- `AppRoot.WaitReady(timeoutMs)` polls its own readiness on the budget given; it used to ask the
  context, which answered "not disposed" without waiting and ignored the timeout.
- The FlaUI driver raises `AppUnavailableException` when a bridge verb, a state read or a gesture
  found no target because the launched app has exited, instead of "not ready" or "unavailable".
- `IMauiElementScope.DescribeMiss(locator)` builds a scope's "not found" message without
  looking again; a control's `NotFound()` uses it.
- `IsExists()` / `IsVisible()` on containers and pages answer now and take no timeout;
  `GetItemCount()`, `IsEmpty()` and `TrySelectItem(index)` lose their unused timeouts.
- `AppRoot` no longer scrolls to look for a control; Shell's flyout still does, within itself.
- `IsExists()` / `IsVisible()` on a control read the element only; they no longer consult the
  page.
- `IItemRootProvider`: `KeyOf(root, position)` and `TryGetItemRoot(ItemKey)`.
- `AvatarView`: the `Text` part is now `Initials` (`AvatarInitials`), and `GetText` /
  `WaitText` / `AssertText` are `GetInitials` / `WaitInitials` / `AssertInitials`. `Image` is an
  `AvatarImage`.
- `StateContainer`: `IsShowing` / `WaitShowing` / `AssertShowing` take a view id only (the
  `Locator` overloads are gone) and are generated; `WaitShowing`'s `expected` is `bool?`.
- `ContentDialog` is a generated template; `GetTitle` and friends take an optional `timeoutMs`,
  and `GetButtonTexts` returns `IReadOnlyList<string>?`.
- `Popup.CloseWith` is hand-written as two calls in sequence; same signature.
- `WebView.GetUrl` / `GetPageTitle` lose an `[AbsenceTolerant]` that had no effect, so their
  behaviour stays as it was now that the attribute works on getters.
- Migrated from Oravey.UITestFramework namespace

### Removed
- `UatStepAttribute`, the writable `[UatPhrase(keyword, phrase)]` and the `[UatPhrase(phrase)]`
  form without a keyword. Use `[UatGiven]`, `[UatWhen]` or `[UatThen]`.
- **Breaking:** Html's async members: `Bruinet.Html.Interfaces.Async` (`IAsyncHtmlElement`,
  `IHtmlAsyncControlObject` and the capability interfaces), `HtmlAsyncExtensions`,
  `ControlBase.RunWithElementAsync`, `HtmlPageObjectBase.WaitLoadedAsync` and the other `*Async`
  page, container and context wrappers. They wrapped the synchronous calls and added nothing;
  every member is one synchronous call, as on MAUI.
- Html's `Control<TScope>` and `List<TScope>`: use `ClickableControlBase` and `ListControl`.
- Html: `RunDoWithElement` no longer retries an action that threw (X2). `ObjectBase.Poll`,
  `ControlBase.RunPoll`, the element finds with a timeout, `PlaywrightTestContext.ForFrame`
  and `BlazorTestContext.WaitForBlazorReady` are gone. `IsLoaded()`, `GetTitle()` and
  `TakeScreenshot(string?)` on Html pages lost their unused timeouts. Html no longer throws
  `PageLoadException`.
- MAUI: `DriverRootScope` (use `AppRoot`), `Popup.Page => null`, `CanResolveElements`,
  `CreateScopeNotReadyException`, `IsParentReady` / `WaitParentReady`, `WaitContentReady` /
  `WaitContentReadyCore` (use `ProbeContentReadiness`), `IsReady(int?)` (now `IsReady()`), the
  page's `EnsureLoaded`, both `Until` implementations (use `Confirm`), `RunPoll`,
  `WaitVisibleCore`, `EnsureVisible(element, timeout)` and `doEnsureVisible`, and the virtual
  `ViewBase.FindElement()` (override `TryFindElement()` and `NotFound()`), and
  `ObjectBase.Poll`.
- `FlaUIMauiDriver.SupportsStateReads`: `ReadState` answers in one bridge walk.
- `PageReadinessSnapshot` from the MAUI page contract.

### Changed
- Android sessions skip installing an APK that is already on the device: the APK's hash and the
  package's update time are recorded there after an install, and an unchanged build only has its
  data cleared. About 3 s less per session start. `BRUINET_ALWAYS_REINSTALL=1` installs every time.
- Readiness is asked once per attempt of a call (`AttemptMemo`): a read's scope chain asked the page
  root for its bounds up to four times. On Android, a row's `resource-id` and class are read once
  per element, and a read no longer asks `displayed` first where being found means being shown. A
  list test on the emulator made 2,386 Appium requests and now makes 1,205; Bruinet's MAUI UI suite
  on Windows ran in under 2 minutes instead of 3.

### Fixed
- `run_scenario` and the preview's Run start a suite's fixture as xunit does: `InitializeAsync` first and
  `DisposeAsync` at the end (`IAsyncLifetime`). The Todo web suite's fixture starts there, so every run of it stopped with
  "The fixture is not started"; its file and scenario start (`IUatScenarioLifecycle`) is now on the fixture as in the MAUI
  suite, so the runner does it too.
- The MAUI sample's slider started at 1, not its view model's 50: it set `Value` before `Minimum` and
  `Maximum`, and MAUI clamps a value to the bounds it has then (1 by default). Found by the new control
  contracts on MauiHeadless; the sample sets the bounds first now.
- A step definition's exception kept only Bruinet's frames: `BddInvoker` threw the inner exception again,
  which starts its stack anew, so the step's own code and the app's were lost to the IDE, logs and reports.
  It is rethrown with its stack now.
- Bruinet.Blazor's `NavMenuControl` named the first item as the active one whatever was active: with its
  default item selector (a list, `a, .nav-link, [role='menuitem']`) the active state joined only the last
  part, so every link matched. Found by new tests of Blazor's own controls on bUnit and in Chromium.
- Two transitive packages pinned past their security advisories, so the solution restores again:
  `System.Security.Cryptography.Pkcs` 9.0.21 in the Stride sample (from Stride's toolkit,
  GHSA-2fmv-rv2c-59x7) and `MessagePack` 2.5.303 in `Bruinet.Cli` (from StreamJsonRpc,
  GHSA-qhrr-8q5h-9q3h). With warnings as errors the advisories failed the whole solution's restore,
  so a solution build compiled nothing.
- UAT: `BeforeFile` runs once per file per fixture. The file last entered was one value per closed
  test base, so collections sharing a base (one fixture each) entered their file again on every
  switch between them.
- Html: `Locator.ByAccessibilityId` finds by the accessible name (`aria-label`, `aria-labelledby`
  or a `<label>`, exactly), where it looked for a generic role with that name and found nothing;
  attribute and text selectors escape a backslash as well as a quote.
- Html: `CollectionObjectBase.Item(key)` and `TryItem(key)` read every row's `data-item-key` in
  turn, a driver call per row, so on a long list one attempt outlasted the call's budget and the
  item was never found. They look the row up once by its key's selector now; such a row's
  `Position` is `-1`.
- Html: a read of an element that a re-render removed waited out the rest of the call, so one
  read ended the poll (R2). Playwright reads now wait for their element at most
  `PlaywrightHtmlElement.ReadAttachMs` and report a node that is gone as stale.
- Html: a keyed row was found again by position, so after a re-render it read another item's row
  and answered for the wrong item. It is found by its `data-item-key` now.
- Html: `CollectionObjectBase`'s "no row matches" failure names what the wait last saw.
- `Bruinet.Maui.UITests`' `NavigationDemoPage` looked up by name through an XPath locator the
  FlaUI driver does not support, so its "unreachable by name" probes never looked.
- `AndroidRecorder` stopped every `screenrecord` on the device (`pkill screenrecord`) and failed when
  one belonged to another user; it stops only its own now. It also pulled the video into the
  results folder directly, which adb cannot do past Windows' 260 characters; it pulls into the
  temp folder and moves the file.
- CI: a deterministic build mapped PDB paths to `/_/`, and the language server failed on them
  ("Invalid URI"); `BddSourceLocator` gives no location for them, and tests and samples keep real
  paths. A cancel that reached the BDD runner before its step started was lost.
- N/A (initial release)

### Fixed
- A page root that left the tree is found again. On Windows a page Shell navigated away from keeps
  answering as present, on screen and full size for about a second; a cached root went on reading
  the page underneath. A root is now valid only while it is attached (`IDetachable`, implemented by
  the Windows element).
- A grouped list is noticed without `HasLogicalIndexes`: two realized rows with one
  `PositionInSet` switch the collection to positions and ids. A search had skipped the second
  group's rows as seen and stopped early.
- A collection search moves past a row only partly on screen, whose content is off screen
  (Windows, iOS) or left out of the tree (Android), instead of failing on it. A predicate no row
  can answer is still reported.
- A collection search that reaches the end scrolls to the top once and searches down again, so an
  item above the viewport is found.
- `ScrollToTop` steps up while the list moves, then aligns the first row. It scrolled the first
  realized row into view and stopped, mid-list in a virtualized list.
- `title`, the Shell title's id on Windows and MauiHeadless, finds the navigation bar's title on
  Android and iOS too.

## [0.1.0] - TBD

- Initial public release
