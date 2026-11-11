# Coming From Reqnroll

For a team that writes Reqnroll or SpecFlow today. Most of what you know carries over: features,
scenarios, outlines, backgrounds, tags, tables, step definitions and hooks are the same ideas, with
a `Bdd` prefix on the attributes. What differs is where the steps come from and when a mistake
shows. The words side by side are in the [BDD Glossary](bdd-glossary.md).

## The Same Scenario, Twice

In Reqnroll, a `.feature` file:

```gherkin
Feature: TOD.02 Create a todo

  Background:
    Given I am on the Todo List page
    When I tap Add
    Then I should be on the Todo Edit page

  @TOD.02.2 @smoke
  Scenario: TOD.02.2 Saving a valid title returns to the list, which shows the new todo
    When I set Title to "Write the report"
    And I tap Save
    Then I should be on the Todo List page
    And the database should have todos
      | Title            | Status | SyncState |
      | Write the report | Open   | LocalOnly |
```

In Bruinet, a `.feature.md` file: Markdown headings for the structure, the steps in `gherkin`
blocks, prose and a metadata table allowed around them ([AD-023](../../.docs/decisions/ad-023-a-feature-file-is-markdown.md)):

````markdown
# Feature: TOD.02 Create a todo

## Background

```gherkin
Given I am on the Todo List page
When I tap Add
Then I should be on the Todo Edit page
```

@TOD.02.2 @smoke
## Scenario: TOD.02.2 Saving a valid title returns to the list, which shows the new todo

```gherkin
When I set Title to "Write the report"
And I tap Save
Then I should be on the Todo List page
And the database should have todos
  | Title            | Status | SyncState |
  | ---------------- | ------ | --------- |
  | Write the report | Open   | LocalOnly |
```
````

The steps are the same text. The difference is the code behind them. In Reqnroll every line needs
a step definition. In Bruinet the first six are already bound: `I tap {control}` and
`I set {control} to {value}` are control verbs, and `I am on the {page} page` and
`I should be on the {page} page` are page flow, all found from the suite's page objects and controls. Only the last step, which
looks in the database, is written by hand:

```csharp
// Reqnroll
[Binding]
public class StoredDataSteps(ITodoArrangement fixture)
{
    [Then("the database should have todos")]
    public void DatabaseHas(Table table) => table.CompareToSet(TodoDbReader.Todos(fixture.DatabasePath));
}

// Bruinet (samples/Todo/tests/Bruinet.Samples.Todo.TestSupport/Uat/StoredDataPhrases.cs)
[BddStepDefinitions(Namespace = "StoredData")]
public sealed class StoredDataPhrases(ITodoArrangement fixture) : BddStepDefinitionsBase
{
    [BddThen("the database should have todos")]
    public void DatabaseHas(BddTable table) => table.AssertContains(TodoDbReader.Todos(fixture.DatabasePath), fixture.TestSet.Now);
}
```

## What Maps One To One

