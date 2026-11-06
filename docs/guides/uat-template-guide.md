# UAT Template Guide

Bruinet UAT tests execute markdown scenarios using a `uat.config.md` file and
one or more `.feature.md` scenario files. A small C# project ties them to a running
app.

A complete working example is
[`samples/Todo/tests/Bruinet.Samples.Todo.Uat`](../../samples/Todo/tests/Bruinet.Samples.Todo.Uat):
four scenarios against the hermetic Todo app on Windows, walked through step by
step in [One Todo UAT, end to end](../../samples/Todo/uat-walkthrough.md).

## Project Layout

The UAT suite lives in the platform's UI test project, beside the page objects its scenarios
name: a scenario is only as good as those, and a page object changes for both.

```text
tests/Bruinet.<Platform>.UITests/
  uat.config.md
  Features/
    scenario-name.feature.md
  ExpectedFailures/
    known-failure.feature.md
  TestSettings/
    testsettings.json
  Uat/
    <App>UatFixture.cs
    <App>UatScenarioTests.cs
  PageObjects/ ...          the page objects the UI tests and the scenarios share
  Tests/ ...                the UI tests
```

The scenario test class carries `[Trait("Category", "UAT")]`, so `--filter "Category=UAT"` runs
the suite alone and `--filter "Category!=UAT"` the UI tests alone. Put it in the UI tests' own
collection when they share one app. A suite may still be a project of its own (the Todo sample's
is): the layout inside is the same, with its C# under `Runtime/`.

A suite below the UI (the `service` head) names its config `bdd.config.md` instead; it is read the
same way, and the name says which kind of suite it is.

Put `uat.config.md`, `Features/**/*.feature.md` and `TestSettings/**/*.json` in the
csproj as `CopyToOutputDirectory="PreserveNewest"`; the runtime reads them from
the output folder.

## The Test Project

Three small pieces. The **fixture** launches the app and exposes the page graph:

```csharp
[TestModuleScan(typeof(TodoListPage), NamespacePrefix = "Bruinet.Samples.Todo.UITests.Pages")]
public sealed class TodoUatFixture : TodoAppFixture
{
    public TodoUatFixture()
    {
        Composition = TestComposition.ForFixture(this, services =>
            services.AddSingleton<IMauiTestContext>(Context));
    }

    public TestComposition Composition { get; }
}
```

`Composition` is what the runtime discovers pages from and constructs them
through. Without it, discovery falls back to page-typed properties on the
fixture itself.

A **collection** shares one app across every scenario class:

```csharp
[CollectionDefinition(CollectionName)]
public sealed class TodoUatCollection : ICollectionFixture<TodoUatFixture>
{
    public const string CollectionName = "Todo UAT";
}
```

The **test class** derives from `UatScenarioTestBase<TFixture>` and turns each
scenario - each outline row too - into a theory case of its own:

```csharp
[Collection(TodoUatCollection.CollectionName)]
[TestCaseOrderer(BddScenarioOrderer.TypeName, BddScenarioOrderer.AssemblyName)]
public sealed class TodoUatScenarioTests : UatScenarioTestBase<TodoUatFixture>
{
    public TodoUatScenarioTests(TodoUatFixture fixture) : base(fixture) { }

    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    // Load the file's TestData before its first scenario (the fixture may restart the app).
    protected override void BeforeFile(BddDocument document) => Fixture.UseTestSet(...);

    // Reset the app between scenarios so they stay order-independent.
    protected override void BeforeScenario(BddBoundScenario scenario) => Fixture.StartTest();

    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "FlaUI");
}
```

`RuntimeValidation` asserts the config at load time: a config whose `Target` or
`Fixture` disagrees, or that registers no assemblies, throws rather than running
scenarios against the wrong head. Override `BeforeScenario` to give each scenario
a clean start - scenarios share one app process. Override `AfterScenario` to put
back what a scenario changed that the next test sharing the fixture must not see (sign
out, close a dialog); it runs whether the scenario passed or failed, after its recording,
so it is never in a video. Override `BeforeFile` to load
what a file's metadata asks for; it runs before the file is bound, once per file.

`GetScenarioTheoryData` orders cases by the files' `TestData`, then file, then
position (`BddScenarioSource.GetOrderKey`). xUnit 2 runs theory cases in case-id
order unless told otherwise, so the Todo sample adds a small `ITestCaseOrderer`
(`BddScenarioOrderer`) that sorts by that key: the app then restarts once per test
set, not once per scenario. `GetScenarioFiles` / `RunUatFileAsync` still run a
whole file as one case.

