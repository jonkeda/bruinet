# What goes in Bruinet Test, Spec, Develop and Migrate

Status: **proposal**, for discussion. Date: 2026-11-18, revised the same day. **One product, four modules**:
Bruinet, with the modules Test, Spec, Develop and Migrate ([one-or-four](../structure/one-or-four.md)). They
replace the four separate parts Bruinet, Lirenet, Frainet and Gaminet. This document places everything that
exists or has been proposed in one of the modules, and says how the modules meet. The steps to get there are
in [the plan](../structure/plan.md).

| Was | Is |
| --- | --- |
| Bruinet | **Bruinet Test** |
| Lirenet | **Bruinet Spec** |
| Frainet | **Bruinet Develop** |
| Gaminet | **Bruinet Migrate** |

## 1. In one line each

| Module | One job | The question it answers |
| --- | --- | --- |
| **Test** | Test | Does the system do it, on this head, at this layer? |
| **Spec** | Describe | What must the system do, is that description sound, and is every line of it proven? |
| **Develop** | Do the work | Who does the next piece of work, with what brief, and when is it done? |
| **Migrate** | Migrate | Which unit of the old system is converted, by which rules, and is it at parity? |

## 2. How to decide where something goes

Ask in this order; the first yes decides.

| # | Question | If yes |
| --- | --- | --- |
| 1 | Do two modules both need it? | **The lowest module that both use**, as a library with no opinions (the spec reader is in Spec, and Test uses it) |
| 2 | Does it drive an app, read a screen, call an API or touch a database, or need a page object? | **Test** |
| 3 | Does it read or write a spec, or need only spec files and test results? | **Spec** |
| 4 | Is it about doing work: picking it, briefing an agent, judging it, learning from it? | **Develop** |
| 5 | Is it only about turning one system into another? | **Migrate** |

And three tests for a placement:

- **Taken alone.** A team that writes specs and tests with plain xUnit takes the Spec packages and nothing of
  Test's drivers. A team that only tests takes Test. Whatever breaks that is misplaced.
- **Arrows one way.** Test and Spec are the base; neither depends on Develop or Migrate. Develop uses Test and
  Spec; Migrate uses all three. Inside the base, Test references only Spec's format package. An architecture
  test enforces it.
- **No second copy.** A thing is written once, in the module that owns it; another module references it. Not
  a copy, not a process started to reach it.

## 3. Test

| Holds | Exists |
| --- | --- |
| Drivers and heads: Playwright, FlaUI, Appium, Stride, Android, MauiHeadless, bUnit, the `service` head, the mixed head | ✓ |
| Page objects, ControlObjects, waits, locators, artifacts | ✓ |
| The BDD engine: feature files, phrases, `[BddControl]`, verbs, states, tables in steps, outlines | ✓ |
| **The spec runner**: a spec's examples run as scenarios with `Rule` and `Spec` traits; it reads specs through Spec's format package | ✓ runs; reads with its own reader today (`BddSpecReader`) |
| **Table checks** (automation entry point A): field table, list table, rights matrix, screen card, flow, state checks on screen; one generated test per cell against the page object | Proposed |
| Test data and test sets, accounts (`TestAccounts.md`), sign-in | ✓ |
| Below the UI: contracts, HTTP and SQL replay, stub backends; proposed: the API head, store objects, effects of a scenario, comparing two runs (old and new version, or old and new system) | Partly |
| The Scraper: record, explore, draft page objects; the places list (`bruinet scrape places`) and the page checklist (`bruinet suite pages`) | ✓ |
| Recordings and the narrator, the review page, screenshots | ✓ |
| Test selection, the Presenter | ✓ |
| Editors: Visual Studio and VS Code, the language server; a rule's sentence on hover through Spec's format package | ✓, hover proposed |
| Skills for test work: the control and UI-test skills, `scrape-pages`, `site-setup`, `scenario-from-spec`, `spec-from-app`, `run-triage`, `narrated-review`, `api-upgrade`, `convert-control` | ✓ |

The package names stay as they are (`Bruinet.Core`, `Bruinet.Html`, …). Not for compatibility, which this work
does not keep: `Bruinet.Html` already says what it is, and a `Test.` prefix on 46 packages adds nothing.