| Reqnroll | Bruinet | Notes |
| --- | --- | --- |
| `[Binding]` | `[BddStepDefinitions]`, or derive from `BddStepDefinitionsBase` | found by the fixture's composition scan; constructor injection from it |
| `[Given]`, `[When]`, `[Then]` | `[BddGiven]`, `[BddWhen]`, `[BddThen]` | a method named `Given…`, `When…` or `Then…` is a step without the attribute |
| `{int}`, `{string}` in a step | `{count}`, `{title}`: any name | the parameter of that name gives the type ([Arguments](uat-step-definitions.md#arguments)) |
| `[StepArgumentTransformation]` | `[BddArgumentType]` with `static Parse(string)`, or an `IBddArgumentConverter<T>` | |
| `Table`, `CreateInstance`, `CreateSet` | `BddTable`, or a record or list parameter the table binds to | |
| `CompareToSet`, `CompareToInstance` | `table.AssertExactly`, `table.AssertContains`, `table.AssertDoesNotContain`, `BddSets.AssertSet` | one diff: missing, extra, and the closest item's different cells ([Tables](uat-step-definitions.md#tables)) |
| `[BeforeFeature]` | `[BddBeforeFeature]` | static, once per file per run |
| `[BeforeScenario("tag")]`, `[AfterScenario("tag")]` | `[BddBeforeScenario("tag")]`, `[BddAfterScenario("tag")]` | ([Hooks](uat-step-definitions.md#hooks)) |
| `[AfterStep]` | `[BddAfterStep]` | may take the step's `BddStepResult` |
| `Scenario Outline` with several `Examples`, each named and tagged | `## Scenario Outline:` with one or more `### Examples: <name>`, a tag line above each | row *n* runs as `<name> [n]`, numbered across the blocks |
| a doc string | a doc string | |
| tags as test categories | tags, and a `BddScenario` trait per scenario | `dotnet test --filter "BddScenario=Features/TOD.02-create-a-todo.feature.md#TOD.02.2-…"` |
| `@ignore` | skip rules: a tag and the environment variable that turns it on | a skipped scenario says why ([Skip Rules](uat-template-guide.md#skip-rules)) |

## What Works Differently

- **Mistakes show before the app starts.** A whole file binds before its first step runs: an
  unknown step, a control the page does not have, or a value written wrong fails in seconds, with
  a code and a line. `bruinet data validate` checks every file in CI without the app.
- **One spelling per value.** Numbers and dates read the same on every machine (`1,250.75`,
  `12-May-2026`, `today`, `+2d`); `1,5` or `12/05/2026` is refused with the right spelling
  ([AD-016](../../.docs/decisions/ad-016-phrase-arguments-are-typed-by-the-method.md)).
- **No regex, no optional words.** A step's text is literal with `{placeholders}`; `a/an` and
  `todo(s)` are not written, so one sentence has one meaning and completion can list it (AD-016).
- **Steps are scoped by page and folder, not by tag.** A page's steps bind only on that page, and
  folders are namespaces a file opts into with `Using`
  ([AD-019](../../.docs/decisions/ad-019-phrases-have-scopes-and-namespaces.md)).
- **Testers write reusable steps in Markdown.** A `## Step:` is a named sequence of steps with
  parameters, no code and no control flow
  ([AD-017](../../.docs/decisions/ad-017-callable-phrases-are-sequences-not-programs.md)).
- **One file, every head.** The same scenarios run on the MAUI app, in the browser, on bUnit and
  below the UI; `@maui` and `@web` pick a head
  ([AD-014](../../.docs/decisions/ad-014-one-scenario-file-serves-every-head.md)).
- **xUnit only.** A suite is an xUnit project; scenarios are theory cases of a
  `UatScenarioTestBase` test class ([UAT Template Guide](uat-template-guide.md#the-test-project)).

## What Is Not There, And Why

| Reqnroll has | In Bruinet |
| --- | --- |
| Retrying a flaky scenario | not on purpose: a flaky scenario is a bug in the app or the test, and stays a failure ([AD-009](../../.docs/decisions/ad-009-app-bugs-stay-failures.md)) |
| `[Scope]` by tag or feature | page scope, folder namespaces and `Using` instead (AD-019) |
| `[BeforeStep]`, `[BeforeTestRun]`, `[AfterTestRun]`, `[AfterFeature]` | not yet; the run's setup and teardown is the xUnit fixture's |
| Keywords in another language (`# language: nl`) | English keywords; the step text can be any language |
| Cucumber Messages and the HTML report | not yet: each scenario writes `__result.json` and its evidence into the run's folder ([Reporting Artifacts](reporting-artifacts.md)); the IDEs show a run at its lines |
| Importing `.feature` files | not yet: the structure converts by hand as above, and most step definitions turn into control verbs or page flow |
| MSTest, NUnit, TUnit | xUnit only |

## Where To Go Next

- [UAT Template Guide](uat-template-guide.md): a suite's project, config and file shape.
- [UAT Step Definitions And Flows](uat-step-definitions.md): steps, arguments, tables, hooks.
- [The Todo sample](../../samples/Todo/README.md): one app tested at five tiers, with these features.