`[UatScenarioTheory]` is `[Theory]` with a stable address per case: the `BddScenario` trait is
the file relative to the config and a slug of the scenario's name, so one scenario runs by filter
(from the UAT project folder):

```powershell
dotnet test --filter "BddScenario=Features/TOD.11-manage-users.feature.md#TOD.11.1-Users-lists-available-users-first-each-with-its-role-and-state"
```

The slug keeps `[A-Za-z0-9._/-]` and turns every other run of characters into one `-`; an outline
row `[2]` ends in `-2`. A suite that keeps `[Theory]` runs as before, without the trait. When the
feature file is the project's own, the case's source is the scenario's line in it.

## Config Shape

```markdown
# UAT Config

## Runtime

| Field | Value |
| --- | --- |
| Target | MAUI |
| Fixture | FlaUI |
| AppPath | ../../src/…App/bin/Debug/net10.0-windows10.0.19041.0/win10-x64/…App.exe |
| WorkingDirectory | ../../../.. |
| Head | maui |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | ../…UITests/bin/Debug/net10.0-windows/…UITests.dll |
| Controls | ../../../../src/Bruinet.Maui/bin/Debug/net10.0/Bruinet.Maui.dll |
| Commands | ../../../../src/Bruinet.Uat/bin/Debug/net10.0/Bruinet.Uat.dll |

## Discovery

| Field | Value |
| --- | --- |
| RequireExplicitUatAttributes | false |
| AllowNameInference | true |

## Reporting

| Field | Value |
| --- | --- |
| OutputDirectory | $(BruinetTestResults)/uat |
| ScreenshotOnFailure | true |
| IncludeRuntimeTrace | true |

## Settings

| Field | Value |
| --- | --- |
| Root | TestSettings |
| DefaultFile | testsettings.json |
| LocalFile | testsettings.local.json |
| ScenarioConvention | scenarios/{ScenarioId}.json |

## Skip Rules

| Tag | EnvironmentVariable |
| --- | --- |
| hardware | BRUINET_UAT_HARDWARE |
| live-api | BRUINET_UAT_LIVE_API |
```

`$(BruinetTestResults)` resolves through the shared Bruinet artifact provider.
If `OutputDirectory` is omitted, UAT should use the suite `uat/` folder.

What each section carries:

| Section | Field | Meaning |
| --- | --- | --- |
| Runtime | `Target` | The platform: `MAUI`, `HTML`, `BLAZOR`, `WPF`, `WINFORMS`, `STRIDE`. Derived from the fixture's base class when it has a Bruinet one (`BlazorTestFixtureBase` is `BLAZOR`); otherwise this row. Checked against `RuntimeValidation`. |
| Runtime | `Fixture` | The fixture type, or for MAUI the driver: `FlaUI` for the Windows head, `Appium` for Android. |
| Runtime | `Head` | Optional: the head this suite drives (`maui`, `web`). A scenario tagged `@maui` or `@web` runs only on its head. See [Heads](#heads-one-scenario-file-several-apps). |
| Runtime | `DefaultTestData` | The test set for a scenario file whose metadata names none. |
| Runtime | `AppPath` | The app to launch, relative to `WorkingDirectory`. |
| Runtime | `WorkingDirectory` | The base the relative paths resolve against. |
| Assemblies | `Pages` | The assembly holding the page objects - the source of control names. |
| Assemblies | `Controls` | The ControlObject assembly for the platform. |
| Assemblies | `Commands` | `Bruinet.Uat`, which carries the engine's own verbs. |
| Using | `Namespace` | Optional: namespaces every scenario uses, such as the subjects outside the screen (`Backend`). See [Where Phrases Live](uat-step-definitions.md#where-phrases-live). |
| Apps | `Name` (+ the fixture's columns) | Optional: the apps a scenario can switch between with `{app} uses the app`. See [Several Apps In One Scenario](#several-apps-in-one-scenario). |
| Discovery | `AllowNameInference` | `true` lets `AddButton` be **Add** without `[UatName]`. |
| Discovery | `RequireExplicitUatAttributes` | `true` limits discovery to members carrying UAT attributes. |

`Assemblies` is required by default: `UatRuntimeValidationOptions` fails a config
that registers none. Pass `RequireAssemblies: false` if a project drives the app
purely through its fixture's composition.

## Scenario File Shape

The formal grammar, with every parser diagnostic, is in [UAT Scenario Grammar](../specs/uat-grammar.md).

````markdown
# Feature: MAUI Main Page Greeting

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.Maui.App |
| Area | Main Page |
| Target | MAUI |
| Tags | smoke, maui, greeting |
| Mode | Automated |
| Requires | Deterministic |
| Priority | Smoke |
| Evidence | none |

@smoke @maui @greeting @automated @deterministic
## Scenario: Greeting appears when a name is entered

```gherkin
Given I am on the Main page
When I clear Name
And I enter "Alice" into Name
And I tap Greet
Then Greeting should contain "Hello, Alice!"
And Greeting should be visible
And Name should be enabled
```
````

The scenario parser supports this document shape:

- The file must contain exactly one `# Feature: <title>` heading.
- `## Metadata` is optional for parsing, but the format tests expect `App`,
  `Area`, `Target`, `Tags`, `Mode`, `Requires`, `Priority`, and `Evidence`.
  The table columns must be `Field` and `Value`.
- **Steps are only read inside a ```` ```gherkin ```` block.** A step, its table
  and its doc string go in one; a `#` line in a block is a comment. A section may
  have several blocks, read in order as one list.
- `## Background` is optional and contains shared `Given`, `When`, `Then`,
  `And`, or `But` steps.
- `## Data: <name>` is optional and must contain a markdown table.
- Tags are written as `@tag` lines and must be immediately followed by
  `## Scenario:` or `## Scenario Outline:`.
- `## Scenario: <name>` contains one or more gherkin blocks.
- `## Scenario Outline: <name>` expands one scenario per row in a required
  `### Examples` table. Step text and step table cells can reference example
  columns with `<columnName>` placeholders.
- **Everything else is prose** (AD-023), in any Markdown form, anywhere: see
  [Prose](#prose).

Scenario discovery scans `Features/**/*.feature.md` recursively, so larger apps
can keep executable specs in module folders such as
`Features/Contacts/00-smoke.feature.md` and
`Features/Projects/01-list-basic.feature.md`.

## Metadata Fields

`BddMetadataFields` names the fields the format tests expect: `App`, `Area`,
`Target`, `Tags`, `Mode`, `Requires`, `Owner`, `Priority`, `Evidence`. The table
is a plain `Field`/`Value` table, so a project can add rows of its own. The Todo
sample adds `Journey`, which ties each scenario to a subtask id in its
[journey checklist](../../samples/Todo/tests/Bruinet.Samples.Todo.Uat/todo-user-journeys.md):

```markdown
| Journey | TOD.02 |
| TestData | one-todo |
```

Metadata is documentation and traceability, with two exceptions. `TestData`
(`BddMetadataFields.TestData`) names the test set the file runs on, and a fixture
that overrides `BeforeFile` loads it; `DefaultTestData` in the config's `Runtime`
table covers files that name none. See [Test Data](test-data.md). `Using`
(`BddMetadataFields.Using`) names the namespaces whose phrases and pages the file
sees besides its folder's: `Accounts, Backend`. See
[Where Phrases Live](uat-step-definitions.md#where-phrases-live). Otherwise the
`@tag` line above `## Scenario:` is what skip rules read, not the `Tags` row.

## One File Per Journey

A file may hold many scenarios, so one journey can be one file: the journey in
the `# Feature:` title and `Journey` metadata, each subtask a scenario whose heading
starts with its id and whose tag line carries it. The shared start goes in
`## Background`. The steps of each scenario go in a ```` ```gherkin ```` block, and
the text around them can say what the journey is for.

````markdown
@TOD.02.1 @maui @create
## Scenario: TOD.02.1 Add opens an empty edit form with status Open

```gherkin
Then Title should equal ""
And Status should show status "Open"
And Title Error should not exist
```
````

The Todo sample's
[`TOD.02-create-a-todo.feature.md`](../../samples/Todo/tests/Bruinet.Samples.Todo.Uat/Features/TOD.02-create-a-todo.feature.md)
is a complete one.

For how step text binds to executable commands, see
[UAT Phrases And Flows](uat-step-definitions.md).

## Step Tables

Scenario steps may include an immediate markdown table:

````markdown
## Scenario: Valid user can sign in

```gherkin
Given I am on the Login page
When I sign in with credentials
| Field | Value |
| --- | --- |
| User name | ada@example.com |
| Password | correct-password |
Then I should see "Welcome Ada"
```
````

Scenario outlines use `### Examples`:

````markdown
## Scenario Outline: Login result is shown

```gherkin
Given I am on the Login page
When I enter credentials
| Field | Value |
| --- | --- |
| User name | <user> |
| Password | <password> |
Then I should see "<result>"
```

### Examples

| user | password | result |
| --- | --- | --- |
| ada@example.com | correct-password | Dashboard |
| locked@example.com | correct-password | Account locked |
````

An outline can have several Examples sections, each with a name and tags of its own. The
tags join the outline's for that section's rows, so a head tag picks rows for a head:

````markdown
@maui @web
### Examples: refused at once

| user | password | result |
| --- | --- | --- |
| locked@example.com | correct-password | Account locked |

@web
### Examples: refused by the server

| user | password | result |
| --- | --- | --- |
| expired@example.com | correct-password | Password expired |
````

Rows are numbered across the sections, `Login result is shown [1]`, `[2]` and on, so splitting a
section, or moving a row between sections in order, keeps every row's name, filter and recorded
timings. Two sections of one outline with the same name are `UAT032`.

Table cells are trimmed. To pass a value whose spaces matter, quote it in the
cell and leave the placeholder unquoted in the step: `When I set Title to <title>`
with a `"   "` row.

A custom phrase receives the step's table by taking a `BddTable` parameter, and
binds it to the app's real types with `BddTableBinding`:

```csharp
[BddThen("the database should have todos")]
public void DatabaseHas(BddTable table)
    => table.AssertContains(TodoDbReader.Todos(fixture.DatabasePath), fixture.TestSet.Now);
```

`Bind<T>` builds records through their widest constructor; `AssertContains`,
`AssertExactly` and `AssertDoesNotContain` compare only the listed columns and
print the expected rows, the actual rows and the closest difference. Columns match
members without case or spaces (`Due Date` is `DueDate`); an unknown column is an
error naming the known ones. An empty cell or `null` is `null` where the type
allows it, and dates may be relative to the test set's now: `today`, `+2d`, `-1d`.
Cells are written in the scenario format - `12-May-2026`, `1,250.75`, `In Progress` - and a
phrase that takes a record list instead of a `BddTable` gets its rows bound and checked when the
step binds ([Arguments](uat-step-definitions.md#arguments)).

## Phrase Files

Steps a tester repeats can be named once, as a callable phrase
([Callable Phrases](uat-step-definitions.md#callable-phrases)). One that several files use goes
in a `*.steps.md` file: one `# Steps:` heading, then only `## Step:` and
`## Step for each row:` sections. Put it in `Features/` for every scenario, or in a module's
folder (`Features/Invoicing/`) for the scenarios in that folder and below
([Where Phrases Live](uat-step-definitions.md#where-phrases-live)). The gate reads scenarios from
the build output, so the project copies phrase files as it copies scenarios:

```xml
<None Update="Features\**\*.feature.md" CopyToOutputDirectory="PreserveNewest" />
<None Update="Features\**\*.steps.md" CopyToOutputDirectory="PreserveNewest" />
```

A suite that links another suite's scenarios links its phrase files too. `bruinet data validate`
warns when a project has phrase files it does not copy.

## Authoring Rules

- Use stable user-facing intent in steps.
- Keep locator names in commands or page objects, not prose.
- Put repeated command behavior in Bruinet commands or controls.
- Keep expected failures in `ExpectedFailures/` with a clear reason.
- Keep generated output under `TestResults/`, not under source folders.

## Settings

UAT settings are resolved through `JsonTestSettingsProvider`.

Default lookup:

```text
TestSettings/
  testsettings.json
  testsettings.local.json
  scenarios/<ScenarioId>.json
```

`testsettings.local.json` is optional. Scenario settings are optional and are
selected from a scenario tag that starts with `uat-`, for example `@uat-login`.

Settings files must be JSON objects. The provider reads a top-level `settings`
object and supports an `include` array for required included files.

## Reporting

Each scenario writes `<feature file>__<scenario>__result.json` to the configured reporting
directory. It always names the feature file (relative to the config) and the scenario's line, the
status and duration, and each step's line, status and message; a step of a callable also has the
call's line (`callLine`), and each step that ran its time (`durationMs`). A failed step has a
`failure`: its `kind` (`assertion`, `locator-not-found`, `timeout`, `app-unavailable`, `bind`,
`app-exception`, `error`), the `exception`'s type, `expected` and `actual` for an assertion, the
control's `locator`, the step's `binding`, and the first five `frames` of the suite's and the app's
code it came through (`TodoList.AssertRowStatus (TodoList.cs:83)`). A step during which the app said something has
`appEvents`: its log's warnings and errors (`Bruinet.Logging`'s `AddBruinetStepLog`, added by the MauiHeadless head;
a bUnit or service suite adds it where it composes the app's services, after the app's own registrations), and the
browser's console errors and warnings and page errors; with `BRUINET_STEP_LOG=all`, information too, with `off`
nothing. An app in a process of its own (the web head's host, a device) is not read. That is what the IDE preview
shows at the lines that ran, and what the MCP server's `run_scenario` and `get_trace` give an agent. A scenario that passed or failed also
writes its step trace, `step-traces/<feature>__<scenario>__trace.json` beside the report (named in its `tracePath`),
with the commit it ran on: each step's control calls with their duration and attempts, its app events, and the page's
controls after a failed step (60 at most, 3 seconds of reading). A pass also keeps `__trace.green.json`, which a later
failure in the same run folder does not overwrite. `BRUINET_TRACE=all` reads the page after every step; `off` traces
nothing. The MCP server's `get_trace` reads a trace, and its `why` compares a failure with the last green run; a failed
scenario's line in `summary.md` says where it first parted from that run. If
`IncludeRuntimeTrace` is true, the report adds each step's binding and call chain, diagnostics,
discovery data, and command catalog data. Scenario result files are also registered in the
shared artifact manifest as `uat-scenario` artifacts, with the suite, file and line in their
metadata.

See [Reporting And Artifacts](reporting-artifacts.md).

## Skip Rules

Skip rules connect scenario tags to environment variables. A scenario tagged
with `@hardware` is skipped unless its configured environment variable is
enabled with `1`, `true`, `yes`, or `on`.

## Heads: one scenario file, several apps

One set of scenario files can run on two heads of the same app: the Todo sample's MAUI suite
and its web suite (`samples/Todo/tests/Bruinet.Samples.Todo.Web.Uat`) run the same
`TOD.*.feature.md` files, the web project linking them from the MAUI one.

- `Head` in the config's `Runtime` names the head a suite drives: `maui`, `web`, or `mixed` for
  a suite whose apps are of several heads (see below).
- A scenario tagged with head tags (`@maui`, `@web`) runs only on a suite whose `Head` is one of
  them; the engine skips it elsewhere, with the reason in the result. A scenario with no head tag
  runs on every head. A suite without `Head` runs everything.
- Each suite has its own page objects and step-definition classes with the same names and phrases: the
  scenario text is the contract between them. Steps that do not touch the screen can be one class
  for both heads (`BackendPhrases`, `DevicePhrases` and `StoredDataPhrases`, over an
  `ITodoArrangement` both fixtures implement).

## Several Apps In One Scenario

A scenario can drive more than one running app: two devices on one backend, two users, or apps
of different heads. The config lists them in an `Apps` table, or names a shared file with one
(`AppsFile | apps.md`, relative to the config). The engine reads `Name`, `User` and `Head`; the
other columns are the fixture's.

```markdown
## Apps

| Name | User | Head | Platform | ClientId |
| --- | --- | --- | --- | --- |
| Daan's PC | Daan | maui | Windows | maui-daan |
| Finn's web | Finn | web | | web-finn |
```

`User` defaults to `Name`, and `Head` to the suite's. A step that starts with an app or a user
runs on that app ([AD-015](../../.docs/decisions/ad-015-a-steps-subject-picks-its-app.md)):
`Daan taps Save` binds to the declared `{user} taps {control}` on Daan's app, and the app stays
current, so a later `I` step runs there too. A phrase a user can say has a `{user}` form beside
its `I` form (see UAT Phrases And Flows); the built-in ones all do. `{app} uses the app` still works. A failure names the app (`[Daan's PC] ...`),
and the result JSON records the app of every step.

```gherkin
Given Daan is on the Todo List page
And Finn is on the Todo List page
When Daan taps Add
Then Daan should be on the Todo Edit page
When Daan sets Title to "Pack for the trip"
And Daan taps Save
Then Daan should be on the Todo List page
When Daan taps Sync
Then the server should have received the todo "Pack for the trip" from Daan
When Finn taps Sync
Then Todos should have todo "Pack for the trip"
```

Name the user on every step of a scenario with several users; a step with `I` is correct but
makes the reader look back.

The fixture implements `IBddSessionHost` (`UseSession(name)` returns the services the app's page
objects resolve through), or more simply `IUatAppsHost`: it hands over a `TestAppSet` and says
what each app's composition needs (`AddAppServices`), and sessions come from the set. The Todo
web suite's `TodoWebUatFixture` is the one-head example.

**Apps of several heads.** A suite with `Head | mixed` runs only scenarios tagged `@mixed`. Each
app's `Head` column picks the page objects it binds against: `IUatAppsHost.ModulesFor(app)` names
that head's modules, and the engine builds one catalog per head. The Todo sample's
`Bruinet.Samples.Todo.Mixed.Uat` runs Windows and web apps in one scenario, reading the same
`apps.md` as its mixed UI tests.
