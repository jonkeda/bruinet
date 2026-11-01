---
title: Bruinet Agent Instructions
description: Local entry point for AI agents working in the Bruinet submodule
---

# Bruinet Agent Instructions

## Read First

For Bruinet work, read:

- [.github/copilot-instructions.md](.github/copilot-instructions.md)
- [docs/README.md](docs/README.md)

Then read the focused doc for the task:

- architecture or project layout: [docs/architecture/structure.md](docs/architecture/structure.md)
- tests or artifacts: [docs/architecture/testing.md](docs/architecture/testing.md)
- controls: [docs/controls/index.md](docs/controls/index.md)
- UAT: [docs/guides/uat-template-guide.md](docs/guides/uat-template-guide.md)
- commands: [docs/run/build-and-test.md](docs/run/build-and-test.md)

The previous docs tree is preserved in [docs2](docs2/README.md). Treat it as an
archive, not the active source of truth.

## Scope

- Bruinet-only work stays inside `Bruinet/`.
- Parent BodyCam work follows `../.github/copilot-instructions.md`.
- Exact.Construction, `.cnv2`, or sibling conversion references are optional and
  task-specific; do not require missing files for normal Bruinet work.

## Skills

Task workflows live in `.github/skills/` (Claude Code reads them through stubs in
`.claude/skills/`):

- [maui-control](.github/skills/maui-control/SKILL.md) - create or extend a MAUI
  ControlObject: simple control, container, component, or collection.
- [maui-ui-test](.github/skills/maui-ui-test/SKILL.md) - write MAUI UI tests and page
  objects.
- [convert-control](.github/skills/convert-control/SKILL.md) - convert a hand-written
  control to the `.tpl.cs` / `.gen.cs` format.
- [html-control](.github/skills/html-control/SKILL.md) - create or extend an Html or Blazor
  ControlObject, container, frame or collection.
- [html-ui-test](.github/skills/html-ui-test/SKILL.md) - write Html and Blazor UI tests and
  page objects (the Html UI tests, the Blazor UI tests, the Todo web head).
- [android-control](.github/skills/android-control/SKILL.md) - create or extend a native Android
  ControlObject, container or collection.
- [android-ui-test](.github/skills/android-ui-test/SKILL.md) - write native Android UI tests, UAT
  scenarios and page objects (the gallery app on the emulator).
- [scrape-pages](.github/skills/scrape-pages/SKILL.md) - make Html or Blazor page objects and
  controls from an app's pages recorded with the Scraper (MCP `start_recording`,
  `detect_patterns`, `write_code`, `check_code`).
- [narrated-review](.github/skills/narrated-review/SKILL.md) - finish a feature with its
  scenarios recorded on video with a voice-over, and a review page (MCP `record_video`,
  `review_page`).
- [ai-item](.github/skills/ai-item/SKILL.md) - build one item of the AI improvements plan
  (`.my/aiimprovements/plan.md`) through design, test-first build, checks, an agent check,
  review and close.

Read the matching skill before doing that task.

## Never In Tests

In tests, page objects' public members, app containers and test helpers - even for a quick
fix:

- no `Context.Driver` or other `IMauiDriver` member;
- no `IMauiElement` variables, parameters, returns or members;
- no `FindElement` / `FindElements` / `TryFindElement`, `ContainerRoot`, `AppElement` or
  `TryGetItemRoot`;
- no `Locator` construction in a test method.

Use or add a page-object or control member instead. The reasons and the one exception
(driver and bridge framework tests) are in
[forbidden-apis.md](.github/skills/maui-ui-test/references/forbidden-apis.md). The same holds
for Html (`IHtmlElement`, `IHtmlDriver`, Playwright types):
[Html forbidden-apis.md](.github/skills/html-ui-test/references/forbidden-apis.md).

## Core Rules

- Build tests through Bruinet page objects and ControlObjects.
- Put repeated interaction behavior in Bruinet controls, not local test helpers.
- Keep test methods focused on user intent and assertions.
- Prefer semantic operations such as `Click`, `SetText`, `SelectItem`,
  `WaitReady`, and assertions over raw driver operations.
