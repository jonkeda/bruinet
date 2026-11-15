# Todo sample: one app, five test tiers

A small MAUI todo app, tested the way a real app should be: each behaviour at the lowest tier
that can prove it, and every test traced back to a user journey. It shows:

- **Bruinet.Maui** driving the app on Windows (FlaUI) and Android (Appium), through page
  objects and ControlObjects only: tests never touch a driver, an element or a sleep.
- **WireMock** (`Bruinet.Mocking`) standing in for the backend, with the requests it received
  asserted by waiting for them.
- **A seeded SQLite database** for the app's local state, built with the app's own migrations.
- **A test pyramid** from unit tests to manual charters, kept honest by a report that compares
  the tests with [journeys.md](journeys.md).
- **UAT scenarios in Markdown** - `Given/When/Then` files a non-programmer can read, running
  against the same app through the same page objects.

## The app

Five pages in a Shell, with a local SQLite store that syncs with `Todo.Api`:

| Page | What it does |
| --- | --- |
| List | All todos, a status filter, Sync, Add, Users. Empty, Loading and Error states (CommunityToolkit `StateContainer`). |
| Detail | One todo in three `SectionCard`s (details, notes, sync state), Next status, Edit, Delete (with confirmation). |
| Edit | Title (required, at most 100 characters), notes, an optional due date, status. Cancel asks before discarding changes. |
| Users | The users, available ones first, each with role and state (Expired, On leave, Inactive). Add; tap to edit. Read-only offline. |
| User edit | One field of each kind a test drives: name and email (`Entry`), status (`RadioButton`s), an optional end date (`CheckBox` + `DatePicker`), role (`Picker`), hours (`Stepper`), hourly rate (`1,250.75`), start of day (`TimePicker`), reminders (`Switch`), workload (`Slider`), notes (`Editor`). Save shows every error on its field. |

Users are managed by the server: a change goes straight to `PUT /api/users/{id}`, which checks
the same rules as the app, and the app caches the answer. A sync fetches the users before the
todos. Users are not linked to todos yet; the `team` test set has one user in each state. The
UI tests are `UsersJourneyTests` (MAUI, Windows) and the UAT scenarios `TOD.11-manage-users.feature.md`,
whose `Using | Users` reaches the users pages and phrases in the `Users` namespace; the web head
has no users pages yet.

Two custom controls, each with a matching ControlObject on the test side:

| App control | Kind | Test side |
| --- | --- | --- |
| `TodoStatusView`: glyph, status name, Next button | Component: its parts' ids repeat in every row, so the ControlObject scopes to the component's root | `TodoStatus<TScope>` |
| `SectionCard`: a header and any content | Container: controls inside it are found within the card | `SectionCard<TParent>` |

## Projects

| Project | What it is |
| --- | --- |
| `src/…Core` | Models, rules, view models, the sync planner. No MAUI, no SQLite, no HTTP. |
| `src/…Contracts` | The DTOs and routes the app and the API share. |
| `src/…Infrastructure` | SQLite repository, API client, sync service, launch settings. |
| `src/…Api` | `Todo.Api`, the backend: a minimal API with its own SQLite file. |
| `src/…App` | The MAUI app (Windows and Android). |
| `src/…Web` | The web head: the same app in Blazor Server, on the same `Core`, `Contracts` and `Infrastructure`. See "The web head" below. |
| `tests/…UnitTests` | Tier 1. |
| `tests/…IntegrationTests` | Tier 2: headless, against WireMock and SQLite; the contract tests against the real API. |
| `tests/…TestSupport` | Test sets (`TestData/*.json`), the WireMock fake of the API, temp databases. |
| `tests/…UITests` | Tiers 3 and 4 on Windows: page objects, ControlObjects, fixtures, tests. |
| `tests/…UITests.Mobile` | The same UI test sources, built for Android (Appium). |
| `tests/…Uat` | Markdown UAT scenarios over tier 3's app and page objects. |
| `tests/…JourneyCoverage` | The journey coverage report. |
| `tests/…TestSupport.Apps` | Several apps of any head on one backend: `apps.md` and `TodoAppsFixture`, shared by the two mixed projects. See "Several apps in one test" below. |
| `tests/…Mixed.UITests` | UI tests that drive Windows, web and Android apps at once. |
| `tests/…Mixed.Uat` | UAT scenarios that do the same: `Daan taps Save` runs on Daan's app. |
| `manual/charters.md` | Tier 5. |

