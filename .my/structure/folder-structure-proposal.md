# Proposal: the folder structure for the four parts and their projects

Status: **proposal**, for discussion. Date: 2026-11-16. **Steps 1 to 4 of section 7 are done (2026-11-16)**: Lirenet
exists, `frainet check` and `frainet trace` forward to it, the scripts, skills and docs name `lirenet`, and Todo's
spec is in `samples/Todo/specs/`.

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with. **Superseded** by that decision: the parts are no longer folders of their own with their own solutions. Its record folders stay: `.developer/` for a development project and `.migrator/` for a migration project, which match Develop and Migrate.

| | |
| --- | --- |
| What | Where **Bruinet**, **Lirenet**, **Gaminet** and **Frainet** live, and where a project keeps the record of its loops |
| Builds on | `.my/lifecycle/lifecycle-proposal.md` (the four parts), `.my/lirenet/lirenet-proposal.md`, `.my/steps/small-steps-proposal.md` (section 7, the record folder) |
| The short version | **Lirenet** and **Gaminet** become folders beside **Frainet**, each a tool of its own with the same layout. A project's record folder is named after the kind of project: **`.developer`** for a development project, **`.migrator`** for a migration project. The name `.factory` is dropped |

## 1. The tools: three folders of the same shape

Frainet is a folder in Bruinet's repository (`test-tooling/Frainet/`) with its own solution,
sources, tests and docs. Lirenet and Gaminet get the same, beside it:

```text
test-tooling/                     Bruinet's repository
  src/  tests/  samples/  docs/   Bruinet itself, as now
  tools/Scripts/                  site-loop.ps1 and the other scripts, as now
  Frainet/                        the factory: runs the loops and the agents
  Lirenet/                        the spec tool: describe, check, trace          NEW
  Gaminet/                        the migrator: inventory, rules, ledger         NEW
```

Each of the three has the layout Frainet has today:

```text
<Tool>/
  README.md                 what it is, how to run it
  <Tool>.slnx               its own solution
  Directory.Build.props     its own build settings
  Directory.Packages.props
  global.json
  src/<Tool>.Cli/           the command line tool (`lirenet`, `gaminet`, `frainet`)
  src/<Tool>.<Part>/        its libraries
  tests/<Tool>.<Part>.Tests/
  docs/                     its formats and guides
  AGENTS.md                 what it is and which skills it has, for an agent (section 4)
  skills/                   its skills (section 4)
  .my/                      working notes: plan, brainstorm, questions
```

A tool depends on another only through files and commands, as the lifecycle proposal says:
Lirenet on nothing, Bruinet on nothing, Gaminet on Lirenet and Bruinet, Frainet on all three.
No project reference crosses a tool's folder.

## 2. What goes in each tool

| Folder | Holds | Comes from |
| --- | --- | --- |
| `Lirenet/src/Lirenet.Specs` | The spec format, `check`, `trace`, the rule results | `Frainet/src/Frainet.Specs`, moved and renamed |
| `Lirenet/src/Lirenet.Cli` | `lirenet check`, `lirenet trace` | `Frainet/src/Frainet.Cli`, moved and renamed |
| `Lirenet/tests/Lirenet.Specs.Tests` | The tests of the above, with the broken copies | `Frainet/tests/Frainet.Specs.Tests` |
| `Lirenet/docs/spec-format.md` | The spec format | `Frainet/docs/spec-format.md` |
| `Gaminet/src/Gaminet.Cli` | `gaminet` | New |
| `Gaminet/src/Gaminet.Ledger` | The inventory and the ledger | New |
| `Gaminet/src/Gaminet.Rules` | Transform rules and packs | New |
| `Gaminet/docs/` | The formats: inventory, architecture description, transform rule, ledger | New |
| `Frainet/src/Frainet.Loops` | The step loop, the measures, the benchmark runner | New; today's site loop is `tools/Scripts/site-loop.ps1` |
| `Frainet/src/Frainet.Cli` | `frainet step`, `frainet retro`, `frainet benchmark` | New, after the spec commands leave |
| `Frainet/docs/` | The step file format, the measures, the loops | New |

After the move Frainet holds no spec code: the name stays with the factory.

Everything that names `frainet check` or `frainet trace` changes with it: `site-loop.ps1`, the
skills `spec-from-app` and `scenario-from-spec`, `docs/guides/runnable-specs.md`, AD-041 and
Bouw7's `AGENTS.md`. For one release `frainet check` and `frainet trace` stay as commands that
call Lirenet and say so.

## 3. Where specs live

A product's specs are in the product's own repository, not in a tool's folder:

| Specs of | Today | Proposed |
| --- | --- | --- |
| Bouw7 | `Bouw7/specs/` | Stays |
| The Todo sample | `Frainet/specs/todo/` | `samples/Todo/specs/`, with the sample they describe; Lirenet's tests read them from there |
| A tool's own features (P6: the tools build the tools) | Nowhere | `<Tool>/specs/`, one folder per tool |

