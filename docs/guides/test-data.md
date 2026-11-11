# Test Data

Most tests need starting data before the first tap: what the server has, what the device has,
what time it is. Bruinet keeps that data in **test sets** - named JSON files, read strictly into
the app's real types and checked before any test runs. The decision behind this is
[AD-012](../../.docs/decisions/ad-012-test-data-is-typed-validated-and-chosen-by-the-scenario.md).
The [Todo sample](../../samples/Todo/README.md) uses everything on this page.

## A Test Set

One file per test set, in the test-support project's `TestData/` folder. The Todo shape:

```jsonc
// TestData/one-todo.json - the server and the device both have "Buy milk", synced.
{
  "$schema": "./testdata.schema.json",
  "now": "2026-03-10T09:00:00Z",
  "server": [
    { "id": "55555555-0000-0000-0000-000000000001", "title": "Buy milk", "notes": null, "dueDate": "2026-03-12",
      "status": "Open", "createdAt": "2026-03-01T08:00:00Z", "updatedAt": "2026-03-01T08:00:00Z" }
  ],
  "local": [
    { "id": "55555555-0000-0000-0000-000000000001", "syncState": "Synced" }
  ]
}
```

- `server` is the API's DTO (`TodoDto`), `local` the app's model. No private copies.
- Reading is strict (`TestDataJson.Strict`): an unknown field, a missing required field or a
  `null` where the type has none is an error, not a silently empty value.
- `$schema` gives editors completion and red underlines; the file is generated from the types.
- Keep test sets small and meaningful. Tests **add** records on top; they do not change or
  remove a test set's records (the device's database is not reset between scenarios).

## Describing The Test Data

An app describes its test sets once, in its test-support project (shortened from the Todo sample's
`TodoTestDataDefinition`, which also names the user tables and their rules):

```csharp
public sealed class TodoTestDataDefinition : TestDataDefinition<TodoTestSetFile>
{
    public override JsonSerializerOptions JsonOptions => TodoTestSet.JsonOptions;

    public override IReadOnlyList<Type> TableTypes => [typeof(TodoDto), typeof(LocalTodoData)];

    protected override string RelativeFolder => "TestData";

    protected override void DefineRules(TestDataRules<TodoTestSetFile> rules) => rules
        .UniqueIds("server", set => set.Server, todo => todo.Id)
        .Require("server", set => set.Server, todo => todo.CreatedAt <= todo.UpdatedAt, "updatedAt is before createdAt.")
        .ForEach("server", set => set.Server, todo => TodoValidation.ValidateTitle(todo.Title));
}
```

- `RelativeFolder` is found in the project folder of the definition's assembly, so the tool
  and the gate check the files you edit, not a copy in `bin`.
- A rule names the JSON path of its collection (`server`), so a failure points at the item's
  line. Use the app's own validation where it exists: seed data the app would refuse is a bug.
- Keep the definition in an assembly without a platform or MAUI target (`net10.0`), so the tool
  can load it.

The **gate** is one xUnit test in the integration tier:

```csharp
[Fact]
public void EveryTestSet_FollowsTheRules()
{
    var report = TestDataValidator.Validate(new TodoTestDataDefinition());
    Assert.True(report.IsValid, report.Format());
}
```

## `bruinet data`

The same engine from the command line, for people and for models editing test data. It loads
the app's test-support assembly at run time, found through a `TestData` row in the UAT
project's `uat.config.md`:

```markdown
| Kind | Assembly |
| --- | --- |
| TestData | ../Bruinet.Samples.Todo.TestSupport/bin/Debug/net10.0/Bruinet.Samples.Todo.TestSupport.dll |
```

Commands, from the UAT project folder (`samples/Todo/tests/Bruinet.Samples.Todo.Uat`), with
[`bruinet`](../run/cli.md) installed (inside Bruinet's repository, `pwsh ../../../../tools/Scripts/bruinet.ps1`
instead of `bruinet`):

```powershell
bruinet data validate
bruinet data validate ../Bruinet.Samples.Todo.TestSupport/TestData/one-todo.json --json
bruinet data describe TodoDto
bruinet data list
bruinet data schema
```