## The five tiers

| # | Tier | `Pyramid` trait | What is real | What is fake | Tests (cases) |
| --- | --- | --- | --- | --- | --- |
| 1 | Unit | `Unit` | View models, rules, the sync planner | Repository, API client, navigator, dialogs (NSubstitute); the clock | 70 (114) |
| 2 | Integration | `Integration` | DI composition, SQLite, `HttpClient`, sync | The backend: WireMock | 24 |
| 2b | Contract | `Contract` | The real `Todo.Api`, in process | nothing | 4 |
| 3 | UI hermetic | `UiHermetic` | The app, SQLite, HTTP | The backend: WireMock; the database is seeded; the clock is pinned | 34 (36) |
| 4 | UI live | `UiLive` | The app and the real `Todo.Api` | nothing | 3 |
| 5 | Manual | - | Everything, on real devices | nothing | 5 charters |

Two gates run inside tier 3: **Smoke** (`Gate=Smoke`, under 30 s: the app starts, the list
arrives, a detail opens) and **Reviews** (`Gate=Review`: every screen once, its controls found by
id, a screenshot each).

The counts are the pyramid's shape. Most of it is rule tables (`[Theory]` over validation, the
Overdue rule, sort order, sync merge) that run in milliseconds. The UI tiers check one example
per behaviour: that the screen shows what the rules decide.

## Walkthrough: TOD.02 "Create a todo" through every tier

[journeys.md](journeys.md) splits each journey into subtasks and gives each subtask the lowest
tier that can prove it:

| Id | Subtask | Tier |
| --- | --- | --- |
| TOD.02.1 | Add opens an empty edit form with status Open | H |
| TOD.02.2 | Save with a valid title returns to the list, which shows the new todo | H |
| TOD.02.3 | Empty or whitespace title shows "Title is required" and does not save | U + H |
| TOD.02.4 | Title over 100 characters is refused | U |
| TOD.02.5 | A due date is optional; switching it off clears it | U + H |
| TOD.02.6 | The new todo is stored locally with a client-generated id and "local only" | I |

Every test carries its subtask as a trait, so you can find all of TOD.02.3's tests with
`--filter Journey=TOD.02.3`, in any test project.

### 1. Unit: every variant of the rule

The rule is a pure function, so all its edge cases go here: four ways of being empty, in
microseconds. `UnitTests/Rules/TodoRulesTests.cs`:

```csharp
[Theory]
[Trait("Journey", "TOD.02.3")]
[InlineData(null)]
[InlineData("")]
[InlineData(" ")]
[InlineData("\t \r\n")]
public void ValidateTitle_EmptyOrWhitespace_IsRequired(string? title)
    => Assert.Equal(TodoValidation.TitleRequired, TodoValidation.ValidateTitle(title));
```

The view model's reaction is a unit test too, with the repository and the navigator faked.
`UnitTests/ViewModels/TodoEditViewModelTests.cs`:

```csharp
[Theory]
[Trait("Journey", "TOD.02.3")]
[InlineData("")]
[InlineData("   ")]
public async Task Save_WithoutATitle_ShowsTheError_AndStaysOnThePage(string title)
{
    var edit = Create();
    await edit.LoadAsync(null);
    edit.Title = title;

    await edit.SaveCommand.ExecuteAsync(null);

    Assert.Equal(TodoValidation.TitleRequired, edit.TitleError);
    Assert.True(edit.HasTitleError);
    Assert.Empty(_repository.Items);
    await _navigator.DidNotReceive().GoBackAsync();
}
```

