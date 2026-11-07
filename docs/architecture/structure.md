# Codebase Structure

Working directory for paths in this document: Bruinet root.

## Top Level

| Path | Purpose |
| --- | --- |
| `src/` | Active Bruinet source projects |
| `tests/` | Active unit, UI, and UAT test projects |
| `samples/` | Sample applications used by UI tests and demos |
| `tools/` | Developer tools: `bruinet`, the command line (`tools/Bruinet.Cli`: the Scraper, test data, control generation, the language and MCP servers), and the IDE extensions |
| `docs/` | Active curated documentation |
| `docs2/` | Preserved previous documentation tree |
| `.my/` | Plans for work in flight, indexed in `.my/README.md` |
| `Bruinet.sln` | The solution: every project, in folders by area, each test project beside the one it is named after. Tests and the Presenter also find the repository root by it |

The two Visual Studio extension projects are in the solution but not built by it; they build from
their own `.slnx` with Visual Studio's MSBuild.

## Source Projects

`src/` contains package-producing projects:

- `Bruinet.Appium` - what the Appium stacks share: Appium's error mapping, `AndroidHost`, `AndroidRecorder`
- `Bruinet.Stride.Automation`
- `Bruinet.Bdd` - feature files without a UI: parse, bind, values, tables, callable steps, namespaces, run
- `Bruinet.Blazor`
- `Bruinet.Contracts` - API contracts: a recorded call checked against the DTO the app reads and the request the reference client sends, and the run report
- `Bruinet.Core`
- `Bruinet.Html`
- `Bruinet.Html.Playwright`
- `Bruinet.Maui`
- `Bruinet.Maui.Appium`
- `Bruinet.Maui.CommunityToolkit`
- `Bruinet.Maui.FlaUI`
- `Bruinet.Logging` - an `ILoggerProvider` for an app's services: what the app logs goes with the step that made it (`StepLog` in Core)
- `Bruinet.Mocking`
- `Bruinet.NativeAndroid` - native Android apps (Views, AndroidX, Material) through Appium, in MAUI's and Html's shape
- `Bruinet.Presenter`
- `Bruinet.Stride`
- `Bruinet.Uat` - UAT on top of `Bruinet.Bdd`: pages, controls, page scope, apps, the UAT test base
- `Bruinet.WinForms`
- `Bruinet.Wpf`

## Test Projects

`tests/` follows naming by capability:

- `Bruinet.*.Tests` for unit and integration-style checks;
- `Bruinet.*.UITests` for live UI automation: the UI tests, the page objects, and the platform's
  UAT suite (`uat.config.md`, `Features/`, and `Uat/` with its fixture and scenario test class),
  which drives the same page objects. A UAT run is the filter `Category=UAT`.

## Samples

`samples/` contains sample apps for:

- Blazor;
- MAUI;
- native Android (`Bruinet.Samples.NativeAndroid.App`, a .NET for Android control gallery);
- Stride;
- WinForms;
- WPF;
- shared sample models and helpers.

## Adding Code

- Add shared contracts to `src/Bruinet.Core`.
- Add platform behavior to the matching `src/Bruinet.*` project.
- Add tests to the matching `tests/Bruinet.*.Tests` project.
- Add UI coverage to `tests/Bruinet.*.UITests` only when a live app or
  platform driver is required.
- Add UAT scenario examples under `Features/` of the matching
  `tests/Bruinet.*.UITests` project.
- Add test settings code under `src/Bruinet.Core/Settings`.
- Add artifact/reporting code under `src/Bruinet.Core/Artifacts` unless the
  behavior is platform-specific.
