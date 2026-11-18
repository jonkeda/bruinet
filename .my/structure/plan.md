# Plan: one product, Bruinet, in four modules

Status: **in progress**: steps 0 to 5 done (2026-11-18). Date: 2026-11-18. Carries out the decision in [one-or-four](one-or-four.md):
Lirenet, Frainet and Gaminet become the modules **Spec**, **Develop** and **Migrate** of Bruinet, beside
**Test** (what Bruinet is today). What goes in each module: [what-goes-where](../parts/what-goes-where.md).

## 1. Where it ends

```text
test-tooling/                         one repository, one solution (Bruinet.sln), one build setup, one release
  src/
    Bruinet.Core, Bruinet.Html, …     Test: names unchanged (Q5)
    Bruinet.Spec.Format               Spec: reads a spec into a model; references nothing
    Bruinet.Spec                      Spec: check, trace; references Bruinet.Spec.Format
  tests/
    Bruinet.Spec.Tests                Lirenet.Specs.Tests, moved
    Bruinet.Architecture.Tests        the arrows between modules (or in Bruinet.Core.Tests)
  tools/Bruinet.Cli                   one CLI: bruinet …, bruinet spec check, bruinet spec trace
  docs/spec/spec-format.md            the format, for users
  .my/spec/design/                    Lirenet/.my/design
  .my/develop/                        Frainet/.my (loops, Tiny, specs, plan, questions)
  .my/migrate/                        Gaminet's notes
```

Gone: the folders `Lirenet/`, `Frainet/`, `Gaminet/`, their solutions, `Directory.*.props`, `global.json` and
CLIs; the commands `lirenet` and `frainet`; Frainet's forwarder; the `SeparateProducts` exemption in
`tests/Bruinet.Core.Tests/SolutionTests.cs` and the one in `Quality.props`; the second spec reader.

Not built: `Bruinet.Develop`, `Bruinet.Migrate`, `Bruinet.Spec.Xunit`, Spec tools in the MCP server. Each
comes with the first case that needs it (small-steps P5).

## 2. Rules for the work

- **Each step is one commit** on the working branch (`move`), and leaves the build green and every test passing.
- **A move is checked by the same results, not more tests.** Before a step, record what it must not change;
  after it, compare. The records below are made in step 0.
- **A new check must have failed once** (small-steps P3): the architecture test is shown red on a planted
  reference before it is trusted.
- **Another session is editing** in this repository (the Scraper, `docs/run/cli.md`,
  `Lirenet/.my/design/330-Salt-vs-Wireframe.md`, skills). Steps that move those files wait until that work is
  committed; nothing here reverts or rewrites it.
- **No backward compatibility.** Anything may change: names, namespaces, commands, arguments, file layout. No
  aliases, no old command kept for a release, no second reader, no `api-upgrade` rows for this work. Where
  something is used, its users are switched in the same commit; a repository that uses Bruinet is fixed when
  it moves to the new version.
- The baseline of step 0 is for **checking the moves**, not for keeping the old behaviour: once a step is
  done, its later improvements may change the output freely.

## 3. The steps

### Step 0: the baseline

Record what the moves must keep:

| Record | How |
| --- | --- |
| Lirenet's tests | `dotnet test Lirenet/Lirenet.slnx` → names and outcomes |
| `lirenet check` on every spec | Bouw7's 30 specs, Todo's spec, samples: output and exit code per file |
| `lirenet trace` on Bouw7 | For each module with a spec: the trace's output, with and without `--runnable` |
| The spec runner | The scenarios Bruinet discovers from `*.spec.md` (names and tags), for Todo and Bouw7 |
| Bruinet's build and tests | `dotnet build Bruinet.sln`, the core and Bdd test projects |