TOD.02.4 (at most 100 characters) stops here: `ValidateTitle_AllowsAtMost100Characters` covers
1, 99, 100, 101 and 500 characters. A UI test would add nothing but time.

### 2. Integration: what reaches the disk

TOD.02.6 needs a real database: the id is made on the device, and a new todo is `LocalOnly`
until it has been synced. `TodoHost` builds the app's real service container on a temporary
SQLite file, with WireMock as the backend. `IntegrationTests/DatabaseTests.cs`:

```csharp
[Fact]
[Trait("Journey", "TOD.02.6")]
public async Task ACreatedTodo_IsStoredLocally_WithItsOwnId_AndLocalOnly()
{
    using var host = await TodoHost.StartAsync();

    var created = await host.Todos.CreateAsync("Buy milk", null, null, TodoStatus.Open);

    var stored = Assert.Single(await host.LocalAsync());
    Assert.Equal(created.Id, stored.Id);
    Assert.Equal(SyncState.LocalOnly, stored.SyncState);
    Assert.Equal(host.Scenario.Now, stored.CreatedAt);
}
```

The **contract tests** (tier 2b, TOD.07.9) keep the fake honest. The same requests go to
the WireMock fake and to the real `Todo.Api` in process, and both must answer alike. If the API
changes, these fail before the hermetic UI tests can pass against a stale fake.

### 3. UI hermetic: one example on the real screen

The fixture seeds a database from a test set (`TestSupport/TestData/three-todos.json`), starts
WireMock with the test set's server half, and launches the app with launch settings pointing
at both. The test only uses page objects. `UITests/Tests/Journeys/CreateEditJourneyTests.cs`:

```csharp
[Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
[Trait("Journey", "TOD.02.3")]
public Task Save_WithoutATitle_ShowsTitleRequired_AndStays()
{
    var edit = _fixture.List.Add().SaveExpectingError();

    edit.TitleError.AssertText("Title is required");
    edit.AssertLoaded(true);

    edit.CancelNew();
    return Task.CompletedTask;
}
```

One example, not four: the unit tier already proved the variants. This test proves what only
the screen can: the error label is bound, and Save stays on the page. The rest of TOD.02 is here
too: `Add_OpensAnEmptyForm_Open_WithoutADueDate` (02.1),
`Save_AValidTodo_ShowsItInTheList_WaitingToSync` (02.2), and
`DueDateSwitch_EnablesThePicker_AndTheDateIsSaved` (02.5).

The page objects read like the user's steps, and every wait is inside them.
`TodoEditPage.SaveExpectingError()` clicks Save and waits for the error label;
`TodoListPage.CreateTodo(title)` is `Add().EnterTitle(title).SaveNew()`.

When a test must see what the app sent, it waits for the request on WireMock rather than
counting straight after a click. From `SyncJourneyTests.cs`:

```csharp
list.Sync();

var put = _fixture.Backend.WaitForRequest("PUT", AnyTodo,
    request => request.BodyAs<TodoDto>(TodoApi.JsonOptions)?.Title == title);
Assert.Equal(TodoApi.DevelopmentApiKey, put.Headers[TodoApi.ApiKeyHeader]);
list.Todos.AssertPending(title, expected: false);
```

### 4. UI live: the real wire, once

A created todo matters only when it reaches the server (TOD.07.4). The live fixture starts the
real `Todo.Api` and launches the app against it. The test checks the result through the
API's own HTTP endpoint, never through the app. `UITests/Live/LiveSyncTests.cs`:

```csharp
[LiveApiFact]
[Trait("Journey", "TOD.07.4")]
public async Task ATodoCreatedInTheApp_IsOnTheServerAfterSync()
{
    var title = LiveTodoAppFixture.Unique("Created in the app");
    var list = _fixture.List.CreateTodo(title);
    Assert.Null(await _fixture.Api.FindAsync(title));

    list.Sync().Todos.AssertPending(title, expected: false, timeoutMs: TestConstants.PageTimeoutMs);

    var stored = await _fixture.Api.FindAsync(title);
    Assert.NotNull(stored);
    Assert.Equal(Contracts.TodoStatusDto.Open, stored.Status);
}
```

