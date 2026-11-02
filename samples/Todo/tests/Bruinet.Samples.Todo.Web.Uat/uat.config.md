# UAT Config

## Runtime

| Field | Value |
| --- | --- |
| Target | BLAZOR |
| Fixture | TodoWebUatFixture |
| Head | web |
| WorkingDirectory | ../../../.. |
| DefaultTestData | one-todo |
| Hermetic | true |
| HttpReplayPace | recorded |

## Apps

| Name | ClientId |
| --- | --- |
| Alice | web-alice |
| Bob | web-bob |

## Dependencies

How each scenario gets what is outside the app ([HTTP Replay](../../../../docs/guides/http-replay.md)). The backend is the
stub the scenarios arrange; on bUnit a run with `BRUINET_HTTP_REPLAY=Record`, then `Replay`, answers the main app's calls
from recordings instead (`Bruinet.Samples.Todo.Web.UITests/Recordings/bunit/http`). The database is the test set's.

| Name | Kind | Mode | Where |
| --- | --- | --- | --- |
| backend | http | stub | `TodoServerStub` |
| db | sqlite | copy | the test set's seeded file |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | bin/Debug/net10.0/Bruinet.Samples.Todo.Web.Uat.dll |
| Controls | ../../../../src/Bruinet.Html/bin/Debug/net10.0/Bruinet.Html.dll |
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
