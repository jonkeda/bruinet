# Testing

Working directory for commands in this document: Bruinet root.

## Build

```powershell
dotnet build Bruinet.sln -v:minimal /nr:false
```

## Unit Tests

Run focused unit tests by project:

```powershell
dotnet test tests\Bruinet.Core.Tests\Bruinet.Core.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Maui.Tests\Bruinet.Maui.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Uat.Tests\Bruinet.Uat.Tests.csproj -v:minimal /nr:false
```

## UI Tests

UI tests require platform setup:

- WPF/WinForms/FlaUI tests require Windows desktop access.
- MAUI Appium tests require Appium and a target app/device.
- Playwright tests require browser installation.
- Stride tests require the sample game/app runtime.

Run the platform-specific guide before running live UI tests.

## UAT Tests

UAT test projects combine:

- `bdd.config.md`;
- one or more `.feature.md` scenario files;
- a fixture, an xUnit collection and a `BddScenarioTestBase<TFixture>` class.

A scenario step binds to a phrase, and the built-in phrases are `[BddWhen]` / `[BddThen]` attributes on the
`Bruinet.Core` control interfaces, discovered by the engine rather than listed anywhere
([AD-011](../../.docs/decisions/ad-011-uat-vocabulary-is-attribute-declared.md)). An app adds
verbs of its own the same way.

A UAT project drives a real app, so it costs what a UI run costs: build the app first, and run
one UI or UAT process at a time.

An app with a MAUI and a web head has one set of scenario files; each head's UAT project runs the
scenarios tagged for it (`Head` in `bdd.config.md`, `@maui` / `@web` tags). A scenario can also
use several apps, such as two devices on one backend, and a `mixed` suite runs apps of several
heads in one scenario
([AD-014](../../.docs/decisions/ad-014-one-scenario-file-serves-every-head.md)). A step names the
app it runs on by its user: `Daan taps Save`
([AD-015](../../.docs/decisions/ad-015-a-steps-subject-picks-its-app.md)).

## Several Apps In One Test

`TestAppSet` (`Bruinet.Core.Testing`) holds the apps of a fixture, as rows of an `Apps` table
(`TestAppDefinition`): each app starts the first time a test asks for it, the started ones are
reset between tests (`ResetStartedAsync`), and they stop together. A test finds an app by its
name or its user's (`Get`, `Get<TApp>`), and `CaptureAll` takes a screenshot of every started app,
one folder each. An app is an `ITestApp`: a device of the app under test with its own test
context, named after the app in the call log.

The UAT engine uses the same set (`IUatAppsHost`), so a mixed UAT suite and mixed UI tests can
read one `apps.md`. The Todo sample's `Bruinet.Samples.Todo.Mixed.UITests` drives two Windows
apps side by side (`MauiDriverOptions.Placement`: `left-half`, `right-half`) and a browser on one
fake backend; its Android test runs only with `BRUINET_ANDROID=1`.

See [BDD Suite Guide](../guides/bdd-suite-guide.md),
[UAT Phrases And Flows](../guides/step-definitions.md), and the worked example in
[One Todo UAT, end to end](../../samples/Todo/uat-walkthrough.md).

## Artifact Layout

Bruinet artifacts should use:

```text
TestResults/<run-id>/suites/<suite-name>/
```

Common subfolders:

- `runner/`
- `logs/`
- `screenshots/`
- `uat/`
- `traces/`
- `videos/`
- `downloads/`
- `attachments/`

Prefer the shared artifact provider instead of hardcoded output folders.
