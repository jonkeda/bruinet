# Bruinet for agents: ten proposals

2026-11-01

Start with three: a live session, failures as structured data, and a map from UI element to source line. Together they close
the loop an agent needs: drive the app, see exactly what went wrong, and find the code to change, in either the test or the
software under test.

Bruinet's MCP server already lets an agent read steps, draft page objects and tests, run them and record video. What it lacks
is the fast inner loop: today an agent writes a whole test, runs it, and reads a summary. The proposals below make each step
of that loop cheap and observable. Each names the tool it borrows from where one exists.

## 1. Live session: drive the app one step at a time

An agent opens a session on a running app and sends one phrase or page object call at a time, getting the new page state back
after each. The app stays open between calls. Taken from Playwright MCP and Chrome DevTools MCP, but through page objects
instead of raw clicks.

- **Tools:** `session_open <suite> <head>`, `do_step "<phrase>"`, `call <Page>.<Member>(args)`, `get_state`, `session_close`.
- **State back:** the page object in view, its controls and their values, as a short text snapshot, not a screenshot.
- **Record as you go:** the session's steps can be saved as a draft `*.feature.md`, so exploring and writing a test are one act.

An agent tries a step in seconds instead of rewriting and re-running a whole scenario.

## 2. Failures as structured data

`get_failure <test>` returns one JSON object: the failing step, expected and actual, the page snapshot, the app's log lines and
exceptions around it, and the since-last-green diff. Taken from how SWE-agent and OpenHands feed test output to models: short,
structured and complete.

- **Kinds:** locator not found, assertion failed, timeout, app exception, setup failed, each with its own fields.
- **Pointers:** the page object member, the step binding, and the feature file line, each as `file:line`.
- **Size:** capped and summarized, so a failure fits in a few hundred tokens instead of a stack trace and a log dump.

The agent stops guessing from a stack trace and starts at the right file.

## 3. Map from UI element to source line

Every element a test touches can be traced to the Blazor component, XAML file or WinForms designer line that rendered it.
Taken from LocatorJS, React DevTools' "view source" and Vue's inspector; .NET UIs have nothing like it.

- **How:** a Debug-only source generator or MSBuild step adds a `data-bruinet-src="Pages/Map.razor:42"` attribute (or an automation property on desktop heads).
- **Tool:** `where_is <Page>.<Control>` returns the app file and line, and the page object file and line.
- **Use:** an agent fixing the software under test jumps from "button missing" to the markup that should render it.

This is what turns a UI test failure into a fix in the app, not just a fix in the test.

## 4. App telemetry correlated per step

The trace for each step includes what happened inside the app during that step: `ILogger` lines, unhandled exceptions, failed
HTTP calls, SQL, and browser console errors. Taken from Sentry's session replay and Replay.io, which join UI events to backend
traces.

- **How:** a test host logging provider and an `Activity` per step, so app logs carry the step's id.
- **Tool:** `step_log <test> <step>` returns only that step's app-side events.
- **Payoff:** "the list is empty" comes with "`TodoStore.Load` threw `SqlException`" next to it.

Most UI test failures are really app failures; this shows the app side without attaching a debugger.

## 5. Plan, generate and heal agents as skills

Three ready-made agent roles over Bruinet's tools: a planner that turns a brief into scenarios, a generator that writes steps
and page objects and runs them, and a healer that fixes failing tests. Taken from Playwright's test agents (planner, generator,
healer).

- **Planner:** reads a brief or user story, uses the live session to explore, writes `*.feature.md` with marked gaps.
- **Generator:** fills the gaps through `test_brief`, `draft_test` and `write_code`, then runs until green.
- **Healer:** takes a failure, decides whether the test or the app is wrong, and proposes one change with evidence.
- **Shipped as:** skills and agent definitions in the package, so any team gets them with `bruinet init`.

Teams get a working agent setup on day one instead of writing their own prompts.

## 6. Breakpoints in scenarios

An agent can pause a run before or after a step, inspect state, call page object members, and continue. Taken from debuggers
via the Debug Adapter Protocol, and Playwright's `page.pause()`.

- **Tools:** `run_test --break-at "<phrase>"`, then the live session tools (proposal 1) on the paused run, then `continue`.
- **Conditional:** break on the first failure, or when a step takes longer than a set time.
- **Same engine:** the VS Code extension's Step button and the agent use one implementation.

An agent investigates the exact moment before a failure instead of re-running with extra logging.

## 7. Safe refactoring tools

Renaming a step phrase, moving a control to another page object, or splitting a page object updates every feature file,
binding and usage at once. Taken from IDE refactorings (Roslyn's rename) and Cucumber's step rename in IntelliJ.

- **Tools:** `rename_step`, `move_control`, `split_page`, `extract_control`, each returning a diff.
- **Checks:** every change is validated with `validate_scenario` and a build before it is offered.
- **Builds on:** `find_step_usages`, which already finds the call sites.

Agents stop making half-finished renames that break scenarios they didn't open.

## 8. Put the app in a state directly

An agent sets up the state a test needs (a logged-in user, a full cart, a survivor at a place) by calling the app's test data
or service head, not by clicking through ten screens. Taken from Cypress's app actions and `cy.task`, and Playwright's
`storageState`.

- **Tools:** `list_states`, `set_state <name> [args]`, backed by Bruinet test sets and the service head.
- **In scenarios:** a `Given` can name a state, so tests start where they mean to.
- **Guarded:** states are declared in the test project, so an agent cannot invent one that writes to a real database.

Each try of the inner loop starts in a second, at the screen that matters.

## 9. Change impact before running

Given a diff to the app, Bruinet lists which page objects, locators and scenarios it is likely to break, before anything runs.
Taken from Launchable's predictive selection and Chromatic's TurboSnap.

- **How:** test selection's touch map read in reverse, plus a check of changed markup against each page object's locators.
- **Tool:** `impact <rev-range>` returns "these locators no longer match" and "these scenarios reach the changed code".
- **Use:** an agent that changes the app fixes the tests in the same change, instead of finding out in CI.

The agent learns about broken tests from one cheap call, not a full suite run.

## 10. Reproduce first, then fix

From a bug report, a recording or a video, an agent writes a failing scenario first, then fixes the app, then proves the
scenario goes green and nothing else turns red. Taken from SWE-bench practice and the "reproduction test" step in SWE-agent and
Copilot's coding agent.

- **Tools:** `repro_from_recording`, building on `search_recordings` and `resolve_actions`; the live session to confirm the bug.
- **Proof:** a red-then-green record: the scenario failing on the old commit, passing on the new, with the selected tests green.
- **Output:** the fix, the new regression scenario and its video in one handover (`review_page`).

Every agent fix arrives with a test that shows the bug and shows it gone.

## Order

The live session and structured failures come first, because every other proposal sends its results through them.

1. **Live session (1):** the inner loop everything else reuses.
2. **Structured failures (2):** what an agent reads when a step goes wrong.
3. **Element to source (3):** the bridge from test to app code.
4. **App telemetry per step (4):** fills in proposal 2's app side.
5. **Breakpoints (6):** the live session on a paused run.
6. **Put the app in a state (8):** shortens every loop above.
7. **Safe refactoring (7):** stands alone; builds on `find_step_usages`.
8. **Change impact (9):** needs test selection's touch map and the locator check.
9. **Reproduce first (10):** combines sessions, recordings and failures.
10. **Plan, generate and heal agents (5):** last, because they package all the tools above into roles.

Open question: should the live session (1) and the breakpoint run (6) share one long-lived host process per suite, as the MCP
server does now? That would keep a session's startup cost to once per conversation.