## 4. Skills, agents and scripts: with their tool

Today the skills are in `test-tooling/.github/skills/` (18, read by Copilot), with a stub per
skill in `test-tooling/.claude/skills/` for Claude Code, and a list in `AGENTS.md`. A product
like Bouw7 has none of its own: it uses them because `test-tooling` is beside it.

A skill is part of its tool, like its docs: it changes when the tool changes, and moves with the
tool if it ever gets a repository of its own. So the skill lives in the tool's folder, and a stub
at the place agents look points to it:

```text
test-tooling/
  .github/skills/     Bruinet's skills, and a stub for every skill of the other three
  .claude/skills/     a stub for every skill of all four tools
  AGENTS.md           the list of all of them
  Lirenet/
    AGENTS.md         what Lirenet is, its skills
    skills/           spec-from-app; later describe-product, ears-rule
  Gaminet/
    AGENTS.md
    skills/           inventory, transform-rule, convert-unit
  Frainet/
    AGENTS.md
    skills/           run-step, retro, friction-log, benchmark
    agents/           builder, reviewer, retro
    scripts/          site-loop.ps1, once the loop moves
```

A stub is a few lines: the skill's name and description, and "read
`<Tool>/skills/<name>/SKILL.md`". The stubs stay at the root because most skills are used
somewhere else than in their tool's folder (on Bouw7, on Todo): Claude Code reads a nested
`<Tool>/.claude/skills/` only while it works on files under that folder.

A skill belongs to the tool whose output it makes:

| Skill | Owner |
| --- | --- |
| `spec-from-app` | Lirenet |
| `scenario-from-spec`, `run-triage`, `site-setup`, `scrape-pages`, the control and UI-test skills, `convert-control`, `narrated-review`, `api-upgrade` | Bruinet |
| `friction-log` | Frainet |
| `ai-item`, `decision-record`, `docs-upkeep` | Bruinet's repository: they are about working on the repository itself |

The same holds for the rest of what an agent needs from a tool:

| What | Where |
| --- | --- |
| Agent definitions (builder, reviewer, retro) | `Frainet/agents/`, with a stub in `.claude/agents/` |
| Scripts that run a loop | `Frainet/scripts/` |
| Formats and guides | `<Tool>/docs/` |
| What the tool is, in a page | `<Tool>/AGENTS.md`, so an agent that opens only that folder can start |

**Not solved here:** a product repository on its own (Bouw7 without `test-tooling` beside it, a
migration project) sees none of these skills. The answer is a plugin per tool that the product
installs; it waits until a project outside this container needs it.

## 5. The record folder: `.developer` or `.migrator`

The small-steps proposal called the record of the loops `.factory/`. It is named after what the
project is instead, so the folder says at once which loop runs there:

| Kind of project | Folder | Example |
| --- | --- | --- |
| Development: building or changing a product | **`.developer/`** | Kofflard and Tomorrow, where it is tried first; the tools themselves; Todo's new journey (slice 3) |
| Migration: converting a product to another architecture | **`.migrator/`** | Case 3 |