It uses the same page objects as tier 3; only the fixture differs. There are three live tests,
all happy paths, and they skip unless `BRUINET_UAT_LIVE_API=1`.

### 5. Manual: what a machine cannot judge

[manual/charters.md](manual/charters.md) holds time-boxed charters, not scripts. For TOD.02:
CH-2 kills the app mid-save on a real device, and CH-3 checks the edit page with the keyboard up,
at 200 % font size, and with a screen reader. The integration tier proves the deterministic
half of CH-2: a save is all or nothing (`ASave_IsAllOrNothing_ToAReaderOnAnotherConnection`).
When a charter finds something reproducible, it moves down into an automated tier, and the
charter records where it went.

## Feature: the journeys, in Markdown

`tests/Bruinet.Samples.Todo.Uat` runs [`Features/*.feature.md`](tests/Bruinet.Samples.Todo.Uat/Features)
against the same hermetic app tier 3 uses - WireMock backend, seeded database - but the test
is the Markdown, not C#:

```gherkin
Given I am on the Todo List page
When I tap Add
Then I should be on the Todo Edit page
When I set Title to "Write the report"
And I tap Save
Then I should be on the Todo List page
And Todos should have todo "Write the report"
```

`Add`, `Title`, `Save` and `Todos` are members of `TodoListPage` and `TodoEditPage`; the verbs
are `[BddWhen]` / `[BddThen]` attributes on the control methods they call, so the vocabulary and the code
that runs it are one declaration
([AD-011](../../.docs/decisions/ad-011-uat-vocabulary-is-attribute-declared.md)). Two verbs are
Todo's own: `I advance {control} to {value}` and `{control} should show status {value}`,
declared on `TodoStatus`.

The acceptance backbone is
[todo-user-journeys.md](tests/Bruinet.Samples.Todo.Uat/todo-user-journeys.md), a plain
checklist of what a user must be able to do. It is not `journeys.md`: that one decides *where*
a behaviour is tested, this one states *what* the user can do. There is one file per
journey (`Features/TOD.02-create-a-todo.feature.md`, `TOD.04-...`, `TOD.05-...`), with one scenario
per subtask, named and tagged with its id (`@TOD.02.3`).

Each file names its starting data in `TestData` metadata (`one-todo`, from `TestSupport/TestData/`;
`DefaultTestData` in `bdd.config.md` is the fallback), and steps such as `the database should have todos` check what the app
stored. Every scenario is its own test case. Today that is TOD.02 complete (02.1, 02.2, 02.3,
02.5) plus TOD.04.3 and TOD.05.1. The checklist marks the rest honestly: `[!]` where
another tier already proves it (rule tables, sync, persistence), `[-]` where UAT cannot
(real hardware, a real network, a human). Windows only; Android UAT and a live-backend UAT
are out of scope.

[One Todo UAT, end to end](uat-walkthrough.md) walks a single scenario from Markdown to the
running app.

## Test data: scenarios, not a database bridge

A test set (`TestSupport/TestData/*.json`) has a local half and a server half, plus a pinned
"now":

- The **local half** goes into a fresh SQLite file, created by the app's own `TodoDatabase`
  migrations, so the seeded schema can never drift from the app's.
- The **server half** loads into `TodoServerStub`, a stateful WireMock fake of `Todo.Api`. A test
  changes it per test (a 500, a delay, a failure that recovers), and the fixture resets it
  between tests.
- **Users** are optional: `users` is what the server has, and `localUsers` what the device has
  cached. Leave `localUsers` out for a device that has synced; `[]` for one that never fetched
  users.

The app receives the file path, the backend URL, the clock and the sync switches as launch
settings (`MauiDriverOptions.LaunchSettings`). On Windows they are environment variables of the
launched process; on Android they are intent extras. A bridge that would let a test write to the
app's database while it runs was considered and not needed. Seeding before launch, plus the
backend fake, covers every journey. The cost shows on Android: a file seeded on the host cannot
reach the device's private storage yet.

