# Bruinet

Bruinet is a UI test framework for .NET. One way to write a test, **page objects and control objects, never a driver or a sleep**,
reaches MAUI, WPF, WinForms, web, Blazor, native Android, Stride games, and the apps' own services. Scenarios are plain
Markdown a non-programmer can read, and the same files run in a browser, on a device, without a device at all, or below the UI.

Around the framework are the tools a team needs to live with it: language servers and extensions for Visual Studio and
VS Code, a command line and an MCP server for agents, a Scraper that drafts page objects from a running app, SQL replay so a UI test
needs no database, test selection from a change, and narrated video recordings of scenarios with a voice-over.

## At a glance

| You want to | Bruinet gives you |
| --- | --- |
| Test an app through its UI, on any stack | Page objects and ControlObjects over FlaUI, Appium, Playwright, Selenium, Stride and Android drivers |
| Write tests a product owner can read | Markdown scenarios (`*.feature.md`) with `Given`, `When`, `Then`, bound to your page objects |
| Run fast, without the device | The **MauiHeadless** and **bUnit** heads render pages in the test process; the **service** head runs below the UI |
| Keep the real device as the judge | The same scenarios and page objects on Windows, Android and iOS, the default head |
| Stop hand-writing page objects | The **Scraper**: record or explore an app, get draft page objects and controls |
| Test without the database | **SQL replay**: record a run's SQL once, replay it in every later run |
| Run only the tests a change reaches | **Test selection** from a touch map: `bruinet tests affected` |
| Show what a feature does | **Narrated recordings**: a scenario on video, spoken by a Windows voice, Azure OpenAI or Azure Speech |
| Work from the editor or an agent | Visual Studio and VS Code extensions, the `bruinet` CLI, an MCP server, skills for agents |

## What a test looks like

A scenario, in a `*.feature.md` file beside the app's tests:

```gherkin
Given I am on the Todo List page
When I tap Sync
Then Todos should have todo "Buy bread"
```

Each phrase is bound to a page object member. Page objects own a screen's structure, ControlObjects own how a control is driven,
and the platform driver stays behind Bruinet's interfaces. The tests never change when the stack under them does.

```text
Tests and UAT scenarios
  Page objects and fixture glue
    Bruinet controls and platform contexts
      Bruinet.Core: interfaces, locators, waits, artifacts
        Drivers: FlaUI, Appium, Playwright, Selenium, Stride, Android, MauiHeadless, bUnit
```

## Where it runs: the heads

A **head** is how a suite reaches its app. A suite picks one with `BRUINET_HEAD`, and the same page objects and scenarios
work on every head of its stack. The device or browser stays the judge at a gate; the lighter heads make the loop fast.