**Where `.developer/` is tried: Kofflard and Tomorrow.** Both are products of ours, real, under
development, with their source and with Bruinet suites that run without a device (Kofflard on the
headless head; Tomorrow's app on the headless head and its Studio on bUnit). That makes them the
place to find out what the folder must hold before the tools get one: Kofflard is small, so the
folder starts there; Tomorrow is larger and has two apps, so it shows whether one record serves
a product with several parts.

A project has one of the two, at the root of the product or suite it belongs to. Both have the
same core, so a person and an agent find the same files in every project:

```text
.developer/   or   .migrator/
  000-Readme.md         what this is, how to run a step
  100-Loops.md          the state of each slice and case; what waits for a person
  200-Steps/            one file per step: what, the check, the planted fault, the result
  300-Measures.md       the measures per step (and measures.json)
  400-FrictionLog.md    what happened, cost, workaround, change, where
  500-Benchmark/        the replayable steps
  600-Retros/           one per slice: the numbers, three proposed changes, what was picked
  decisions/            numbered decisions
```

`.migrator/` adds what only a migration has:

```text
.migrator/
  700-Inventory.md      the units of the old system, each with its state
  710-Ledger.md         what was converted, by which rule, with which evidence
  720-Architecture/     source.md and target.md
  730-Rules/            the transform rules, one file each, with their status
  740-Differences.md    the intended differences, each accepted by a person
  750-Data/             the reconcile reports
```

The numbers leave room: 000 to 600 are the same everywhere, 700 and up belong to the kind of
project.

## 6. The whole picture

```text
HtmlConstruction/                 the container repository
  .my/                            proposals and discussions (this file)
  test-tooling/                   Bruinet's repository (submodule)
    .developer/                 the record of building the four tools       NEW
    .docs/                        decisions, as now
    src/  tests/  docs/  tools/
    samples/Todo/
      specs/                      Todo's specs                                MOVED
    Frainet/
    Lirenet/                      NEW
    Gaminet/                      NEW
  Bouw7/                          a product's suite
    .testing/                     its record (see the open questions)
    specs/
    Bouw7.Web.UITests/
```

## 7. In what order

Each line is a step with a check, small enough to do alone.

| # | Step | Done when |
| --- | --- | --- |
| 1 | Create `Lirenet/` with Frainet's layout; move the spec code and its tests, renamed | Lirenet's tests pass; `lirenet check` and `lirenet trace` give the same output as `frainet` did on every spec |
| 2 | `frainet check` and `frainet trace` call Lirenet | The site loop is green for Bouw7's four modules without a change to it |
| 3 | Move the scripts, skills and docs to `lirenet` | Nothing in the repositories names `frainet check` or `frainet trace` but the forwarding commands |
| 4 | Move Todo's specs to `samples/Todo/specs/` | Lirenet's tests read them there |
| 5 | Create `.developer/` with the core files, first in Kofflard, then in Tomorrow, then `test-tooling/.developer/` | A step file without a check is refused (slice 0's planted fault); one real step of each product is done through it and recorded |
| 6 | Create `Gaminet/` with the layout and an empty tool | It builds; `gaminet --help` runs |
| 7 | Move `spec-from-app` to `Lirenet/skills/` and `friction-log` to `Frainet/skills/`, each with a stub in `.github/skills/` and `.claude/skills/`; an `AGENTS.md` per tool | A fresh agent working on Bouw7 still finds and follows both skills; every stub's path exists (a check in the repository's tests) |

Steps 1 to 4 are done: the old `frainet`, `lirenet` and the forwarding `frainet` gave the same output and exit code
on 13 commands over Todo's and Bouw7's specs, and the site loop was green before and after its script changed.
Of step 5, Kofflard's `.developer/` exists with the core files (2026-11-16), no step done through it yet, and
Tomorrow's `.sdlc/` is renamed `.developer/` with its own layout. Step 6 is done (2026-11-16): `Gaminet/` has the layout and
an empty `gaminet` (`--help`, exit 0; anything else is refused, exit 2), without `Gaminet.Ledger` and `Gaminet.Rules`,
which come with the first step that needs them. They were slice 1's first step in the small-steps proposal; step 5 is slice 0; step 6
waits until slice 6 needs it; step 7 goes with step 3.

## 8. Open questions

- **Skills for a product on its own.** Proposal: a plugin per tool, later (section 4). Until
  then a product gets the skills from the `test-tooling` beside it.
- **Do Bruinet's own skills move too**, to a `skills/` folder at the root, so all four look the
  same? Proposal: no: `.github/skills/` is where Copilot reads them, and Bruinet is the
  repository's root, so its skills are already "with their tool".
- **Tomorrow's layout.** Tomorrow's record folder was `.sdlc/` and is renamed `.developer/`
  (2026-11-16), with its own layout kept: `sdlc.md`, `plan/`, `modules/`, `decisions/`, `canon/`,
  `templates/`, `runs/`, `reviews/`. Kofflard's has the core files of this section. Proposal: the
  two run side by side for a while, and what they share becomes the core.
- **Which step is the first in Kofflard and in Tomorrow?** Proposal: one small change that is
  already wanted in each, with a spec of one journey first, so the experiment costs nothing
  extra.
- **One record for the four tools, or one per tool?** Proposal: one, `test-tooling/.developer/`:
  a slice goes through all four parts, so its steps and measures belong together.
- **Bouw7's `.testing/`.** Bouw7 is neither development nor migration: it tests a product that
  is not ours. Proposal: it keeps `.testing/`, as a third kind (a test project), with the same
  000 to 600 core. The alternative is to rename it `.developer/`.
- **Does `.testing/` get the 000 to 600 numbers?** Its files are numbered differently today
  (150-Structure, 200-Tasklist, 500-Harvest). Proposal: leave it until Bouw7's next slice
  (slice 7), then renumber once.
- **Gaminet's libraries.** `Gaminet.Ledger` and `Gaminet.Rules` are a guess before the design
  exists. Proposal: start with `Gaminet.Cli` and one library, and split when slice 6 shows where.
- **A repository of its own for Lirenet.** It depends on nothing and is meant for teams without
  Bruinet. Proposal: a folder now, a repository when someone outside uses it.
- **The existing migration's record.** It has a folder of its own already. Proposal: not touched
  now; when it comes in, its record is mapped to `.migrator/`, not renamed in place.