## Running it

From `samples/Todo`:

```bash
# Tiers 1, 2 and the coverage report: no app, seconds.
dotnet test tests/Bruinet.Samples.Todo.UnitTests
dotnet test tests/Bruinet.Samples.Todo.IntegrationTests
dotnet test tests/Bruinet.Samples.Todo.JourneyCoverage

# The test sets (TestSupport/TestData), from the command line - see docs/guides/test-data.md.
cd tests/Bruinet.Samples.Todo.Uat; pwsh ../../../../tools/Scripts/bruinet.ps1 data validate; cd ../..

# Tier 3 on Windows: build the app first; the fixture finds it under src/…App/bin.
dotnet build src/Bruinet.Samples.Todo.App -f net10.0-windows10.0.19041.0
dotnet test tests/Bruinet.Samples.Todo.UITests                          # everything, ~30 s
dotnet test tests/Bruinet.Samples.Todo.UITests --filter Gate=Smoke      # the Smoke gate
dotnet test tests/Bruinet.Samples.Todo.UITests --filter Journey=TOD.02.3

# Tier 4: also start the real API, in process on a free port.
BRUINET_UAT_LIVE_API=1 dotnet test tests/Bruinet.Samples.Todo.UITests --filter Pyramid=UiLive

# The Markdown UAT scenarios: same app, same build-the-app-first rule.
dotnet test tests/Bruinet.Samples.Todo.Uat
```

To run tier 4 against an API that is already deployed, set `TODO_LIVE_API_BASEURL` and
`TODO_LIVE_API_KEY`.

Run one UI test process at a time: two runs fight over the desktop and fail for reasons that
have nothing to do with the app.

To try the app by hand, start `dotnet run --project src/Bruinet.Samples.Todo.Api` (it listens
on port 5080) and then run the app.

## The web head

`src/Bruinet.Samples.Todo.Web` is the same app in the browser: Blazor Server with interactive
server rendering. It reuses the MAUI app's view models, rules, sync and SQLite store unchanged.
Only navigation, dialogs and the pages are its own. Its test ids are the MAUI AutomationIds
(`data-testid="TodoEdit_Title"`), so both heads offer page objects the same contract. It exists to
prove Bruinet.Html on a real app, and later to run the MAUI and web apps against one backend in
one test ([apps in a test](../../.docs/contracts/uat-apps-host.md)).

- **One host process is one device.** It reads the same launch settings as the MAUI app
  (`TODO_DB_PATH`, `TODO_API_BASEURL`, `TODO_NOW`, ...) from its environment, and has its own
  SQLite file. Two hosts are two devices. Two browser tabs on one host share one app.
- **Who sent it.** Set `TODO_CLIENT_ID` (on either head) and every request carries it in
  `X-Todo-Client`. WireMock records the header. `Todo.Api` keeps an in-memory log of accepted
  requests with their client (`RunningTodoApi.Requests`). See TOD.07.10.
- **Busy signal.** `<body data-busy>` is `true` while the page's view model is busy.

To try it, start the API as above, then:

```bash
dotnet run --project src/Bruinet.Samples.Todo.Web    # http://localhost:5090
```

Its UI tests are `tests/Bruinet.Samples.Todo.Web.UITests`: the TOD journeys through Html page
objects, hermetic (WireMock, a seeded database, a pinned clock) and live (the real `Todo.Api`,
with `BRUINET_UAT_LIVE_API=1`). The fixtures start the web head themselves on a free port, so
nothing needs to be running first:

```bash
dotnet test tests/Bruinet.Samples.Todo.Web.UITests
```

The UAT scenarios run on the web head too: `tests/Bruinet.Samples.Todo.Web.Uat` runs the same
`Features/*.feature.md` files as the MAUI suite (a scenario tagged `@web` runs there), through web
page objects with the MAUI member names. It also has a scenario with two devices on one backend
(`TOD.07-two-devices.feature.md`: `Alice taps Sync`, then `Bob taps Sync` and sees her todo):

