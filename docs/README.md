# Bruinet Documentation

Bruinet is a multi-platform .NET UI test automation framework. These docs are
the active, curated set: guides for someone *using* Bruinet. The decisions and
invariants behind it - what must stay true when *changing* Bruinet - are in
[`.docs`](../.docs/README.md).

## Start Here

- [Quick Start](getting-started/quick-start.md)
- A first test per stack, compiled with the build: [web](getting-started/quick-start-html.md),
  [MAUI](getting-started/quick-start-maui.md), [WPF](getting-started/quick-start-wpf.md),
  [WinForms](getting-started/quick-start-winforms.md),
  [native Android](getting-started/quick-start-native-android.md),
  [Stride](getting-started/quick-start-stride.md), and [scenarios in Markdown](getting-started/quick-start-uat.md)
- [Framework Overview](getting-started/framework-overview.md)
- [Test Writing Guide](guides/test-writing.md)
- [Troubleshooting](guides/troubleshooting.md)

## Samples

- [Todo: one MAUI app, five test tiers](../samples/Todo/README.md): page objects and
  ControlObjects, WireMock, a seeded SQLite database, and a test pyramid traced to user journeys,
  on Windows and Android.
- [One Todo UAT, end to end](../samples/Todo/uat-walkthrough.md): a Markdown scenario from
  `Given` to the running app - phrases, page objects, config and fixture.

## Architecture

- [Architecture Overview](architecture/overview.md)
- [Codebase Structure](architecture/structure.md)
- [Technology Stack](architecture/stack.md)
- [Testing](architecture/testing.md)
- [Architectural Decisions, Invariants, Contracts](../.docs/README.md)

## Controls

- [Control Object Index](controls/index.md)
- [Core Interfaces](controls/interfaces.md)
- [CommunityToolkit.Maui Controls](controls/community-toolkit.md)

## Guides

- [Test Writing](guides/test-writing.md)
- [UAT Template Guide](guides/uat-template-guide.md) - project layout, `uat.config.md`,
  scenario file shape
- [UAT Step Definitions And Flows](guides/uat-step-definitions.md) - the step vocabulary and how to
  extend it
- [Features Below The UI](guides/features-below-the-ui.md) - the `service` head: when a scenario
  runs without a screen, its suite and step definitions
- [The bUnit Head](guides/bunit.md) - a Blazor suite's page objects and scenarios without a browser
- [The MauiHeadless Head](guides/maui-headless.md) - a MAUI suite's page objects and scenarios without a device
  (`BRUINET_HEAD=bunit`), what needs one (`[BrowserFact]`, `@browser`), and what behaves differently
- [Test Selection](guides/test-selection.md) - the tests a change reaches: the touch map from a full
  run, `bruinet tests affected`, what runs the suite whole, and what selection misses
- [SQL Replay](guides/sql-replay.md) - a UI test without its database: the SQL log, recording and
  replaying, the app's wiring (EF, ADO.NET, an app in its own process), stale recordings and re-records
- [HTTP Replay](guides/http-replay.md) - a test without the services its app calls: record once, replay with none,
  a mode per dependency and the hermetic run
- [API Contracts](guides/contracts.md) - a live run's calls checked against the DTOs the app reads and the
  requests a reference client sends, with a hermetic check of every write in the mock backend
- [BDD Glossary](guides/bdd-glossary.md) - Bruinet's words beside Cucumber's and Reqnroll's
- [Coming From Reqnroll](guides/coming-from-reqnroll.md) - one scenario in both forms, the attributes
  side by side, what works differently and what is not there, and why
- [Scenario Preview In Visual Studio](guides/visual-studio-preview.md) - the extension: feature and step
  files as readable, bound steps beside the editor; Run, Run as test, Debug and Step through a scenario
- [Bruinet BDD In VS Code](guides/vscode-extension.md) - the VS Code extension: the same server and
  preview, Test Explorer on the scenarios' addresses
- [Narrated Recordings](guides/narrated-recordings.md) - record scenarios on video with a voice-over:
  video scripts (`*.video.md`), Record in the IDEs and Presenter, `BRUINET_RECORD`
- [The Scraper](guides/scraper.md) - record or explore an app's pages into a UI test project,
  draft page objects from them, and have an agent make controls; from VS Code, Visual Studio, the
  command line or MCP, and unattended with the loop
- [Adding Controls For A New Control Library](../.guides/control-library.md) - the
  `control-source-builder` agent, step by step: survey, groundwork, probe, build, closeout
- [Test Data](guides/test-data.md) - test sets, `bruinet data`, `TestData` in UAT
  files, typed mock bodies (`BRUINET001`)
- [Test Settings](guides/settings.md)
- [Reporting And Artifacts](guides/reporting-artifacts.md)
- [What Bruinet Writes](guides/what-bruinet-writes.md)
- [Migration Notes](guides/migration.md)
- [Troubleshooting](guides/troubleshooting.md)

## Platform Guides

- [MAUI](platform-guides/maui.md)
- [Playwright](platform-guides/playwright.md)
- [WinForms](platform-guides/winforms.md)
- [WPF](platform-guides/wpf.md)
- [Stride](platform-guides/stride.md)
- [Native Android](platform-guides/native-android.md)
- [Presenter](platform-guides/presenter.md)

## Run Guides

- [Build And Test](run/build-and-test.md)
- [Scripts For An App's Test Runs](run/app-scripts.md) - headless and Android runners, run guards, test guard, coverage and mutation
- [The `bruinet` Command Line](run/cli.md) - install it, and every command
- [MAUI](run/MAUI.md)
- [MAUI Android](run/maui-android.md)
- [Native Android](run/native-android.md)
- [Playwright](run/Playwright.md)
- [HTML](run/Html.md)
- [WinForms](run/WinForms.md)
- [WPF](run/WPF.md)

## Specs

- [Spec Status](specs/README.md)
- [UAT Scenario Grammar](specs/uat-grammar.md) - the formal format of `.feature.md` and `.steps.md`
  files, and the parser's diagnostics

## Maintenance Rules

- Keep links relative and valid.
- Use `Bruinet.*` namespaces only.
- Mark every command with its working directory.
- Keep guides here; keep decisions and invariants in `.docs/`.
- Do not add generated test artifacts under `docs/` or `.my/`; they belong under `TestResults/`.
