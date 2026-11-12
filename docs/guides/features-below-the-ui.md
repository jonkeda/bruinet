# Features Below The UI

A feature file can run without a screen, against the app's services, database and fake backend,
on the `service` head. The same `*.feature.md` files serve the UI suites and the service suite;
a scenario's tags say where it runs. The decision is [AD-020](../../.docs/decisions/ad-020-a-tier-is-a-head.md).

## When A Scenario Goes To `service`

Ask what the assertion looks at:

- **A rendered control** (the pending marker shows, the Error state with Retry appears): a UI
  head. Leave it untagged or tag it `@maui` / `@web`.
- **Data, requests or the app's state** (the sync pushed then pulled, the stored todos survive a
  restart): tag it `@service`. It runs in milliseconds and needs no desktop.
- **Both, one example each**: tag it `@service @maui`. It runs on both suites, counts for both
  tiers in the coverage report, and binding it on both keeps the two vocabularies the same.

Write Gherkin when someone other than the developer reads it. Plumbing (a contract's shape,
tombstones, retry timing) stays a C# test.

A `@service` scenario may not use a page step (`I am on the ... page`), a control verb
(`I tap Sync`) or a page phrase: binding fails with **UATB013** before anything runs. Say the same
thing with a step the service head declares (`I sync`).

## The Suite

In the Todo sample, `Bruinet.Samples.Todo.IntegrationTests` is the service suite:

- `bdd.config.md`, with `Head | service`, `Fixture | TodoServiceFixture` and a `## Using` table;
  the tools read it as they read a `uat.config.md`.
- The feature files, linked from `Bruinet.Samples.Todo.Uat/Features/`.
- `TodoServiceFixture`, which starts a new headless host per scenario (a database from the file's
  `TestData`, a WireMock backend, online), so no scenario sees another's data.
- `TodoServiceScenarioTests : UatScenarioTestBase<TodoServiceFixture>`, with the traits
  `Pyramid = Integration` and `Category = BDD`.

Only scenarios tagged `@service` are listed; an untagged scenario never runs there.

## Step Definitions

A service step definition is a step-definition class like any other
([UAT Step Definitions And Flows](uat-step-definitions.md#step-definition-classes)), resolved from
the scenario's scope. Use the words the UI heads use for the same action:

<!-- sample: tests/Bruinet.Docs.Samples/Guides/FeaturesBelowTheUi.cs#steps -->
```csharp
[BddStepDefinitions(Namespace = "Sync")]
public sealed class SyncSteps(TodoServiceFixture fixture) : BddStepDefinitionsBase
{
    [BddWhen("I sync")]
    public Task SyncAsync() => fixture.Host.Sync.SyncAsync();
}
```

Steps that reach past the screen on every head (backend, device, stored data) are written once, over
the fixture's arrangement interface (`ITodoArrangement` in the sample), in a project that references
`Bruinet.Bdd` only.

## Run

From `samples/Todo`:

```powershell
dotnet test tests\Bruinet.Samples.Todo.IntegrationTests --filter "FullyQualifiedName~TodoServiceScenarioTests"
```