- Prefer UI Automation patterns before pointer or coordinate strategies.
- Do not expose direct mouse movement as a normal public test API.
- MAUI on Windows uses no physical input (mouse, keyboard, clipboard, foreground), and
  there is no switch to turn it on. Do not add any to `Bruinet.Maui.FlaUI`: an action
  with no UI Automation pattern and no bridge verb throws, naming the route. A test of
  real input belongs on the Android head. WPF and WinForms still go through
  `PhysicalInput` and `BRUINET_BACKGROUND_MODE`. See AD-005.
- An action with no UI Automation route goes through the app's gesture bridge
  (`GestureAutomation.Verbs`), never through coordinates - and only after the three
  admission tests in AD-008. For MAUI on Windows the bridge is required.

## Synchronization

- Do not add arbitrary sleeps or longer waits to fix tests.
- Wait for concrete UI state: loaded pages, visible/enabled controls, text,
  busy sentinel changes, request observation, or navigation completion.
- Named polling intervals, cancellation-aware retries, host startup loops, mock
  cadence, and debug-only pauses are acceptable when intentional.
- When a UI test fails, inspect screenshots, diagnostics, runner output, and app
  logs before changing code.

## Code Style

- Use xUnit `Assert`; do not add FluentAssertions.
- Do not add empty catches. Optional platform probing belongs in named `Try*`
  helpers with clear fallback behavior.
- Keep platform element types out of `Bruinet.Core`.
- Match existing project layout under `src/` and `tests/`.

## Docs Rules

Five trees, each with one job:

- **`.docs/`** - decisions, invariants and contracts: what must stay true. Read it before
  changing Bruinet. A decision is recorded **once**, here, never also in code prose.
- `docs/` - active guides for someone *using* Bruinet.
- `.my/` - plans, analyses and reviews for work in flight, indexed in `.my/README.md`.
  When the work is done: extract, then delete. Move its decisions to `.docs/` and anything a
  user needs to `docs/`, repoint the links, then delete the topic. An AD that needs the
  evidence links to it at its last commit. Code comments never point into `.my/`.
- `.guides/` - user guides: step-by-step how-tos for a task, such as adding controls for a new
  control library. Linked from `docs/README.md`.
- `docs2/` - historical archive.

- Keep links valid and relative.
- Use `Bruinet.*` namespaces in examples.
- Mark commands with their working directory.
- Update `docs/README.md` when adding or moving active docs.

Comment policy (full version in
[.github/instructions/csharp-comments.instructions.md](.github/instructions/csharp-comments.instructions.md)):
keep short `<summary>`/`<param>`/`<returns>` on public and protected members and a one-line
`// why` with a pointer where a decision surprises; delete `<remarks>` that restate the code
and all XML doc on private/internal members. Durable rationale goes to `.docs/`, not code prose.

## Verification

Commands are from the Bruinet root:

```powershell
dotnet build Bruinet.sln -v:minimal /nr:false
dotnet test tests\Bruinet.Core.Tests\Bruinet.Core.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Maui.Tests\Bruinet.Maui.Tests.csproj -v:minimal /nr:false
dotnet build src\Bruinet.Maui.FlaUI\Bruinet.Maui.FlaUI.csproj -f net10.0-windows -v:minimal /nr:false
dotnet test tests\Bruinet.Html.Tests\Bruinet.Html.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Blazor.Tests\Bruinet.Blazor.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Html.Bunit.Tests\Bruinet.Html.Bunit.Tests.csproj -v:minimal /nr:false
```

The Html and Blazor UI suites (`tests\Bruinet.Html.UITests`, `tests\Bruinet.Blazor.UITests`)
start the Blazor sample themselves on a free port and need Playwright's Chromium installed
(`pwsh tests\Bruinet.Html.UITests\bin\Debug\net10.0\playwright.ps1 install chromium`).
They also run without a browser, on the **bUnit head**: `$env:BRUINET_HEAD = "bunit"` renders the
sample's components in the test process (about 2 s for the Blazor suite against about 40 s in a
browser). Tests that need a browser are `[BrowserFact]`, scenarios `@browser`, and are skipped there
([the guide](docs/guides/bunit.md), AD-030).