## 4. Spec

| Holds | Exists |
| --- | --- |
| **The spec format**: journeys (intent, rules, examples, choices, not in this journey, open questions), EARS rules; proposed: product, domain model, screens, flows, interfaces, and the table styles | Journeys ✓, the rest proposed |
| **`Bruinet.Spec.Format`**: reading a spec file into a model (sections, rules, examples, tables, pictures). No checks, no dependencies. **The one reader of the format**, used by Spec's checks and by Test's spec runner and language server | Proposed; today two readers |
| `bruinet spec check`: the form of every file, vague and code words, ids; proposed: EARS shapes, the vocabulary check, cross-file checks | ✓ as `lirenet check`, partly |
| `bruinet spec trace`: rule ids found in test sources, states from results (TRX); proposed: per-row states for table styles, "proven at which layer" | ✓ as `lirenet trace`, extensions proposed |
| Coverage per product; acceptance per rule; change proposals and what a change reaches; release evidence | Proposed |
| **Readers of the pictures**: Mermaid flowcharts, state, ER and sequence diagrams (a documented subset); Wireframe screens through `wire parse` JSON | Proposed |
| **`SpecTable`** (automation entry point C): an xUnit data attribute that feeds a theory from a spec's table, with the rule id as trait (`Bruinet.Spec.Xunit`) | Proposed |
| **Drift against inventories**: the spec against what Test's Scraper found (the places list), an OpenAPI document, a schema snapshot | Proposed |
| Skills that only need spec files: `spec-rule`, `spec-review` | Proposed |
| The spec-format documentation | ✓ `Lirenet/docs/spec-format.md`, to `docs/spec/` |

**Not in Spec**: running anything against an app. `spec-from-app` drafts a spec *from a running app* with
Test's Scraper, so it is a Test skill that writes in Spec's format.

**Wireframe** stays its own repository and tool. Spec reads its output; it is not a module.

## 5. How Test and Spec share the format

Today Test reads specs with `src/Bruinet.Bdd/BddSpecReader.cs` and Spec with its own parsing in
`SpecCheck.cs`: two readers of one format. With table styles and pictures added they would drift.

| Package | Holds | References |
| --- | --- | --- |
| `Bruinet.Spec.Format` | Reading a spec into a model | nothing |
| `Bruinet.Spec` | `check`, `trace`, coverage, drift | `Bruinet.Spec.Format` |
| `Bruinet.Spec.Xunit` | `SpecTable` and its helpers | `Bruinet.Spec.Format`, xUnit |

`Bruinet.Bdd` (the spec runner), the table checks and the language server reference `Bruinet.Spec.Format`
only. The commands `bruinet spec check` and `bruinet spec trace` live in the one CLI (`tools/Bruinet.Cli`),
which references `Bruinet.Spec`.

## 6. Develop

| Holds | Exists |
| --- | --- |
| **The loops**: task, capability, module, case, and the improvement loop above them; their stages (spec, explore, controls, page objects, scenarios, run, triage, learn, fix friction) | Written up (`Frainet/.my/loops/`), run by `tools/Scripts/site-loop.ps1` and by hand in Bouw7 (`.testing/100-Loops.md`) |
| **The orchestrator**: the next unit of work, its brief, the agent that does it, the checks that judge it, the record | Proposed |
| The **verdict rule**: an agent never judges its own work; a reviewer that is not the builder; which checkpoints need a person | Written up, not enforced by a tool |
| **The record**: step files, measures (turns, human minutes, cost, escaped faults), the friction log, retros, the benchmark of past steps; a project's record folder `.developer/` | The friction log ✓ (Bouw7), the rest proposed |
| Skills about the work itself: **`friction-log`**, **`improvement-review`**, the method of `ai-item` | ✓, filed with the test skills today |
| The method: dark factory, tiny prompts with exact gates, loops in loops | ✓ `Frainet/.my`, to `.my/develop/` |

**No code yet.** Develop is a folder of notes until a case of the small-steps proposal needs code; then it
gets `Bruinet.Develop` and a `bruinet develop` command group.