Written outside the repository (the session's scratch folder), with the old `lirenet` built from before step 1 kept
beside them, so every later step compares against the same records. **Done when** the records exist and a rerun gives
the same.

### Step 1: Spec into the solution

- Move `Lirenet/src/Lirenet.Specs` to `src/Bruinet.Spec`, `Lirenet/tests/Lirenet.Specs.Tests` to
  `tests/Bruinet.Spec.Tests`; namespaces `Lirenet.Specs` → `Bruinet.Spec`. Add both to `Bruinet.sln`.
- They now build under Bruinet's `Directory.Build.props` and `Quality.props`: analyzers, code style, a declared
  public API (`PublicAPI.*.txt`), package settings. Fix what they raise; no blanket suppressions.
- `Lirenet.Cli` stays for now, pointed at the moved library, so step 0's command records can be compared.

**Done when** the build is green, `Bruinet.Spec.Tests` gives step 0's names and outcomes, and `lirenet check`
and `lirenet trace` give step 0's records.

### Step 2: `bruinet spec`

- Add the command group `spec` to `tools/Bruinet.Cli` (AD-028: one command line): `bruinet spec check`,
  `bruinet spec trace`. Same arguments and exit codes as `lirenet` in this step, so the baseline compares;
  better arguments are a later change, free to break.
- Switch every user in the same commit: `tools/Scripts/site-loop.ps1` (builds and runs Lirenet today), the
  skills `spec-from-app` and `scenario-from-spec` (in `.github/skills` and `.claude/skills`), `AGENTS.md`,
  `docs/guides/runnable-specs.md`, `docs/run/cli.md`, `src/Bruinet.Traits/BruinetTraitDiscoverer.cs` (a
  summary names `lirenet trace`).
- Delete `Lirenet/src/Lirenet.Cli` and `Frainet/src/Frainet.Cli` (the forwarder).

**Done when** `bruinet spec check` and `bruinet spec trace` give step 0's records, and `site-loop.ps1` on
Bouw7 reports the same gates and states as its last run before the step.

### Step 3: the arrows

- An architecture test: no project references Develop or Migrate (none exist yet; the test is ready for
  them); `Bruinet.Spec.Format` and `Bruinet.Spec` reference no `Bruinet.*` project outside Spec; among Spec's
  projects, Test's projects reference only `Bruinet.Spec.Format`.
- Planted fault: add a reference from `Bruinet.Spec` to `Bruinet.Core`; the test fails, naming it; remove it.
- Remove the `SeparateProducts` exemption from `SolutionTests` and the Frainet/Lirenet/Gaminet remark from
  `Quality.props`: **in step 5**, when the last projects under those folders are gone; until then they would
  fail `EveryProjectInTheRepository_IsInBruinetSln`.

**Done when** the test is green, was red on the planted reference, and `SolutionTests` passes without the
exemption.

### Step 4: one spec reader

- Split `Bruinet.Spec.Format` out of `Bruinet.Spec`: the reading of a spec file into a model (title, sections,
  rules with ids, examples, chapters, background). `SpecCheck` checks the model instead of reading lines itself.
- `Bruinet.Bdd`'s `BddSpecReader` reads through `Bruinet.Spec.Format` and keeps only the turning of the model
  into a feature.
- Rule check (step 3's test): `Bruinet.Bdd` → `Bruinet.Spec.Format` is the only reference from Test into Spec.

**Done when** step 0's check and trace records and the discovered scenarios are the same, and the Bdd and
runnable-spec tests pass. This is the step most likely to show a difference; a difference is a finding of
one of the two old readers, decided one by one, never smoothed over.

### Step 5: Develop and Migrate as folders

- Move `Frainet/.my` to `.my/develop/` and Gaminet's README and docs to `.my/migrate/`; delete
  `Gaminet/src/Gaminet.Cli` and the `Frainet/`, `Gaminet/` folders with their solutions and build files.
- Each gets a README at the top of its `.my` folder: what the module is for, that it has no code yet, and the
  case that will start it.

**Done when** the folders are gone, nothing links to them (a link check over `docs/`, `.docs/`, `.my/`,
skills), and the build and tests are as before.

### Step 6: Spec's notes and docs

- Move `Lirenet/.my/design` to `.my/spec/design/` (after the other session's edits there are committed) and
  `Lirenet/docs/spec-format.md` to `docs/spec/spec-format.md`; delete `Lirenet/`.
- Words: "Lirenet spec" → "spec", `lirenet` → `bruinet spec`, Lirenet / Frainet / Gaminet → Spec / Develop /
  Migrate in `docs/`, `.docs/` (AD-033, AD-041 get a dated line, not a rewrite), skills, `AGENTS.md`. The spec files
  themselves do not change: their title is already `# Spec: <id> …` and the extension stays `*.spec.md`.
- `docs/README.md`: the module list. The root `README.md`: the four modules first, a line each, and which
  packages a spec user takes.

**Done when** a search for `lirenet|frainet|gaminet` outside `.my/`, `CHANGELOG.md` and dated decision lines
finds nothing, and the link check passes.

### Step 7: the container

- Bouw7's live documents: `.testing/150-Structure.md`, `200-Tasklist.md`, `400-FrictionLog.md`,
  `500-Harvest.md`. Its specs need no change. Run reports under
  `.testing/runs/` are history and stay as written.
- The container's proposals (`.my/lifecycle`, `.my/lirenet`, `.my/ears`, `.my/steps`, `.my/structure`,
  `.my/bouw7`, `.my/Planning`): a "Names since" row at the top, as the EARS proposal has, not a rewrite.
  `.my/structure/folder-structure-proposal.md` is marked superseded by this plan, except its record folders
  `.developer/` and `.migrator/`, which stay (they match Develop and Migrate).

**Done when** Bouw7's `site-loop.ps1` run gives the same gates and states as step 2's, and the container's
live documents name only the modules.

### Step 8: the release and the close

- The release (`release.yml`, `tools/Scripts/pack.ps1`) packs every packable project under `src/`, so
  `Bruinet.Spec.Format` and `Bruinet.Spec` are packed and proven by `check-packages.ps1` with no workflow
  change; check it on a CI run. `nuget.config`'s `Bruinet.*` mapping already covers them.
- `CHANGELOG.md`: the modules, `bruinet spec`, the end of `lirenet` and `frainet`.
- A decision record (skill `decision-record`): one product, four modules, references one way, no code ahead
  of a case. It rules out separate products until a trigger of [one-or-four](one-or-four.md) section 5.
- The `docs-upkeep` checklist; then extract and delete this topic and `.my/parts` (AGENTS.md: when the work is
  done, extract, then delete), and the baseline records.

**Done when** CI is green with the Spec packages packed and checked, the decision is recorded, and the topic
is gone.

## 4. Progress

| Step | State | Notes |
| --- | --- | --- |
| 0 | Done | 31 specs: `check` passes all; `trace` with and without `--runnable`; 40 tests |
| 1 | Done | `src/Bruinet.Spec`, `tests/Bruinet.Spec.Tests`, solution folder `7 Spec`, in `coverage.ps1`. Bruinet's analyzers raised only the undeclared public API (59 lines in `PublicAPI.Unshipped.txt`). Check, trace and tests identical to step 0. `Lirenet.Cli` still builds, on the moved library |
| 3 | Done | `tests/Bruinet.Core.Tests/ModuleTests.cs`; red on a planted `Bruinet.Spec → Bruinet.Core`, green without. The exemption moves to step 5 |
| 2 | Done | `tools/Bruinet.Cli/Spec/SpecCommands.cs`, tests in `tests/Bruinet.Cli.Tests/Spec`. Check and trace identical to step 0 on all 31 specs; `trace --runnable --results` identical to the old `lirenet` on all 73 saved Bouw7 runs (with real passing states). `site-loop.ps1` builds and calls `bruinet`; it was not run end to end, since that runs Bouw7's tests on acc and rewrites its loop file. Done early, from step 6: `docs/spec/spec-format.md` (the guide's rows link to it) and the old README folded into `src/Bruinet.Spec/README.md`. Gone: `Lirenet.Cli`, `Frainet.Cli`, and both folders' solutions and build files; left: `Lirenet/.my`, `Frainet/.my`, `Frainet/README.md` |
| 4 | Done | `src/Bruinet.Spec.Format`: `SpecReader.Read` gives a `SpecDocument` (title, header, sections, rules table, chapters and examples, each with its line). `SpecCheck`, `RuleTrace.SpecAsSource` and `BddSpecReader` read through it; `Bruinet.Bdd` references it, the only reference from Test into Spec. Check, trace, `--results` on 73 runs, and the runner's 181 scenarios of the 31 specs are identical. Where the two old readers differed, the format decides, pinned in `SpecReaderTests`: the title is the first line; `Chapter:` as written; a `####` example only in a chapter; a heading inside a fence (`` ``` `` or `~~~`) is text. No spec in Bouw7 or Todo met any of these |
| 5 | Done | `Frainet/.my` is `.my/develop/`, Gaminet's notes `.my/migrate/`, each with a README saying what the module is and that it has no code yet. `Frainet/` and `Gaminet/` are gone; the `SeparateProducts` exemption, the `Quality.props` remark and AD-033's line are updated. No link points into the old folders |
| 6–8 | Open | |

Found on the way, not part of this plan: three `SolutionTests` fail before and after step 1. The iOS sample is not in
`Bruinet.sln`, the iOS test projects are in no script, and `MediaElement.tpl.cs:42` names a platform.

## 5. Order and size

| Step | Depends on | Size |
| --- | --- | --- |
| 0 Baseline | | An hour |
| 1 Spec into the solution | 0 | Half a day: the analyzers and the public API are the work |
| 2 `bruinet spec` | 1 | Half a day |
| 3 The arrows | 1 | An hour or two |
| 4 One spec reader | 1, 3 | A day; the real change |
| 5 Develop and Migrate as folders | 2 (the forwarder gone) | An hour |
| 6 Spec's notes and docs | 2, the other session's commits | Half a day |
| 7 The container | 2, 6 | An hour or two |
| 8 Release and close | all | Half a day, mostly waiting for CI |

Steps 3 and 5 can run beside 2; everything else goes in order. Steps 1 to 3 and 5 are mechanical; step 4 is
the only one that changes how something works.

## 6. Risks

| Risk | Answer |
| --- | --- |
| Lirenet's code fails Bruinet's analyzers and public-API checks in many places | Expected; fix in step 1, which is sized for it. A rule that does not fit is turned off in `Quality.globalconfig` with why, as for the rest |
| The two readers disagree on a spec | Step 4 decides each difference; Bouw7's 30 specs and Todo's are the test set |
| The other session's work overlaps the moved files | Steps 2 and 6 wait for its commits; check `git status` before each step |
| A link or skill still says `lirenet` and an agent runs a command that is gone | Step 6's search; the CLI could name the new command for an unknown `lirenet`-style call, but there is no `lirenet` binary left to call, so a search is enough |
| A spec user reads "Bruinet" as a UI test tool | The root README and the package description name the modules first (step 6) |

## 7. Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | The architecture test: its own project or in `Bruinet.Core.Tests` beside `SolutionTests`? | Beside `SolutionTests`: it reads project files the same way |
| Q2 | Do the spec files change? | No: the title is `# Spec: <id> …` already and `*.spec.md` stays; only the words around them in docs and skills |
| Q3 | Does `.my/spec/design` keep its numbering (100-Questions … 500-Mixed, automation/)? | Yes, moved as it is |
| Q4 | Start now, or after the other session's Scraper work is committed? | Steps 0, 1 and 3 now (they touch none of its files); the rest after |
| Q5 | Do Test's packages get a module prefix (`Bruinet.Test.Html`)? | No. Compatibility is not the reason: `Bruinet.Html` already says what it is, and a prefix on 46 packages is churn in every suite's code and docs for no reader's gain |
