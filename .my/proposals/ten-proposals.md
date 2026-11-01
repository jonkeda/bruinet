# Bruinet: ten proposals

2026-11-01

Start with three: the since-last-green failure diff, model-call replay and the step trace viewer. They cut the two biggest
costs of UI testing today: working out why a red test failed, and paying for runs that depend on slow or changing outside
services.

Each proposal builds on something Bruinet already records (page snapshots, SQL logs, touch maps, video), so none of them is a
new framework. Where a competitor already does it well, the proposal says so and takes the idea. Features Bruinet already has
(the MAUI accessibility audit, Playwright traces on the web head, test selection) are extended, not repeated.

## 1. Since-last-green failure diff

When a scenario fails, Bruinet compares it step by step with the last run where it passed, and names the first step that
differs. That step is usually where the cause is, even when the error is thrown later. Borrowed from Launchable's failure
analysis and Datadog's "new since" error view.

- **What it compares:** each step's page snapshot, its SQL statements, and the source files it touched, all of which Bruinet already captures.
- **What it says:** one paragraph, such as "At *When I tap Sync* the list showed 2 rows, not 3; a query on `Todo` now filters on `IsArchived`; `TodoStore.cs` changed since."
- **What it labels:** page changed (fix a page object), data changed (refresh the seed or recording), or timing only (snapshots match, durations differ).
- **Where it shows:** `bruinet why <test>`, an MCP tool for agents, and a line in `summary.md`.

Saves the most time of the ten, because diagnosis is where most of the time goes.

## 2. Step trace viewer on every head

A failed run opens in a timeline: every Given, When and Then, with its snapshot before and after, the control it drove, its
waits and its log lines. Taken from Playwright's Trace Viewer and Cypress's time-travel command log. Bruinet saves Playwright
traces on the web head today, but MAUI, WPF, WinForms, Android and bUnit have nothing like it.

- One trace format for every head, written per step into the existing `traces/` folder.
- Viewed in the Presenter, the VS Code extension, or as one HTML file that can be attached to a CI run.
- A scenario step links straight to its page object member, so the reader goes from "what happened" to "which code did it" in one click.

Together with proposal 1, this replaces most re-runs done just to watch a failure happen.

## 3. Self-healing locators, proposed as a fix

When a locator finds nothing, Bruinet searches the page for the element that most likely replaced it and offers a change to
the page object. It never quietly passes the test. Taken from Healenium, Testim and mabl, minus the part where the tool heals
silently and hides real bugs.

- Uses the Scraper's existing matching (`find_similar_elements`) against the last green snapshot of that control.
- Output is a patch to the page object, with the old and new locator and a confidence score, ready to review in a PR.
- The test still fails that run; it passes only after someone accepts the patch.

This keeps Bruinet's "page objects own the structure" rule and makes renamed IDs and moved buttons a one-click fix instead of
an hour of digging.

## 4. HTTP and model-call replay on WireMock.Net

Record every outgoing HTTP call once, including AI model calls, and replay the recorded answers in later runs. This is SQL
replay for the network. WireMock.Net does the recording and replaying; Bruinet adds a thin package with its conventions
around it, so this is an integration, not a new engine.

