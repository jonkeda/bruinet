# BDD Config

## Runtime

| Field | Value |
| --- | --- |
| Target | MAUI |
| Fixture | MauiFixture |
| AppPath | ../../samples/Bruinet.Samples.Maui.App/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Bruinet.Samples.Maui.App.exe |
| WorkingDirectory | ../.. |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | bin/Debug/net10.0-windows/Bruinet.Maui.UITests.dll |
| Controls | ../../src/Bruinet.Maui/bin/Debug/net10.0/Bruinet.Maui.dll |
| Commands | ../../src/Bruinet.Uat/bin/Debug/net10.0/Bruinet.Uat.dll |

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