| Command | Does |
| --- | --- |
| `validate [file] [--json] [--fix]` | Every test set (or one file), plus every `TestData` name in `Features/**/*.feature.md` and `DefaultTestData`. With a `Fixture` and a `Pages` assembly in the config, also every scenario file (or one `.feature.md`): parse, bind and argument errors, as the xUnit gate reports them. `--fix` rewrites values with one right answer (`2026-05-12` to `12-May-2026`) |
| `phrases [--keyword When] [--json]` | The phrases steps can use, with argument types (`{due: date}`), where each is declared, and broken phrases as warnings; then the callable phrases of every `*.steps.md` under `Features/`, with their inferred types and the folder each is seen in |
| `phrases <file>` | Only the phrases that scenario file sees, grouped by where they come from: its own callables, its folders, the namespaces it uses (`(Using)`), every scenario, each page |
| `references <namespace>` | The config and the scenario files that use a namespace: `Using`, `## Using`, or being in its folder |
| `snippet <file>:<line>` | A method for a step that binds to nothing, its phrase in the name: `When_I_archive_P1_on_P2(string p1, DateOnly p2)` |
| `describe <Type>` | The fields a file writes for a type: required, nullable, enum values |
| `list` | The test sets, their record counts, and which files use each |
| `schema [--check]` | Writes (or checks) `testdata.schema.json` |

Output is `file:line: CODE path: message`, e.g.
`TestData/one-todo.json:4: DATA001 server[0]: unknown field 'titel' on TodoDto. Did you mean 'title'?`.
Exit code 0 means fine, 1 problems found, 2 the tool could not run. `--assembly <dll>` replaces
the config lookup. The tool warns when the assembly is older than its sources (or a referenced
project's): rebuild, or it checks the old types.

| Code | Meaning |
| --- | --- |
| `DATA001` | Unknown field |
| `DATA002` | Missing required field |
| `DATA003` | Wrong value (enum name, `null` where not allowed) |
| `DATA004` | Not valid JSON |
| `DATA010` / `DATA011` | A rule failed / a duplicate id |
| `DATA020` | A `TestData` reference names no test set |
| `UAT0xx`, `UATB0xx`, `UATA0xx`, `UATC0xx` | A scenario file's parse, bind, argument and callable-phrase errors ([Arguments](uat-step-definitions.md#arguments), [Callable Phrases](uat-step-definitions.md#callable-phrases)). `validate` also checks each `*.steps.md` under `Features/`, and reports a broken callable as a warning at its heading |

## In UAT

A scenario file names its test set; the fixture loads it:

```markdown
| Field | Value |
| --- | --- |
| Journey | TOD.02 |
| TestData | one-todo |
```

`DefaultTestData` in `uat.config.md`'s `Runtime` table covers files that name none. The test
class loads it in `BeforeFile`, before the file's first scenario:

```csharp
protected override void BeforeFile(BddDocument document)
    => Fixture.UseTestSet(document.Metadata.TryGetValue(BddMetadataFields.TestData, out var name) && name.Length > 0
        ? name
        : TodoUatFixture.DefaultTestData);
```

In the Todo sample, `UseTestSet` seeds a fresh database, resets the fake backend to the set's
server half and restarts the app with the set's now - only when the set changes.

Steps then add to the set and check what the app did, through tables bound to the real types
(see [Step Tables](uat-template-guide.md#step-tables)):

```gherkin
Given the server also has todos
  | Title        | Status     | DueDate |
  | ------------ | ---------- | ------- |
  | Call plumber | InProgress | +2d     |
When I tap Sync
Then Todos should have todo "Call plumber"
And the database should have todos
  | Title        | SyncState |
  | ------------ | --------- |
  | Call plumber | Synced    |
```

Read the database only after a UI sentinel (the row is on screen), so the app's write has
landed. Checks compare only the listed columns. "Has these rows" is safe in any order; "has
only these rows" is safe only when the database is reset per scenario.

## Mock Bodies Are Contract Types

A WireMock response body is the API's DTO, or a recorded sample (`ReturnsSample`) - never an
anonymous object:

```csharp
mock.Get(TodoApi.TodosPath).ReturnsJson(new[] { new TodoDto(...) });   // fine
mock.Get(TodoApi.TodosPath).ReturnsJson(new { titel = "x" });           // BRUINET001
```

`BRUINET001` is an analyzer shipped in the `Bruinet.Mocking` package. It also reports anonymous
items in arrays and lists, and in `MockResponse.Json` inside a `RespondsWith` callback. Tests of
the mock server itself may suppress it with a reason (`<NoWarn>$(NoWarn);BRUINET001</NoWarn>`).
Prefer a typed fake per API (the Todo sample's `TodoServerStub`) over one stub per test.
