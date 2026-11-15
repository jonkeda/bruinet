# BDD Config

The mixed suite: scenarios that use apps of several heads. Its apps are the ones in `apps.md`,
the table the mixed UI tests read too. It runs only scenarios tagged `@mixed`.

## Runtime

| Field | Value |
| --- | --- |
| Target | MIXED |
| Fixture | TodoMixedUatFixture |
| Head | mixed |
| AppsFile | apps.md |
| WorkingDirectory | ../../../.. |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | bin/Debug/net10.0-windows/Bruinet.Samples.Todo.UITests.dll |
| Pages | bin/Debug/net10.0-windows/Bruinet.Samples.Todo.Web.UITests.dll |
| Commands | bin/Debug/net10.0-windows/Bruinet.Samples.Todo.Mixed.Uat.dll |

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
| RequireExplicitBddAttributes | false |
| AllowNameInference | true |

## Reporting

| Field | Value |
| --- | --- |
| ScreenshotOnFailure | true |
| IncludeRuntimeTrace | true |

## Skip Rules

| Tag | EnvironmentVariable |
| --- | --- |
| android | BRUINET_ANDROID |