### Match the test scope to the change

**Do not run the full UI suite to verify a narrow change.** `Bruinet.Maui.UITests`
launches a real Windows app and drives it through UI Automation; it takes minutes,
and most of it is irrelevant to any given edit. Run the smallest tier that can
actually falsify the change, and only widen if that tier fails or the change is
genuinely broad.

| Tier | Command (filter on `tests\Bruinet.Maui.UITests`) | Cost |
|---|---|---|
| 1. One area | `--filter "FullyQualifiedName~AutomationProbeTests"` | ~7 s |
| 2. Related areas | `--filter "FullyQualifiedName~Tests.Container\|FullyQualifiedName~Tests.Collection"` | ~11 s |
| 2b. Gesture bridge and background | `--filter "Stage=Background"` - bridge verbs, focus, text, scrolling, navigation, the foreground watchdog | ~20 s |
| 3. Full suite | no filter | ~3 min |
| Stress (opt-in) | `$env:BRUINET_STRESS = "1"` then `--filter "Pattern=Stress"` - reproduces UI Automation retiring the window element | 1-2 min |

Use tier 3 only when finishing a phase of work, when the change touches shared
infrastructure (fixture, navigation, `MauiProgram`, handler registration), or when a
narrower tier fails in a way that suggests wider breakage.

For the Html and Blazor suites, the same rule with two heads: while working, run the
narrowest filter on the bUnit head (`$env:BRUINET_HEAD = "bunit"`); before reporting the work
done, run that filter once in the browser (unset the variable). The browser judges: a pass on
bUnit alone is not done, because bUnit has no layout, style sheets or script.

The narrowest filter comes from the change: `pwsh tools/Scripts/select-tests.ps1` (`bruinet tests affected`,
`docs/run/cli.md`) follows the working tree's changed files through each suite's `touch-map.json` to the tests and
scenarios they reach, prints each with why and one `--filter`, and runs it with `-Run` (`-Head playwright` for the
browser). Shared code (`src/`, the test base, the sample's layout and start) runs the suite whole. After a full run,
`-WriteMap` refreshes the maps; commit them.

Notes:

- A platform's UAT suite lives in its `*.UITests` project (`uat.config.md`, `Features/`, `Uat/`):
  `--filter "Category=UAT"` runs it alone, `--filter "Category!=UAT"` the UI tests alone.
- A behaviour is checked by a UAT scenario or by a code UI test, never both: a scenario for what a user does and sees,
  a code UI test for what a scenario says badly (layout at a size, a redirect, a head's detail). Where both exist, the
  code test's extra checks move into the scenario and the code test goes (the `*-ui-test` skills).
- `dotnet test` takes **one** `--filter`; passing two silently drops the first. Use
  `|` for OR and `&` for AND inside a single filter string.
- The full suite is expected to be **green with nothing set**: 0 failed. The only deliberate
  skip is the opt-in stress test. A failure is a
  regression until shown otherwise; still establish it against a rebuilt baseline before
  reporting it. `OccludedScreenshotTests` reads real screen pixels and fails its own
  check when other always-on-top windows are open - rule that out first.
- **Rebuild the sample app before a UI run** when anything under `samples/` changed.
  `dotnet test` builds the test project, not the app it launches, and a stale app
  binary passes or fails for reasons that no longer exist.
- **One UI test process at a time**, and do not build while one is running: a build
  overwrites binaries under a live run and every result after that is fiction.
- Each run writes `test-timings.md` and `accessibility-audit.md` under
  `TestResults/<run-id>/suites/<suite>/attachments/`. A class marked **slower** in the
  timings report is the framework starting to wait for something.
- A mechanical refactor (moving files, extracting a project) is verified by *the same
  tests passing identically*, not by running more of them.
