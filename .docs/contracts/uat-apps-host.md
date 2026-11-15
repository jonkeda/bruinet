# Contract: Apps in a test (`TestAppSet`, `IUatAppsHost`)

A test or scenario that uses several apps (two devices on one backend, several users, apps of
different heads) gets them from one set. This is what a fixture author implements and what the
UAT engine and UI tests rely on. Decisions: [AD-014](../decisions/ad-014-one-scenario-file-serves-every-head.md)
(heads, `mixed`) and [AD-015](../decisions/ad-015-a-steps-subject-picks-its-app.md) (a step's
subject).

## Who owns what

| Part | Owner | Holds |
| --- | --- | --- |
| `TestAppDefinition`, `ITestApp`, `TestAppSet` | `Bruinet.Core.Testing` | apps: rows, running apps, start, reset, evidence |
| `IUatAppsHost` | `Bruinet.Uat` | words: sessions, subjects, a catalog per head |
| A device per app (`TodoMauiDevice`, `TodoWebDevice`) | the app's test support | how one app is arranged and started |

Core does not reference `Bruinet.Uat`. In C# UI tests apps are variables with their own page
types; only UAT has subjects and a current app.

## The rules

- **One table.** Apps are the rows of an `Apps` table (`Name`, `User`, optional `Head`, any other
  columns as values). A UAT suite and the UI tests of the same apps read one file (`AppsFile` in
  `bdd.config.md`); an inline table also works. Two app names that differ only in case are an
  error.
- **Found by name or user.** `Get("Daan")` finds the app called `Daan` or the one app of user
  `Daan`. A miss names every choice. `Get<TApp>` fails naming the app's head when the app is of
  another kind.
- **Started lazily, one at a time.** An app starts the first time a test asks for it; apps a
  test does not use stay stopped. Only started apps are reset between tests (`ResetAsync`: online,
  on its first page). All stop together, in reverse start order.
- **Each app is its own.** Its own database, launch settings, test context (named after the app,
  so logs and failures say whose it was) and screenshot folder.
- **Evidence from every started app.** A failure captures every started app (`CaptureAll`), one
  folder per app under the suite's artifacts (AD-007); the UAT result lists each path.
- **Per-app scenario state.** The current page and the service provider are kept per app, so
  switching apps does not lose where the other one was.

## What an `IUatAppsHost` fixture provides

- `Apps`: the `TestAppSet`.
- `AddAppServices(app, services)`: what that app's page objects need, such as its test context.
- `ModulesFor(app)`: the modules of the app's head, for a `mixed` suite; null means the fixture's
  own. The scenario checker uses it too, so it must answer without starting anything.
- Optionally `OnAppUsed(app)`, called each time an app becomes current.

The engine builds one composition and scope per app on first use and keys it by the running app
object, so a restarted app gets a fresh scope. The fixture does not write `UseSession`.

## Broken when

- A fixture keeps its own dictionary of apps or sessions beside a `TestAppSet`.
- Starting a fixture starts apps no test asked for, or a reset touches an app that was not
  started.
- A failure captures only the current app while others are running.
- Two files list the same cast for a UAT suite and its UI tests.
- `Bruinet.Core` references `Bruinet.Uat`.
