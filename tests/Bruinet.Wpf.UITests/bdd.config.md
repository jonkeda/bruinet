# BDD Config

## Runtime

| Field | Value |
| --- | --- |
| Target | WPF |
| Fixture | WpfUatFixture |
| AppPath | ../../samples/Bruinet.Samples.Wpf.App/bin/Debug/net10.0-windows/Bruinet.Samples.Wpf.App.exe |
| WorkingDirectory | ../.. |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | Bruinet.Wpf.UITests.dll |

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
