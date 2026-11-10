# Closing the gaps a BDD user looks for

Status: **design**, 2026-10-30. D3 built (`docs/guides/coming-from-reqnroll.md`). **D1 parked**
(2026-10-30, the owner): worth its week only when someone names the tool that will read the
output (Construction's team, an Exact dashboard); its spike then tests that tool, not Cucumber's
HTML formatter. D2 built (2026-10-30; a missing table stays `UAT017`, no `UAT033`; its grammar recorded as AD-036 on 2026-11-10). Item 6 of [improvements](../improvements/README.md),
built on [the Reqnroll comparison](../uat/reqnroll-comparison.md) of 2026-09-27, checked against the
tree at 3ec7549b.

## Outcome

Someone coming from Reqnroll or SpecFlow checks five things in the first hour: hooks, scenario
outlines, table comparison, a report their dashboard reads, and how their `.feature` files map
over. Bruinet now has the first three, documented, and not the last two. This design adds the
missing two, plus one small grammar gap:

- **D1** Cucumber Messages from a run, so the Cucumber HTML report, Cucumber Reports and any tool
  that reads Messages show Bruinet's results.
- **D2** Several named, tagged Examples blocks in one outline.
- **D3** A "Coming from Reqnroll" page: what maps, what differs, what is absent and why.

D3 costs an hour, D2 a day, D1 about a week, after a one-day spike that tests the one real risk:
whether Cucumber's tools accept Bruinet's Markdown as the source.

## What a BDD user finds today

| Looks for | Today (checked 2026-10-30) | Here |
| --- | --- | --- |
| Hooks per scenario, per step, by tag | `[BddBeforeFeature]`, `[BddBeforeScenario(tags)]`, `[BddAfterScenario(tags)]`, `[BddAfterStep(tags)]` (`src/Bruinet.Bdd/BddHooks.cs`), in `docs/guides/uat-step-definitions.md` | built; no `BeforeStep` or run hooks (an xUnit fixture is the run's scope), named in D3 |
| Scenario Outline | `## Scenario Outline:` with one or more `### Examples: <name>` sections, tagged per section (`BddMarkdownParser`) | built (D2, AD-036) |
| Set and instance comparison with a diff | `BddSets.AssertSet`, `BddTable.AssertExactly`, `AssertContains`, `AssertDoesNotContain`, documented | built |
| Cucumber Messages, an HTML report | a `<feature>__<scenario>__result.json` per scenario, `manifest.json` and `summary.md` per run (`UatScenarioReport`, `TestArtifactManifestWriter`) | **D1** |
| A map from Reqnroll's words | `docs/guides/bdd-glossary.md`: the words only | **D3** |
| Keywords in Dutch | English keywords only | not now: waits for Construction's testers (R3) |
| Importing `.feature` files | none | not now: on demand (R16) |
| Parallel runs | collections dealt by duration (test-selection next-plan, item 1) | built |

## D1. Cucumber Messages from a run

### Shape

A converter over a finished run folder, not a writer inside the run:

```text
bruinet report messages <run-folder> [--suite <name>] [--out <file.ndjson>] [--embed]
```

- It reads `manifest.json`, every `__result.json` it lists, the feature files those name, and the
  evidence files beside them; it writes one NDJSON file per suite and head
  (`<run-folder>/<suite>.<head>.ndjson` by default).
- **Why a converter:** nothing marks the end of a run (the manifest is rewritten at every artifact),
  `testRunFinished` needs one, and the xUnit gate, the runner process (Presenter, IDEs) and the
  narrator all write the same reports. A converter serves all three, and CI runs it after
  `dotnet test`.
- **Where:** `src/Bruinet.Bdd.Workspace/Reports/CucumberMessages/` (Workspace already loads a suite and
  parses its files for the preview), the command in `tools/Bruinet.Cli`, an MCP tool
  `report_messages` beside `review_page`.
- **Types:** the `Cucumber.Messages` NuGet package (netstandard2.0, no dependencies; 28.x as of
  2026-10), pinned in `Directory.Packages.props`. Its protocol version goes in `meta`.

### What the run must add first

`__result.json` has each step's line, status and message, but no times. Messages needs a timestamp
on every start and finish. Additive fields, written always:

| Where | Field | From |
| --- | --- | --- |
| report | `startedAtUtc` | the scenario's start in `UatScenarioResults.Record`'s caller |
| report | `examplesRow` (outline rows only) | the row's block and index, for D2's ids |
| step | `startedAtUtc`, `durationMs` | timed in `BddExecution` around each step; `BddStepResult` gains `Started` and `Duration` |
| manifest suite | `configDirectory` | the runtime's config folder, so a report's relative `file` resolves |

A run written before these fields converts with times taken from the manifest record and spread
evenly over the steps; the command prints a warning saying so.

### Mapping

| Envelope | Built from |
| --- | --- |
| `meta` | `implementation` Bruinet and the CLI's version; `runtime`, `os`, `cpu` from the machine; `ci` from `GITHUB_ACTIONS` or `TF_BUILD` and their run URLs |
| `source` | the feature file's text, `mediaType` `text/x.cucumber.gherkin+markdown`, `uri` relative to the repository root |
| `gherkinDocument` | `BddMarkdownParser`'s model: feature, prose as description, tags, background, scenarios, outlines with their Examples; ids stable (`<uri>:<line>`) |
| `pickle` | one per scenario as it ran (an outline row is its own, `<name> [n]`); steps after placeholders, a step table as `dataTable`; tags with `@`; `astNodeIds` to scenario and row |
| `stepDefinition` | only with the runtime trace on: one per `commandId`, `pattern` the phrase as `CUCUMBER_EXPRESSION`, `sourceReference` its class and method |
| `testRunStarted`, `testRunFinished` | earliest start, latest end; `success` when no scenario failed |
| `testCase`, `testStep` | one per report, one step per pickle step (`pickleStepId`; `stepDefinitionIds` when known) |
| `testCaseStarted` / `Finished`, `testStepStarted` / `Finished` | the new times; `attempt` 0, `willBeRetried` false (AD-009: no retries) |
| `attachment` / `externalAttachment` | the evidence (screenshot on failure, a recording) on the failed step: a path relative to the NDJSON file, or the bytes in base64 with `--embed` |

Status: `Passed` PASSED, `Failed` FAILED, `Skipped` SKIPPED (the scenario's skip reason as the
first step's message), `Canceled` SKIPPED with the message "canceled", a step never reached after a
failure SKIPPED, a bind error (no report today) UNDEFINED once the bind writes one.

Not carried: the head and the subject other than as tags (`@head:web` added to each pickle), a
callable step's inner calls (one pickle step per written step; the call chain in the message on
failure), the hooks (the result does not record them).

### The risk, tested first

Cucumber's own Markdown dialect (MDG) is not Bruinet's: steps there are list items, here gherkin
blocks under `##` headings. A tool that re-parses `source` instead of reading `gherkinDocument`
would fail. The spike settles it before any code:

1. Write by hand the NDJSON for TOD.02's run (one passed, one failed with a screenshot).
2. Render it with `npx @cucumber/html-formatter` and open it in Cucumber Reports' viewer.
3. Passes when both show the scenarios, steps, tables, the failure's message and the screenshot.
4. If one re-parses `source`: send a plain-Gherkin rendering of the file instead
   (`text/x.cucumber.gherkin+plain`, lines mapped back in `gherkinDocument`), and record why.

### Checks

| Test | Where | Proves |
| --- | --- | --- |
| A golden NDJSON for a recorded Todo web run on bUnit | `tests/Bruinet.Bdd.Workspace.Tests/Reports/` | the mapping, stable ids, order of envelopes |
| Every envelope validates against the package's JSON schema | same | the protocol |
| Outline rows, a skipped scenario, a canceled run, a run without the new fields | same | the edges in the table above |
| `bruinet report messages` on a run folder writes one file per suite and head | `tests/Bruinet.Cli.Tests` | the command |
| CI: the Html suite's run converted and uploaded as an artifact | `.github/workflows/ci.yml` | it works where users look |

## D2. Several Examples blocks

Grammar change in `docs/specs/uat-grammar.md`:

~~~markdown
## Scenario Outline: TOD.03.2 Titles that are refused

```gherkin
When I set Title to "<title>"
Then Title Error should show "<error>"
```

@maui @web
### Examples: empty and blank

| title | error |
| --- | --- |
| | A title is required |

@web
### Examples: refused by the server

| title | error |
| --- | --- |
| Already taken | A todo with this title exists |
~~~

- An outline has one or more `### Examples` sections; `### Examples: <name>` names one, and the
  bare `### Examples` stays valid.
- Tags on the line above an Examples heading join the outline's tags for that block's rows, so a
  head tag picks rows for a head.
- Rows are numbered across blocks, `<name> [n]` as today, so addresses, filters, touch maps and
  durations keep their names; the block's name is in the report (`examplesRow`) and the preview,
  not in the scenario's name.
- Every block needs every placeholder's column (`UAT015` per block). New: `UAT032` two blocks with
  the same name. (Built: an Examples section with no table is the existing `UAT017`, so no `UAT033`.)

Changes: `BddMarkdownParser.ParseExamplesTable` becomes a loop; the outline model keeps its blocks
(name, tags, table, line); `ExpandOutline` numbers across them; the preview renders each block with
its name and tags; the language server folds and outlines each; the grammar page and
`uat-template-guide.md`. Tests in `tests/Bruinet.Bdd.Tests` (parse, tags per block, numbering,
`UAT015` per block, `UAT032`, `UAT017` for a block without a table), a preview snapshot in `Bruinet.Bdd.Workspace.Tests`, and a
Todo outline split in two blocks run on both web heads.

## D3. Coming from Reqnroll

`docs/guides/coming-from-reqnroll.md`, linked from `docs/README.md`, the glossary and the README's
writing guides:

- The concept map: the glossary's rows plus attributes (`[Binding]` to `[BddStepDefinitions]`,
  `[Given]` to `[BddGiven]`, `[BeforeScenario("tag")]` to `[BddBeforeScenario("tag")]`,
  `Table.CompareToSet` to `table.AssertExactly`).
- One TOD scenario in both forms, `.feature` beside `.feature.md`.
- What is different and why: binding before run, one spelling per value (AD-016), control verbs
  from page objects, no regex or optional text (AD-016), xUnit only (AD-006).
- What is absent and why: retries (AD-009), tag-scoped bindings (AD-019), `BeforeStep` and run hooks
  (the fixture), Dutch keywords (waiting), an importer (on demand); Cucumber Messages pointing at
  D1 once built.

## Order

1. D3, so a Reqnroll user finds today's answers now: done 2026-10-30.
2. D2: done 2026-10-30 (TOD.02.3 split into two named blocks, both web heads green).
3. D1, parked: when a reader of its output is named, the spike against that reader, then the run
   fields (times, `examplesRow`, `configDirectory`), the converter, the command, the MCP tool, CI.
   Many dashboards (Jenkins' Cucumber reports, Xray) read Cucumber's older JSON rather than
   Messages, and Azure DevOps reads neither (xUnit's TRX already reaches it): the named reader
   decides the format.

Each step is its own commit on the working branch, with `docs/` and the CHANGELOG in the same
commit; an AD at D1's end (reports are Messages, made after the run) and D2's end (the grammar).

## Open questions, with proposals

| # | Question | Proposed | Why |
| --- | --- | --- | --- |
| Q1 | The `Cucumber.Messages` package or our own writer | the package | the protocol's own types and version; no dependencies; a writer of our own drifts from the schema |
| Q2 | Converter after the run, or a writer in it | converter | no run end to hook; serves the gate, the runner and the narrator alike |
| Q3 | Source as Bruinet's Markdown or as plain Gherkin | Markdown, unless the spike fails | real line numbers into the file the user edits |
| Q4 | `Canceled` as SKIPPED or UNKNOWN | SKIPPED, with "canceled" | report tools count UNKNOWN as broken |
| Q5 | An HTML report of our own | no: Cucumber's html-formatter over D1's output, documented | one rendering, maintained by Cucumber; Reqnroll ships the same |
| Q6 | An Examples block's name in the scenario's name | no | addresses, filters, touch maps and durations keep working |