- **Engine:** WireMock.Net in proxy mode records each request and response as a mapping; in replay mode it serves those mappings.
- **Wiring:** the suite's test host points the app's base URLs at WireMock.Net by configuration (an Azure SDK client such as Foundry's changes its endpoint, it does not go through `IHttpClientFactory`).
- **Recordings:** one mapping set per scenario, beside the feature file or under `recordings/`, reviewable in a PR like SQL replay's.
- **Missing recording:** WireMock.Net's default 404 becomes a test failure that names the call.
- **Model-call matching:** a preset that matches on prompt and model name and ignores request ids, `temperature` and `seed`, using WireMock.Net's JSON body matchers.
- **Refresh:** `--refresh` switches to proxy mode, re-records, and shows what the model now says differently.
- **To check:** recording and replaying streamed model answers (server-sent events).

It makes runs cheaper (no token spend), faster (no 5-second waits on a model) and repeatable (the same story text each time).
For an AI-narrated app like Tomorrow, that matters a lot.

## 5. Visual snapshot review

A scenario step can say `Then the Map page should look as approved`, and Bruinet compares a screenshot with an approved image.
A small review page lets a person approve or reject each change. Taken from Chromatic, Percy and Verify's approval files.

- Masks regions that always change (clocks, generated art, model text) by naming the control in the page object, not by pixel boxes.
- Compares with a tolerance per control, so anti-aliasing doesn't fail a run.
- Approved images live in the repo next to the feature file, so a PR shows the visual change.

This catches layout and styling bugs that no assertion on text or state will find, which matters most for canvas screens such
as Tomorrow's board.

## 6. Flake detection with a cause

Bruinet keeps a history of results per scenario, and flags one that passed and failed on the same commit as flaky, with the
step where it varies. Taken from Datadog Test Optimization, Buildkite Test Engine and Gradle Develocity. Bruinet's docs already
say a flaky scenario is a bug, so this finds and explains flaky tests; it does not hide them in a quarantine list.

- History is a small file per suite next to `durations.json`, so no service is needed.
- The flake report lists the varying step, and runs proposal 1's diff between the passing and failing run.
- An optional repeat mode (`--repeat 20` on one scenario) proves a fix before it lands.

This turns "it fails sometimes" into a named step with evidence.

## 7. Sharding across machines

`bruinet shard 3/8` runs the third of eight balanced slices of a suite, so a CI matrix of eight jobs finishes in about an
eighth of the time. Taken from Playwright's `--shard`, Cypress Cloud's smart orchestration and Knapsack Pro.

- Balances on the timings Bruinet already keeps in `durations.json`, not on test counts.
- Merges the shards' `TestResults/` folders into one `summary.md`, one trace set and one touch map.
- Combines with test selection: select first, then shard what is left.

Wall-clock time drops and nothing new has to be written; cost stays the same or falls, because idle runners finish together.

## 8. Accessibility checks on every head

Every page a scenario visits gets an accessibility check, and `Then the page should be accessible` makes it a gate. Taken from
axe-core, Lighthouse and Android's Accessibility Test Framework. Today only the MAUI suite writes `accessibility-audit.md`.

- Web and bUnit heads run axe-core over the rendered DOM.
- Windows heads use UI Automation properties; Android uses its Accessibility Test Framework checks.
- Each finding names the page object and control, not a CSS path, so the fix is easy to find.
- A baseline file accepts today's findings, so only new problems fail a run.

It finds keyboard and screen-reader bugs without anyone writing a single extra test.

## 9. Mutation testing for scenarios

Bruinet changes what the page shows (hides a button, swaps a label, drops a list row) and checks that at least one scenario
notices. A change nobody catches marks a Then-step that asserts too little. Taken from Stryker, which does this for unit
tests, applied to the UI.

- Mutations happen in the bUnit and MauiHeadless heads, so each one costs milliseconds, not a browser run.
- The touch map limits each mutation to the scenarios that reach that control.
- The report lists the controls no scenario checks, ranked by how many steps use them.

It answers "do these tests prove anything?" with a number, and shows where to add the one missing Then.

## 10. Agent-run exploratory testing

An agent drives the app only through existing page objects, trying paths no scenario covers, and writes every bug it finds as
a draft `*.feature.md` that reproduces it. Taken from Meticulous, QA Wolf and Microsoft's model-based testing, but kept inside
Bruinet's rule that tests never touch the driver.

- Uses the MCP server's existing tools (`list_steps`, `draft_test`, `run_test`) as its only way into the app.
- Looks for crashes, error pages, console errors, broken accessibility and steps that hang.
- Each finding comes with its draft scenario, trace and video, so a person only decides whether to keep it.
- A budget of steps and minutes keeps the cost fixed per run, for example one nightly run.

It finds the bugs between the scenarios people thought to write, and turns them into regression tests.

## Order

Build the step trace first, because four other proposals read it. After that, each step builds on the ones before it.

1. **Step trace viewer (2):** the per-step record that 1, 3, 6 and 10 read.
2. **Since-last-green diff (1):** the biggest time saver, once traces exist.
3. **HTTP and model-call replay (4):** stands alone, and is the biggest cost saver for AI apps.
4. **Sharding (7):** small, and uses timings Bruinet already has.
5. **Flake detection (6):** history plus the diff from step 2.
6. **Self-healing locators (3):** the Scraper's matching against green snapshots.
7. **Accessibility on every head (8):** one check per head, with a baseline.
8. **Visual snapshot review (5):** needs a review page and masking per control.
9. **Mutation testing (9):** needs the headless heads and the touch map to be cheap.
10. **Agent-run exploration (10):** last, because it uses everything above to report what it finds.

Open question: should the replay and trace formats be shared with SQL replay, as one recording per run? That would be the
simpler design.