| Stack | Heads |
| --- | --- |
| MAUI | Windows (FlaUI), Android and iOS (Appium), **MauiHeadless** (`headless`: no device, emulator or window) |
| Web | Playwright and Selenium in a browser, **bUnit** (`bunit`: a Blazor component, no browser) |
| WPF, WinForms | FlaUI on Windows |
| Native Android | Appium with UiAutomator2 |
| Stride | An in-game automation server and a test client |
| Below the UI | The **service** head: the same feature files against the app's services, no screen |
| Mixed | One scenario that drives several apps at once: Windows, web and Android (Todo's `Mixed.Uat`) |

[MauiHeadless](docs/guides/maui-headless.md) is also a package of its own: headless component testing for .NET MAUI on xUnit, NUnit
or MSTest, with no Bruinet in it.

## Tools

| Tool | What it does | Guide |
| --- | --- | --- |
| `bruinet` CLI | Scraper, test data, scenario previews and results, control generation, test selection, and the servers behind the extensions and agents (`bruinet lsp`, `bruinet mcp`) | [Command line](docs/run/cli.md) |
| Visual Studio and VS Code | Feature and step files as readable, bound steps beside the editor; Run, Debug and Step through a scenario; Record on a video script | [Visual Studio](docs/guides/visual-studio-preview.md), [VS Code](docs/guides/vscode-extension.md) |
| Presenter | A desktop shell for a UAT workspace: run and step scenarios, record them on video, set up the narrator's voices | [Presenter](docs/platform-guides/presenter.md) |
| Scraper | Record or explore an app's pages into a test project; patterns, matching and draft page objects; no model call in the engine | [Scraper](docs/guides/scraper.md) |
| SQL replay | The SQL log of a run, a schema snapshot, and replay from recordings, for EF Core, ADO.NET and apps in a process of their own | [SQL replay](docs/guides/sql-replay.md) |
| Test selection | A touch map from a full run, then only the tests a change reaches | [Test selection](docs/guides/test-selection.md) |
| Test data | Typed, validated test sets with diagnostics at file and line, and JSON schema | [Test data](docs/guides/test-data.md) |
| Narrated recordings | A scenario or a video script on video with a voice-over and touch marks: a Windows voice, Azure OpenAI, or Azure Speech, with SSML in a line and a suite's `pronunciations.md` | [Narrated recordings](docs/guides/narrated-recordings.md) |

## Packages

Packages target `net8.0`, `net9.0` and `net10.0`; the SDK is pinned in `global.json`.

**Core**

| Package | Purpose |
| --- | --- |
| `Bruinet.Core` | Contracts, locators, waits, the call model, artifacts, settings and test attributes |
| `Bruinet.Bdd` | Markdown feature files: parse, bind, values, tables, callable steps and run, with no UI |
| `Bruinet.Uat` | Markdown-driven UAT scenario runtime for xUnit |
| `Bruinet.TestData` | Typed, validated test data and test sets |
| `Bruinet.Logging` | An app's log kept by the step that made it, for the step's result: `AddBruinetStepLog()` |
| `Bruinet.Mocking` | WireMock servers and mock sensors for UI tests |

**Platforms**

| Package | Purpose |
| --- | --- |
| `Bruinet.Maui` | MAUI controls, page objects and driver factory |
| `Bruinet.Maui.FlaUI`, `Bruinet.Maui.Appium` | The Windows and the Android and iOS drivers for MAUI |
| `Bruinet.Maui.Headless`, `MauiHeadless` | The headless head for MAUI, and the headless component testing framework under it |
| `Bruinet.Maui.CommunityToolkit` | CommunityToolkit.Maui controls |
| `Bruinet.Html`, `Bruinet.Html.Playwright` | Web abstractions, controls, pages and containers, and the Playwright driver |
| `Bruinet.Html.Bunit`, `Bruinet.Blazor` | The bUnit head, and Blazor helpers |
| `Bruinet.Wpf`, `Bruinet.WinForms` | Desktop automation through FlaUI |
| `Bruinet.NativeAndroid` | Native Android apps through Appium |
| `Bruinet.Stride`, `Bruinet.Stride.Automation` | Stride game UI testing: the client, and the server inside the game |
| `Bruinet.Appium`, `Bruinet.FlaUI`, `Bruinet.Uia.Contracts` | What the Appium stacks share, what the FlaUI stacks share (MAUI on Windows, WPF, WinForms), and the wire contract of the custom UI Automation pattern |

**Data**

| Package | Purpose |
| --- | --- |
| `Bruinet.Mocking.Data` | The SQL log and schema snapshot; with `.EntityFrameworkCore`, `.EntityFrameworkCore.SqlServer`, `.AspNetCore` and `.SystemWeb` hooks for SQL replay |

**Tooling**

| Package | Purpose |
| --- | --- |
| `Bruinet.Bdd.Workspace` | What every tool shares: a suite loaded one way, and a feature, step or video file rendered as the preview shows it |
| `Bruinet.Scraper` | The Scraper's engine: recordings, patterns, matching, diff and code output |
| `Bruinet.Recording`, `Bruinet.Narrator`, `Bruinet.Narrator.Windows` | Screen recording, narrated recordings with their voices, and the Windows recorder and voices |
| `Bruinet.Presenter` | The desktop presenter |
| `Bruinet.Cli` | The `bruinet` command line, a .NET tool |

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | The packages above |
| `tests/` | Unit, UI and UAT tests, one project per package and head |
| `samples/` | Sample apps used by tests and demos, and the Todo sample |
| `tools/` | The CLI, the Visual Studio and VS Code extensions, and scripts |
| `docs/` | Guides for using Bruinet; `.docs/` holds the decisions and invariants behind it |
| `Scraper.Sites/` | What the Scraper makes of public sites, one page per technology |
| `.my/` | Plans for work in flight, indexed in `.my/README.md` |

## Get started

```powershell
git clone https://github.com/exact-mobile-apps/test-tooling.git Bruinet
cd Bruinet
dotnet build Bruinet.sln -v:minimal /nr:false
```

`Bruinet.sln` holds every project, in folders by area. The two Visual Studio extension projects are in it but not built by it: they need
Visual Studio's MSBuild, and build from `tools\Bruinet.Bdd.VisualStudio\Bruinet.Bdd.VisualStudio.slnx`.

Install the command line anywhere, or as a local tool for a repository's scripts and CI:

```powershell
dotnet tool install -g Bruinet.Cli
bruinet --help
```

Then read the [Quick Start](docs/getting-started/quick-start.md) for a first page object, and the [Todo sample](samples/Todo/README.md) to
see one app tested at five tiers.

## Samples

- [Todo](samples/Todo/README.md): one MAUI app, five test tiers: page objects, WireMock, a seeded SQLite database, a test pyramid
  traced to user journeys, and UAT scenarios, on Windows and Android, headless, on the web and below the UI. It also has video scripts.
- [One Todo UAT, end to end](samples/Todo/uat-walkthrough.md): a Markdown scenario from `Given` to the running app.
- `samples/` also holds small apps for each stack: MAUI, Blazor, native Android, WPF, WinForms, Stride and SQL.

## Documentation

- [Documentation index](docs/README.md): quick start, framework overview, guides by topic, platform and run guides.
- Guides for the heads: [MauiHeadless](docs/guides/maui-headless.md), [bUnit](docs/guides/bunit.md), [features below the UI](docs/guides/features-below-the-ui.md).
- Guides for writing: [test writing](docs/guides/test-writing.md), [UAT scenarios](docs/guides/uat-template-guide.md), [step definitions](docs/guides/uat-step-definitions.md), [control objects](docs/controls/index.md), [coming from Reqnroll](docs/guides/coming-from-reqnroll.md).
- Architecture: [overview](docs/architecture/overview.md), [structure](docs/architecture/structure.md), [testing](docs/architecture/testing.md), and [the decisions and invariants](.docs/README.md).
- Build and run: [build and test](docs/run/build-and-test.md), the [command line](docs/run/cli.md), [troubleshooting](docs/guides/troubleshooting.md).

## Working with agents

`bruinet mcp` serves the suite to Copilot, Claude and other agents: scenarios and steps, page objects, recorded pages, test runs, video
recording and review pages. [AGENTS.md](AGENTS.md) is the entry point, and `.claude/skills` holds skills for writing MAUI, web and
Android UI tests, making controls, scraping pages and handing over a narrated review.

## Core rules

- Tests express user intent.
- Page objects own screen structure; controls own repeated interaction behavior.
- Wait for concrete UI state instead of adding arbitrary sleeps.
- Use xUnit `Assert`; do not add FluentAssertions.
- Route screenshots, logs, traces and UAT output through `TestResults/<run-id>/`.

See also [CONTRIBUTING](CONTRIBUTING.md), [VERSIONING](VERSIONING.md), the [breaking changes policy](BREAKING-CHANGES-POLICY.md) and the [CHANGELOG](CHANGELOG.md).

## License

Bruinet is licensed under the MIT License. See [LICENSE](LICENSE).