```bash
dotnet test tests/Bruinet.Samples.Todo.Web.Uat
```

### Several apps in one test

`tests/Bruinet.Samples.Todo.TestSupport.Apps/apps.md` lists the apps a test can use - Daan and Gus
on Windows, Eva on Android, Finn and Hana in a browser - all on one fake backend, each with its
own database, `X-Todo-Client` and clock (`ClockOffsetMinutes`). `TodoAppsFixture` starts an app
the first time a test asks for it: two Windows apps go side by side, left and right.

`tests/Bruinet.Samples.Todo.Mixed.UITests` uses them as variables:

```csharp
var daan = apps.Maui("Daan");
var finn = apps.Web("Finn");
daan.List.CreateTodo(title).Sync();
apps.WaitForTodoFrom(daan, title);
finn.List.Sync().Todos.AssertRow(title);
```

`tests/Bruinet.Samples.Todo.Mixed.Uat` reads the same table, and each step runs on the app of the
user it names (`When Daan taps Save`, `Then Finn should be on the Todo List page`); see
`Features/TOD.07-mixed.feature.md`. A failure captures every started app, one folder each. The
Android test and scenario run only with the emulator and `BRUINET_ANDROID=1`.

```bash
dotnet test tests/Bruinet.Samples.Todo.Mixed.UITests
dotnet test tests/Bruinet.Samples.Todo.Mixed.Uat
```

Build the MAUI app for Windows first, as for tier 3.

### On Android

Start an emulator and an Appium server (`127.0.0.1:4723`, UiAutomator2 driver). Then, from
`samples/Todo`:

```bash
dotnet build src/Bruinet.Samples.Todo.App -f net10.0-android
APPIUM_PLATFORM=android dotnet test tests/Bruinet.Samples.Todo.UITests.Mobile
```

The app reaches WireMock on the host through `10.0.2.2`. Eight hermetic tests are skipped on
Android, each for a stated reason (`[WindowsOnlyFact(reason)]`). The two biggest: the device
cannot open a database file seeded on the host, and Bruinet cannot drive a MAUI Picker's native
dialog on Android yet. Charter CH-5 covers those tests by hand until they can run there. The
rest (26 tests) pass on both platforms.

## The journey coverage report

`tests/Bruinet.Samples.Todo.JourneyCoverage` reads [journeys.md](journeys.md), reads every
test's traits by reflection and parses every scenario file (nothing runs, nothing launches), and
fails when:

- a subtask has no test in a tier `journeys.md` assigns it to;
- a subtask marked **M** is not named in `manual/charters.md`;
- a `Journey` trait names a subtask that does not exist (a typo);
- a test has no `Pyramid` trait, or a scenario no `@TOD.xx.y` tag;
- the pyramid inverts: unit cases must outnumber hermetic UI cases, and those must outnumber
  live ones.

It writes the full matrix, with every subtask, tier and covering test, to
`TestResults/<run-id>/suites/TodoJourneyCoverage/journey-coverage.md`.

A scenario counts like a test: its `@TOD.xx.y` tags are its subtasks, and each head it runs on
gives a tier. `@service` is Integration (the headless suite in `IntegrationTests/Service`); a UI
head, or no head tag, is UiHermetic. A scenario tagged `@service @maui` counts in both.

To add a behaviour: add its subtask to `journeys.md` with the lowest tier that can prove it,
write the test with its `Journey` and `Pyramid` traits (or the scenario with its tags), and let
the report confirm they match.

## Rules the tests follow

- Tests use page objects and ControlObjects only. No driver, no `IMauiElement`, no `Find*`, no
  `Thread.Sleep`. Waiting belongs to the ControlObjects and page objects.
- Synchronize on what the user would see (a state, a marker, a row), or on a request WireMock
  received. Never on time.
- One example per behaviour in the UI tiers. If a UI test repeats a variant a unit test already
  covers, push it down.
- xUnit `Assert` only.
