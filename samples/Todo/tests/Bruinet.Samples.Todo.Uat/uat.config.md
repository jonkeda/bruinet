# UAT Config

## Runtime

| Field | Value |
| --- | --- |
| Target | MAUI |
| Fixture | TodoUatFixture |
| Head | maui |
| AppPath | ../../src/Bruinet.Samples.Todo.App/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Bruinet.Samples.Todo.App.exe |
| WorkingDirectory | ../../../.. |
| DefaultTestData | one-todo |
| Hermetic | true |
| HttpReplayPace | recorded |

## Dependencies

How each scenario gets what is outside the app ([HTTP Replay](../../../../docs/guides/http-replay.md)). The backend is the
stub the scenarios arrange; a run with `BRUINET_HTTP_REPLAY=Record`, then `Replay` (what `run_scenario`'s `replay` sets),
answers the app's calls from recordings instead (`Recordings/headless/http`), the stub reached only while recording. The database is the test set's, restored before each scenario.

| Name | Kind | Mode | Where |
| --- | --- | --- | --- |
| backend | http | stub | `TodoServerStub` |
| db | sqlite | copy | the test set's seeded file |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | bin/Debug/net10.0-windows/Bruinet.Samples.Todo.Uat.dll |
| Controls | ../../../../src/Bruinet.Maui/bin/Debug/net10.0/Bruinet.Maui.dll |
| Commands | ../../../../src/Bruinet.Uat/bin/Debug/net10.0/Bruinet.Uat.dll |
| TestData | ../Bruinet.Samples.Todo.TestSupport/bin/Debug/net10.0/Bruinet.Samples.Todo.TestSupport.dll |

## Using

The subjects outside the screen every scenario may arrange or check (phrase namespaces).

| Namespace |
| --- |
| Backend |
| Device |
| AppLifecycle |
| StoredData |

## Discovery

| Field | Value |
| --- | --- |
| RequireExplicitUatAttributes | false |
| AllowNameInference | true |

## Reporting

| Field | Value |
| --- | --- |
| ScreenshotOnFailure | true |
| IncludeRuntimeTrace | true |

## Settings

| Field | Value |
| --- | --- |
| Root | TestSettings |
| DefaultFile | testsettings.json |
| LocalFile | testsettings.local.json |
| ScenarioConvention | scenarios/{ScenarioId}.json |