**Not in Develop**: any check. Develop *calls* checks (`bruinet spec check`, `bruinet spec trace`,
`dotnet test`, Test's runs) and records their verdict; it never decides one. Develop may reference Spec and
Test, but adds no check of its own: the judge is always another module.

## 7. Migrate

The inventory of the old system, source and target architecture descriptions, transform rules, the ledger
(per unit: state, rules applied, parity per layer), intended differences, data reconcile, migration packs; a
project's record folder `.migrator/`. It uses Spec for the behaviour (captured once, the fixed point), Test
for the proof (comparing old and new runs), and Develop's loop for the conversion. **No code yet**: notes
until a real migration; then `Bruinet.Migrate` and `bruinet migrate`.

## 8. How the modules meet

| What crosses | From | To | As |
| --- | --- | --- | --- |
| The spec's model | Spec | Test's spec runner, table checks, language server | `Bruinet.Spec.Format`, a reference |
| Rule ids (`TOD.02.R3`), table and row ids | Spec (the spec) | Tests in Test or anywhere | Text: a trait or a tag |
| Test results | Test, or any test tool | Spec's trace, Develop's record, Migrate's ledger | Files: TRX, Bruinet's result JSON. A file, because the results come from any test tool, not only Test |
| Inventories (places, endpoints, tables) | Test's Scraper and logs | Spec's drift report, Migrate's inventory | Files |
| Comparison reports (old against new run) | Test | Migrate's ledger, people | Files |
| Work state (loops, steps, measures) | Develop | People, agents | Markdown and JSON in `.testing/` or `.developer/` |
| Commands | each module | Develop, CI, agents | One CLI: `bruinet …`, `bruinet spec …`, later `bruinet develop …`, `bruinet migrate …`, with exit codes; one MCP server with tools in groups |

Inside the repository a module reaches another by **reference**, never by starting it as a process. Files
remain the way in for what comes from outside: results of any test tool, an OpenAPI document, Wireframe's
JSON.

## 9. What moves

| Now | Moves to | Why |
| --- | --- | --- |
| `Lirenet/src/Lirenet.Specs`, its tests | `src/Bruinet.Spec` (and `Bruinet.Spec.Format`), `tests/Bruinet.Spec.Tests` | One product, one solution |
| `lirenet check`, `lirenet trace` (`Lirenet.Cli`) | `bruinet spec check`, `bruinet spec trace` | One CLI |
| `BddSpecReader`'s own parsing | `Bruinet.Spec.Format` | One reader of the format |
| `Frainet/src/Frainet.Cli` (the forwarder), `Gaminet/src/Gaminet.Cli` | Deleted | No code ahead of a case; no forwarding |
| `Frainet/.my`, `Gaminet/docs`, `Lirenet/.my/design` | `.my/develop`, `.my/migrate`, `.my/spec/design` | Plans for work in flight |
| `Lirenet/docs/spec-format.md` | `docs/spec/spec-format.md` | A guide for users |
| `friction-log`, `improvement-review` | Listed under Develop in `AGENTS.md` | They are about the work, not about testing |
| The small-steps proposal (container `.my/steps`) | Stays, with a "names since" row | It is the plan for all four modules |

## 10. Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Do Test's packages get a `Bruinet.Test.` prefix? | No: the module that is used keeps its names; new modules get `Bruinet.Spec`, `Bruinet.Develop`, `Bruinet.Migrate` |
| Q2 | Do Test's commands move under `bruinet test …`? | No: they stay where they are; the other modules get a group each |
| Q3 | Do the spec files change? | No: their title is already `# Spec: <id> …` and `*.spec.md` stays; "Lirenet spec" goes from the docs and skills |
| Q4 | Spec tools in the MCP server now, or only the commands? | The commands now; MCP tools when an agent needs them in a loop |
| Q5 | Is `Bruinet.Spec.Xunit` a package before anyone uses `SpecTable`? | No: built with the first table-style trial |
| Q6 | Does Develop's orchestrator become code? | Skills and conventions first; code when a whole case has been run by hand with step files and measures |
| Q7 | Does `ai-item` move to Develop? | The method yes, the plan no: Develop holds how an item is run, Test holds which items |
