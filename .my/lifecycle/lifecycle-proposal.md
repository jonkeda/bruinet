# Discussion: beyond UI testing — testing, specs, migration and the factory as four parts

Status: **discussion paper**, with proposals and questions. Date: 2026-11-14.

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with.

| | |
| --- | --- |
| The question | Bruinet started as a UI test tool. Can it grow into a tool for **migrations** and for the **software life cycle**? |
| Short answer | Yes, but **not inside Bruinet**. Four parts, each with one job (section 8): **Bruinet** tests, a **spec tool** describes, a **migrator** converts with evidence, and the **factory** runs the agents. Bruinet stays a test tool, widened from the UI to the API and the database |
| Builds on | `.my/ears/ears-proposal.md` (EARS rules), the talk about folding Frainet in and adding a domain model, screens and flows, and `.my/Planning/bouw7-proposal.md` (B16 parity runs, F1–F10) |
| Added after discussion | Section 4: checks at the **API** and in the **database**, so a migration is checked in full, not only on the screen |
| Added after discussion | Section 5: source and target **architecture**, **transform rules** between them, and **AI** (Claude Code, VS Code) doing the conversion with Bruinet as the harness |
| Decided in discussion | **The migrator is separate from Bruinet. Bruinet is for testing. The spec tool is its own thing, also for ordinary software development.** Section 8 is rewritten for this; where sections 3–6 say `bruinet spec` or "Bruinet gives", read the part named in section 8.2 |
| Keep in mind | The nine-reviewer summary ([00-summary.md](https://github.com/exact-mobile-apps/test-tooling/blob/71d91504/.my/copilot/answers/00-summary.md), at its last commit): the strongest position is narrow, "quality engineering platform" is a trap, and there is one author over 43 packages |

## 1. What I think

Bruinet's real asset is not "it clicks buttons". It is that Bruinet holds a **description of what
an app does that a machine can run**, and that this description does not depend on the stack
under it: scenarios in the screen's words, page objects, recorded SQL and HTTP, API contracts,
recordings on video. The README says it itself: "the tests never change when the stack under them
does."

That sentence is the definition of a migration tool. In a migration the stack changes and the
behaviour must not. Whoever holds an exact, runnable description of the old behaviour can say, per
screen and per rule, whether the new system is there yet. Today that is done by hand, with
spreadsheets and testers comparing two screens.

The life cycle follows from the same asset. If the description of the system (specs, rules,
domain model, screens, flows) lives beside the proof that it holds (tests on heads), then
Bruinet can answer the questions every phase asks: *what must it do, is it built, is it proven,
what changed, what broke*. That is a real slice of the life cycle, and the one agents need most.

Where I would stop: Bruinet should not become an issue tracker, a CI system, a deployment tool or
a design tool. Those exist, teams already have them, and one author cannot build them. Bruinet
should be the **source of truth about behaviour** that those tools read.

So one spine, and four parts around it (section 8):

```text
   Lirenet (specs)           Bruinet (tests)            Gaminet (migration)
   what the system does  ->  proof that it does it  ->  the same proof, old against new
            \                     |                      /
             +------ Frainet, the factory: agents and loops ------+
```

## 2. What Bruinet already has, seen this way

| Bruinet today | In a migration it is | In the life cycle it is |
| --- | --- | --- |
| Scenarios (`*.feature.md`) on any head | The behaviour the new system must match | Acceptance tests of a spec |
| Page objects and ControlObjects | The seam: same scenario, old and new screen | The screen inventory in code |
| Scraper (Record, Explore) | Capture of the old system: screens and navigation | Drift check: the app vs its described screens |
| SQL replay, schema snapshot | Same data under old and new; a first draft of the data model | Tests without a database |
| HTTP replay, `Bruinet.Contracts` | Old API vs what the new client reads | Contract checks per release |
| Mixed head | Old and new parts in one journey, while both live | Journeys across apps |
| Heads and tiers | Old head and new head | Fast loop vs the judge |
| Test selection | "What does this converted screen reach" | Tests per change |
| Narrated recordings, review pages | Side-by-side evidence for the owner | Acceptance and release evidence |
| Frainet: specs, check, trace | The contract of the migration | The specify phase |
| Skills, MCP, CLI, orchestrator idea | Conversion loops for agents | The build loops |
| `convert-control`, `api-upgrade` skills | Small migrations already done this way | — |

## 3. Proposals: migration

A migration in five moves: **capture, pin, convert, compare, switch**.

| # | Proposal | What it gives |
| --- | --- | --- |
| M1 | **Capture the old system** into a system description: the Scraper's Explore gives screens and flows; the SQL schema snapshot gives a draft domain model; HTTP recordings give the API contracts; `spec-from-app` (F3) drafts rules in EARS. Everything unclear becomes an open question for the owner | A described system where there was none. Most old systems have no specs; this is the first value a customer sees |
| M2 | **Pin the behaviour**: scenarios run green on the old system, tagged with their rules. This is the characterization suite, the oracle of the migration | "Works as before" becomes checkable |
| M3 | **Parity runs** (B16): one scenario on the old head and the new head, with a side-by-side report: steps, screenshots, timings, differences | The owner judges differences, not test logs |
| M4 | **A migration ledger**: per screen and per rule one state: not started, built, at parity, accepted, retired, with parity shown per layer (screen, API, database; section 4). The migrator's `status` prints it, as Markdown and JSON | The burn-down of a migration, from facts, not from a spreadsheet |
| M5 | **Intended differences**: a list of rules that change on purpose in the new system (new rule id, old one retired), each accepted by the owner. Parity does not flag them | A migration is never 1:1; the tool must know what is meant to differ |
| M6 | **Parity below the screen**: the same scenario is checked at the API and in the database too, not only on the screen. Worked out in section 4 | Proof where migrations really break |
| M7 | **Conversion loops for agents**: a skill per kind of migration (as `convert-control` is now). The brief for one screen is its rules, its old page object, its recordings; done is "at parity on the judge head" | Agents convert; Bruinet's checks say when a screen is done |
| M8 | **Living together**: mixed-head journeys across old and new parts while both run (strangler pattern), as G5 does for phone and back office | Migrate module by module, switch per module |

What Bruinet would **not** do in M7: it does not translate code itself. It gives the agent the
brief and the exact check. The translating is the agent's work, per stack, in a skill.

## 4. Checks below the screen: API and database

Added after discussion: Bruinet today mainly checks the UI. A migration is only checked in full
when the **API** and the **database** are checked too. A screen can look right while the wrong
thing was sent or stored, and that is the usual way a migration fails.

### 4.1 Three layers, one scenario

| Layer | What is checked | In a migration |
| --- | --- | --- |
| **Screen** | What a person sees and can do | New screen behaves as the old one |
| **API** | What goes over the wire: requests sent, answers given, errors, rights | New client sends what the old one sent; new backend answers what the old one answered |
| **Database** | What is stored and changed: rows, values, relations, invariants | Same action leaves the same data; migrated data is complete and correct |

The idea: **one scenario, three observations.** While a scenario runs, Bruinet records what the
screen showed, which calls went over the wire and what changed in the database. That is the
scenario's *behaviour record*. Parity (M3) compares the records of old and new, layer by layer.
A person still writes one scenario, in the screen's words.

### 4.2 What exists, and what it is used for today

The surprise is how much of the *recording* exists. It is used to **replace** a dependency, not
yet to **judge** one.

| Exists | Used today for | Missing for checks |
| --- | --- | --- |
| SQL log (`Bruinet.Mocking.Data`): every command and the objects it names, never its values | Replay without a database; timing reports | The *effect*: which rows changed, to what values. No comparison of two runs |
| Schema snapshot | Replay | A comparison of two schemas; a mapping to the domain model |
| HTTP recording and replay; `Refresh` already lists which answers changed | Hermetic tests | Pointing the same recorded requests at a *second* system and reporting the differences as parity |
| `Bruinet.Contracts` (AD-035): each response against the DTO the app reads; `ExpectedRequest` from the reference client, "for a migrated app, the old app's serializer" | Shape of responses and of writes | Behaviour, not only shape: status codes, errors, rights per role, sequences |
| The `service` head: feature files below the UI | A few Todo scenarios | Steps for API and database that any product can declare, as page objects do for screens |
| Volatile values (record twice, mark what differs) | Replay matching | The same trick for comparing old and new: ids and clocks must not count as differences |

### 4.3 Proposals: API

| # | Proposal | Notes |
| --- | --- | --- |
| A1 | **Endpoint objects**: an API described as page objects describe a screen: one object per resource, typed requests and answers, phrases from attributes. Scenarios tagged `@api` run on an **API head** | The `service` head runs inside the app's process; the API head calls a deployed system from outside, so it works on a system that is not ours |
| A2 | **API behaviour checks**, beyond shape: status codes, error bodies, paging, sorting, and **rights per role** with the named accounts of `TestAccounts.md` | Contracts stay for shape; this is for what the API does |
| A3 | **API parity, backend migrates**: record a scenario's calls against the old backend, send the same calls to the new one, compare the answers field by field with volatile values left out | Close to HTTP replay's `Refresh`; the report format is the new part |
| A4 | **API parity, client migrates**: old and new client run the same scenario; what each *sends* is compared | `ExpectedRequest` does this per write today; widen it to whole scenarios |
| A5 | **Traffic observed from the UI run**: on the web head, Playwright sees the calls the page makes; on MAUI, the app's exchange log. So a UI scenario yields its API observations with no extra test | Works on Bouw7 acc today, with no access beyond a login |
| A6 | **Other consumers**: integrations that call the API are not on any screen. Their contracts come from recorded traffic or from their owners | Easy to forget; often what breaks after go-live |

### 4.4 Proposals: database

| # | Proposal | Notes |
| --- | --- | --- |
| D1 | **Store objects**: the database described in the domain model's words (an Invoice, its Lines), mapped to tables once per system. Phrases like `Then the store has an invoice "2026-014" with status Open` | The mapping is the only place that knows table names. Old and new schema each get a mapping to the *same* domain model, so they can be compared though they differ |
| D2 | **Effect of a scenario**: the rows a scenario inserted, changed or deleted, with values, in domain terms. The SQL log already names the tables a scenario touched; snapshot only those before and after, or use SQL Server's change tracking | The log deliberately holds no values today; an effect record does, so it needs masking (section 4.6) |
| D3 | **Effect parity**: the same scenario on old and new, effects compared through the domain model, volatile values left out | "Looks the same, stored differently" is caught here |
| D4 | **Data migration checks** (the migrator's `reconcile`): after the data is moved: counts per entity, sums and checksums, each old record found in the new store, no orphans | A different check from D3: it judges the *moved data*, not behaviour |
| D5 | **Invariants as checks**: rules that hold for all data ("an invoice number is unique", "hours belong to one project") run as queries on old and new | The old data often breaks them already; better known before the move than after |
| D6 | **Schema comparison**: old and new schema snapshot side by side, through the mapping: what has no counterpart | A report for the migration's designer |

### 4.5 Across the layers

- **Consistency in one scenario**: after `When I save the invoice`, the scenario may check the
  screen, the call and the stored row. Mostly this is not written by hand: the behaviour record
  holds all three, and parity compares them.
- **The spec needs two more kinds of rule.** An EARS rule in a journey is about what a person
  sees. A migration also needs **invariants** (on the domain model, D5) and **interface rules**
  (on an API: "If the token has expired, then the API shall answer 401"). EARS fits both; they
  live beside the domain model and in an interface spec, not in a journey.
- **The ledger (M4)** gets a column per layer: a rule can be at parity on the screen and not yet
  in the database.
- **Beyond these three**: documents (an invoice as PDF), e-mails, exports, scheduled jobs. Not
  now, but the behaviour record should be able to hold more kinds of observation later.

### 4.6 What makes this hard

- **Access.** Each layer needs more access than the one above: a login for the screen, the same
  login for observed traffic (A5), API credentials for A1–A3, a database connection for D1–D6.
  On a customer's acc environment the database is often not reachable. The method must degrade:
  whatever layers are reachable are checked, and the ledger says which were not.
- **Personal data.** Screens and SQL logs keep little; API bodies and row values hold names,
  addresses, amounts. Records with values must be masked before they are stored, and never
  committed raw. This needs a rule as strict as AD-038 is for secrets.
- **Different on purpose.** A new schema or a new API version differs by design. Comparison
  through the domain model (D1) and the intended differences (M5) are what keep parity meaningful.
- **Volatile values.** Ids, timestamps, generated numbers. "Record twice" already solves it for
  replay; the same marking must serve comparison.
- **Writes on a shared environment.** Effect checks need a scenario's changes told apart from
  another user's. Either an environment of our own, or data marked per run (the data marker
  Bouw7's rules already ask for).

## 5. Two architectures, transform rules, and AI doing the work

Added after discussion: the system we migrate **from** and the one we migrate **to** will mostly
have different architectures. So both must be described, and **transform rules** must say how one
becomes the other. The converting itself is done by AI: Claude Code, or Copilot in VS Code.

### 5.1 Four descriptions, not one

| Description | Says | Who makes it | Changes during the migration? |
| --- | --- | --- | --- |
| **Behaviour** (specs, rules, domain model, screens, flows) | What the system does | Captured from the old system (M1), accepted by the owner | No, except intended differences (M5) |
| **Source architecture** | How the old system is built | An agent, from the code; a developer corrects it | No |
| **Target architecture** | How the new system must be built | People decide; written as decisions and shown in one worked example | Rarely, and then on purpose |
| **Transform rules** | How a thing of the source becomes a thing of the target | Drafted by an agent from the two architectures and the worked example; accepted by a person | Yes: the catalog grows as cases turn up |

The behaviour is the fixed point. The two architectures and the rules between them are the
moving parts, and parity (M3, section 4) is the judge of all three.

### 5.2 Describing an architecture

The same list for source and target, so they can be laid side by side:

| Aspect | Example, source → target |
| --- | --- |
| Projects and layers | One app project with code-behind → app, view models, services, each a project |
| Screens and navigation | Xamarin.Forms pages with `NavigationPage` → MAUI Shell routes |
| State and binding | Hand-written `INotifyPropertyChanged` → source-generated view models |
| Controls | A control library → its counterparts in the new stack |
| Data access | Stored procedures and `DataSet` → EF Core entities |
| Database | The schema: tables, types, keys → the new schema |
| API | Endpoints, DTOs, versioning, errors → the new ones |
| Cross-cutting | Sign-in, rights, logging, errors, offline, localisation |
| Background work | Jobs, sync, notifications |

- **Source**: an agent makes an **inventory** from the code (every screen, view model, endpoint,
  table, job, control used) and a short description per aspect. The inventory's rows become the
  ledger's rows (M4), so nothing is forgotten.
- **Target**: decisions in the `.docs/decisions` style (one decision, what it rules out), plus
  **one worked example**: a small slice built by hand or closely supervised, which shows every
  aspect. Agents copy an example far better than they follow prose.
- **The target as checks**: as much as possible of the target architecture is written as
  something that fails: architecture tests, analyzers, banned APIs (the `dotnet-quality-checks`
  stack, Bruinet's own `BannedSymbols`). "No logic in code-behind" is then a red build, not a
  review remark.

### 5.3 Transform rules

A transform rule says: *a thing of this kind in the source becomes this in the target.*

| Field | Holds |
| --- | --- |
| Id | `T.UI.03`, never reused |
| Kind | Structure, pattern, control, data, API, naming |
| From | The source shape, with a real example from the code |
| To | The target shape, with the same example converted |
| Unless | The exceptions, each pointing to another rule or to a person |
| Checked by | What proves it was applied right: a build check, an architecture test, parity on a layer |

Kinds, with what Bruinet already has for them:

| Kind | Example | Already there |
| --- | --- | --- |
| Structure | A page with code-behind → a view, a view model and a service | — |
| Pattern | An event handler that calls the database → a command that calls a service | — |
| Control | `Picker` with a custom renderer → the new control | **ControlObjects**: the same `SelectItem` on both sides, so the scenario does not change; the Scraper's patterns per technology |
| Data | Table `tblInv`, column `Stat` as `int` → `Invoice.Status` as an enum; how each value maps | The store mapping (D1) *is* the data transform rule, read the other way |
| API | `GET /inv/list?p=2` → `GET /api/invoices?page=2`; old DTO field → new | Contracts, `ExpectedRequest` |
| Naming | Hungarian and Dutch names → the target's naming | — |

How the catalog comes to be:

1. People decide the target and build the worked example.
2. An agent drafts the first rules from the example: what was the source, what did it become.
3. Units are converted by rule. When an agent meets something **no rule covers**, it does not
   improvise: it stops that unit, writes the case as an open question with a proposed rule, and
   takes another unit. A person accepts the rule; the catalog grows by one.
4. A rule that keeps producing parity failures is wrong: it is changed, and the units converted
   with it are listed by the ledger and done again.

So the ledger (M4) gets one more column: **by which rules** a unit was converted. That makes a
bad rule a query, not a hunt.

### 5.4 AI does the work; the tools are the harness

The agent converts. The migrator, with Bruinet's checks under it, gives it four things and keeps one for itself.

| The tools give | For one unit (a screen, an endpoint, a table) |
| --- | --- |
| **The brief** | The unit's behaviour rules, its source code, its old page object and recordings, the transform rules that apply, the worked example |
| **The tools** | MCP and CLI: next unit from the ledger, the brief, run the checks, report the result |
| **The checks** | Build, target-architecture tests, unit tests, parity on a fast head, parity on the judge head, API and database parity where reachable |
| **The record** | The ledger: state, rules applied, evidence, open questions |
| **Keeps: the verdict** | An agent never marks its own unit done. Only passing checks move a row, and a person accepts |

The loop for one unit:

```text
next unit from the ledger (dependencies first)
  -> brief
  -> convert, by the transform rules
       no rule fits? -> open question with a proposed rule; take another unit
  -> checks: build, architecture, tests, parity (screen, API, database)
       red? -> fix, a bounded number of turns; then hand to triage
  -> review by another agent than the builder
  -> ledger: at parity, with evidence
  -> a person accepts, per module
```

**Claude Code and VS Code alike.** Bruinet already works this way: one `AGENTS.md`, skills in
`.github/skills` with stubs in `.claude/skills`, one MCP server, one CLI. The migration adds
skills, not a second system:

| Skill | Does |
| --- | --- |
| `migration-inventory` | Source code → inventory and source architecture |
| `migration-capture` | Running old system → behaviour description (M1) |
| `migration-rule` | Write, split or correct a transform rule |
| `migration-unit` | Convert one unit by the loop above |
| `migration-triage` | A red parity: is it the conversion, the rule, the test, or an intended difference? |

And MCP tools beside the existing ones: `migration_next`, `migration_brief`, `migration_check`,
`ledger_update`.

**Where people stay**: the target architecture, each transform rule, each intended difference,
the parity report per module, and go-live. Everything between can run dark.

### 5.5 Migration packs

Transform rules for a pair of stacks are mostly the same at every customer: Xamarin.Forms → MAUI,
Vue 2 → Vue 3, .NET Framework → .NET. So a **migration pack** per pair: its transform rules, its
control mappings, its skills, its target checks. A project starts from the pack and adds its own
rules. The second migration of the same pair starts far ahead of the first. `convert-control` is
the seed of this: a pack of one rule.

### 5.6 What this needs that Bruinet lacks today

- **The source code.** Sections 3 and 4 work on a system we can only log in to. This section does
  not: converting needs the code of the old system and a place to build the new one.
- **A format for architecture descriptions and transform rules**, with a model-free check (ids,
  fields filled, examples present), as Frainet has for specs.
- **The ledger**, now clearly the centre: inventory row, state, rules applied, parity per layer,
  evidence.
- **Units and their order**: which unit depends on which, so the loop converts leaves first.

## 6. Proposals: life cycle

Only the phases where behaviour is the subject.

| Phase | # | Proposal |
| --- | --- | --- |
| Specify | L1 | The spec tool: journeys with EARS rules, plus the three layers from our talk: **domain model**, **screens**, **flows**. A vocabulary check: a rule may only use words the model and the screen list define |
| Design | L2 | Decisions as a feature: the `.docs/decisions` pattern (one decision, once, with what it rules out) offered to a product, with a check that links stay valid. Screen designs are linked, not held |
| Build | L3 | The orchestrator and loops (Bouw7 proposal section 6; Frainet's loops): task, capability, module, each with an exact check as feedback |
| Verify | L4 | What Bruinet is today. Add F1: a rule's state from real results: not reached, not run, failing, passing |
| Accept | L5 | Acceptance recorded on the spec: who accepted which version, on which evidence (a narrated recording, a coverage report). A changed rule drops the acceptance of that rule only |
| Release | L6 | A **release evidence pack**: for this build, every rule with its state per head, the intended differences, the contract checks, the recordings. And release notes drafted from the rules that changed |
| Operate | L7 | Read-only smoke scenarios against production (the account model already has a write guard). A bug from production ends as a new rule or a new example, never only as a fix |
| Change | L8 | Change proposals to an accepted spec (OpenSpec's idea): the proposed rule beside the current one, and the **impact**: which scenarios, tests and screens a rule change reaches (test selection, read backwards) |

## 7. What Bruinet should not become

| Not this | Use instead | Bruinet's part |
| --- | --- | --- |
| Issue tracker, backlog, planning | GitHub Issues, Azure DevOps, Jira | A rule id or a ledger row that an issue can link to |
| CI and pipelines | GitHub Actions, Azure Pipelines | Commands with exit codes and JSON; nothing more |
| Deployment, environments | The team's own tools | Smoke scenarios after a deploy |
| Design tool | Figma and the like | A link per screen, and the check that the built screen has what the spec lists |
| Monitoring | Application Insights and the like | Scenarios as synthetic checks, read-only |
| A code translator | Agents with skills | The brief and the check |

## 8. Four parts

Rewritten after discussion. My first recommendation was one tool with three commands. The better
shape is yours: **separate parts, each usable alone**. It keeps Bruinet's narrow position, which
is what the reviewers called its strength, and it lets a team that never migrates anything use
the spec tool.

### 8.1 The parts

| Part | One job | Used by | Usable without the others? |
| --- | --- | --- | --- |
| **Bruinet**, the test tool | **Test**: prove what a system does, on the screen, at the API and in the database, on any head | Anyone testing an app | Yes, as today |
| **Lirenet**, the spec tool (the spec code inside Frainet's folder today) | **Describe**: journeys and rules (EARS), domain model, screens, flows, invariants, interface rules; check the description; trace it to tests | Any software team, with or without agents, with or without Bruinet | Yes. It reads rule ids from test sources and results as files, so it works with other test tools too |
| **Gaminet**, the migrator (new) | **Convert with evidence**: inventory, source and target architecture, transform rules, the ledger, intended differences, data reconcile, migration packs | A migration project | No: it needs the spec tool for the behaviour and Bruinet for the proof |
| **Frainet**, the factory | **Build**: run the agents that write the code, for a **new system**, a **change** to an existing one, or a **migrated unit**. The loops, the orchestrator, briefs, the rule that an agent never judges its own work, decisions | Ordinary development and migrations alike | Yes, but it is at its best with exact specs and exact checks |

The fourth part is the factory, and it is called **Frainet** (confirmed 2026-11-14): it also builds
new systems, which is what Frainet's own README says it is for. So the name Frainet moves from
the spec tool to the factory.

The names follow one pattern: an invented word ending in "net" with the subject's letters inside.

| Name | Hidden letters | Part |
| --- | --- | --- |
| Br**ui**net | UI | Testing |
| Fr**ai**net | AI | The factory |
| Li**re**net | RE, requirements (after the linnet) | The spec tool |
| Ga**mi**net | MI, migration (after the gannet, a migratory bird) | The migrator |

A web search on 2026-11-14 found nothing named exactly Gaminet or Lirenet. That is not a
trademark, domain or nuget.org check. Below, "spec tool" means Lirenet and "migrator" means Gaminet. The orchestrator and the loops of sections
5.4 and 6 serve ordinary development as much as a migration, so they do not live in the migrator.

Three kinds of work, one factory:

| Work | The spec comes from | What Frainet builds from | The judge |
| --- | --- | --- | --- |
| A new system | People, written in the spec tool | The spec, the target architecture, a worked example | Bruinet's tests of the spec's rules |
| A change | A change proposal to an accepted spec | The changed rules and the code that is there | The same tests, plus the ones the change reaches |
| A migration | Captured from the old system | All of the above, plus the migrator's transform rules and the old code | Bruinet's comparison of old and new |

So a migration is a build with two extra inputs. The migrator supplies them (inventory, transform
rules, ledger); Frainet does the building in all three cases. The agents themselves are Claude
Code or Copilot in VS Code: Frainet does not write code, it runs the loop around the agent that does.

### 8.2 What lands where

| From this paper | Part |
| --- | --- |
| Heads, page objects, scenarios, replay, contracts, recordings | Bruinet |
| API head, endpoint objects, API behaviour checks, observed traffic (A1, A2, A5) | Bruinet |
| Store objects, effect of a scenario, masking (D1, D2) | Bruinet |
| The behaviour record, and comparing two runs of one scenario (M3, A3, A4, D3) | Bruinet: a comparison of two runs is a test feature, useful for old against new *version* too |
| Spec format, EARS, domain model, screens, flows, vocabulary check (L1) | Spec tool |
| Invariants and interface rules: *written* in the spec, *run* by Bruinet (D5, section 4.5) | Spec tool and Bruinet |
| Trace with results, acceptance, change proposals, impact (F1, L5, L8) | Spec tool |
| Capture of a running system into a description (M1) | Spec tool's skill, using Bruinet's Scraper and logs |
| Inventory, architecture descriptions, transform rules, packs (section 5) | Migrator |
| Ledger, intended differences, data reconcile, schema comparison (M4, M5, D4, D6) | Migrator |
| Migration skills (`migration-unit`, `migration-triage`, …) | Migrator, run by Frainet's loop |
| Orchestrator, loops, briefs, verdict rule, decisions as a feature (L2, L3) | Frainet |
| Release evidence pack (L6) | Spec tool, from Bruinet's results |

### 8.3 How the parts meet

Through **files and ids, never through code references**. That is what keeps them separable.

| Contract | Written by | Read by |
| --- | --- | --- |
| Rule ids (`TOD.02.R3`) in specs | Spec tool | Tests name them as a trait or a tag; the migrator's ledger rows name them |
| Test results and behaviour records, as JSON | Bruinet | Spec tool (trace with results), migrator (parity per layer) |
| Comparison report of two runs | Bruinet | Migrator (ledger), people |
| The ledger, as Markdown and JSON | Migrator | Factory (next unit), people |
| CLI commands with exit codes; MCP tools | Each part its own | Factory, CI, agents |

```text
Lirenet          depends on nothing
Bruinet          depends on nothing        (a test may name a rule id; that is text)
Gaminet     ->   Lirenet, Bruinet          (through files and commands)
Frainet     ->   whichever parts a project uses
```

Editor support crosses the line once: Bruinet's language server showing a rule's sentence on
hover reads the spec tool's files. That stays an optional reader, not a dependency.

### 8.4 What this costs, and how to keep it small

Four parts and one author is the risk of section 10, made larger. To keep it honest:

- **Separate tools, not yet separate repositories.** Frainet already sits in Bruinet's repository
  as a folder of its own with its own solution. The migrator can start the same way. A part gets
  its own repository when someone uses it without the others.
- **The migrator is mostly not code.** Formats, a ledger, checks on them, and skills. The heavy
  machinery (driving apps, recording, comparing) stays in Bruinet, where it is also a test feature.
- **The factory is mostly method.** Skills, conventions and one orchestrator; it should stay thin.

## 9. Order

Nothing ahead of a real case that needs it, as Frainet's plan says.

| Stage | Part | What | Proven on | Done when |
| --- | --- | --- | --- | --- |
| 1 | Spec tool | EARS trial on TOD.02; the tool made usable outside Bruinet's source (a dotnet tool, F10) | Todo | `check` and `trace` run as an installed tool; the owner accepts or rejects EARS |
| 2 | Spec tool | Domain model, screens, flows; vocabulary check | Todo, then Bouw7's first module | A rule with an unknown word fails the check |
| 3 | Spec tool, Bruinet | Capture (M1) and pin (M2) | Bouw7 web on acc | One Bouw7 module described from the running app and green, the owner having only corrected a draft |
| 4 | Bruinet | Behaviour record and comparison of two runs: screen first (M3), then observed traffic (A5) | Todo on two heads, then Bouw7 | A side-by-side report an owner can judge |
| 5 | Bruinet | API head and API comparison (A1–A4) | The first real pair | API differences for one journey in the report |
| 6 | Bruinet | Database: store objects, effect of a scenario, masking (D1–D3) | A system whose database we can reach: Todo first | One journey compared on screen, API and database |
| 7 | Migrator | Inventory, source and target architecture, one worked example, first transform rules, the ledger (section 5, M4, M5) | A real pair with source code: the converted MAUI app and its predecessor | The inventory fills the ledger; an agent converts a second unit from the rules alone |
| 8 | Migrator, Factory | The loop for one unit, run dark; triage | The same pair | One module at parity, accepted by its owner |
| 9 | Migrator | Data migration checks (D4, D6) | The first real data move | A reconcile report an owner signs |
| 10 | Spec tool | Results in the trace, acceptance, release evidence, change proposals (F1, L5, L6, L8) | Whoever has a product in production by then | One release with its evidence pack |

Stages 1–3 are already planned under other names. Stages 4–6 widen Bruinet as a *test* tool and
are worth having with no migration in sight. The migrator starts at stage 7, on top of them.

## 10. Risks

- **One author.** Every proposal here widens the surface. Mitigation: nothing new below the
  spine (no new drivers for this), stage by stage on a real case, and agents doing the conversion
  work through skills, not through new engine code.
- **Losing the narrow position.** "The way to test a MAUI app" is clear; "life cycle tool" is not.
  Mitigation: keep the test tool usable alone; migration and specs are layers on top, never required.
- **A cheap head that looks like the judge** (reviewers' point) matters more in a migration:
  parity on a headless head is not parity. The ledger must say on which head parity was shown.
- **The old system cannot always run.** Capture needs a running old app with data. Where it
  cannot run, only documents and code are left, and that is a different, weaker method.
- **Outsiders cannot get it yet.** A migration tool is for customers; the feed and licence
  questions come first.

## 11. Questions

Each with my proposal; yours decide.

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Which migrations do you mean? UI stack (Xamarin → MAUI, Vue 2 → something), platform (.NET Framework → .NET), a whole rewrite, a database move? | Start with **UI stack and rewrite with the same behaviour**: that is where scenarios are the oracle. Database and platform moves only as far as M6 reaches |
| Q2 | Does Bruinet convert code, or only prove the conversion? | Prove, and brief the agent. Conversion lives in skills per stack |
| Q3 | Is there a real migration to run this on now? Is Bouw7 web going to be rebuilt, or only the phone app converted? Is Construction old-and-new both runnable? | Use the converted MAUI app vs its predecessor first, if the old one still runs; otherwise web vs phone for the journeys they share |
| Q4 | Who is the user: you and your team, customers of a service you offer, or anyone as a product? | A service first ("we migrate with evidence"), a product when a second team runs it without you |
| Q5 | Is the life cycle slice for agent-built software (the dark factory) or for any team? | Agents first: they need exact specs and checks most, and you already work that way. People can use the same files |
| Q6 | Names | **Answered 2026-11-14**: Bruinet (testing), Frainet (the factory), **Lirenet** (the spec tool), **Gaminet** (the migrator). Still to check: trademarks, domains, nuget.org |
| Q7 | Where does a system's description live: in the product's repository, beside its tests? | Yes, `specs/` in the product's repo, as in `Bouw7/specs/`; never in Bruinet's |
| Q8 | Which outside tools must it link to first: GitHub, Azure DevOps, Jira? | None built in. Stable ids and JSON first; one integration when a customer asks |
| Q9 | May scenarios run against production, read-only? | Yes for smoke, only with accounts marked as not writing; off by default |
| Q10 | Who accepts an intended difference, and where is that recorded? | The product owner, on the spec, with a date, as `Status: accepted` is now |
| Q11 | Should the domain model be checked against the real database (schema snapshot)? | Later; loosely, as a report, never as a failure |
| Q12 | Is this commercial, open source, or internal? It changes how much polish stage 4 needs | Decide before stage 4; stages 1–3 are needed either way |
| Q13 | What do we stop or slow to make room: more heads, the Scraper, the IDEs, recordings? | No new UI heads and no new IDE features until stage 6 stands. The API head (A1) is the one exception |
| Q14 | Do screen designs belong in scope at all, beyond a link and a list of what a screen holds? | Link and list only; visual comparison is a separate, later question |
| Q15 | In the migrations you have in mind, which side changes: the client, the backend, the database, or all? | It decides the order: client only → A4 and A5 first; backend → A3; database → D1–D5 |
| Q16 | What access do we get at a customer: only a login, API credentials, a database connection? On Bouw7 acc today? | Plan for a login only (screen and observed traffic), and treat the database as a layer we get on our own environments first |
| Q17 | May row values and API bodies be stored as evidence, and where? | Masked, outside git by default (as `Recordings/` is ignored in BruinetB7); only shapes and masked samples are committed |
| Q18 | Are old and new databases the same schema, a changed one, or another engine (SQL Server → PostgreSQL, SQLite on the phone)? | Assume changed; that is why comparison goes through the domain model (D1) |
| Q19 | Who writes the mapping from tables to the domain model? | An agent drafts it from the schema snapshot and the SQL log; the product's developer corrects it |
| Q20 | Is moving the data itself in scope, or only checking the move (D4)? | Only checking. The move is the customer's or an agent's work, like code conversion |
| Q21 | Do documents, e-mails and jobs matter for the first migration (invoices as PDF)? | Note them in the ledger as not checked; build nothing until a case needs it |
| Q22 | Is the target architecture already decided for the first case, and by whom? | It must be before any unit is converted; if not, deciding it is stage 3a's first work |
| Q23 | Do we have the old system's source code? For Bouw7 web we have only a login | A migration needs code. Bouw7 web stays a test and capture site; the conversion case is the phone app |
| Q24 | Everything at once, or module by module with old and new living together (M8)? | Module by module; the ledger and the loop are built for that |
| Q25 | Which agent is first: Claude Code, Copilot in VS Code, or both from the start? | Both, through the shared skills and MCP as today; prove the loop on one, then run it on the other |
| Q26 | How much may run without a person? Which checkpoints are fixed? | Fixed: target architecture, each transform rule, intended differences, acceptance per module. The rest dark |
| Q27 | Are migration packs (section 5.5) something to offer, or only our own shortcut? | Our own first; a pack is worth offering after its second migration |
| Q28 | Does the new system also get new unit tests, written by the agent, or only the parity checks? | Both: parity proves it behaves the same, unit tests keep it right afterwards |
| Q29 | Which stack pair comes first? | Whatever the converted MAUI app came from; one pair, done fully, before a second |
| Q30 | What is the fourth part? | **Answered 2026-11-14: the factory**, the agents' loops and orchestrator, shared by ordinary development and migration |
| Q31 | Comparing two runs of one scenario: in Bruinet (a test feature) or in the migrator? | Bruinet. Old against new *version* needs it as much as old against new *system*; the migrator only reads the report |
| Q32 | Separate repositories now, or separate folders in one repository until a part has its own user? | Folders with their own solutions now, as Frainet has; a repository each when someone uses a part alone |
| Q33 | Must the spec tool work with test tools other than Bruinet (plain xUnit, Playwright, Reqnroll)? | Yes, by design: it reads rule ids as text and results as files. Bruinet is the first reader, not the only one |
| Q34 | Is the migrator for .NET targets only, or any stack an agent can build? | The formats and ledger are stack-neutral; the checks need Bruinet to drive the target, so in practice what Bruinet's heads reach |

## 12. Next step

If the four parts in section 8 look right, three proposals follow, one per part that needs work:

1. **Lirenet**, the spec tool: written, `.my/lirenet/lirenet-proposal.md`, with EARS as its part
   (`.my/ears/ears-proposal.md`).
2. **Bruinet below the screen**: the behaviour record, the API head, store objects, comparing two runs.
3. **Gaminet**, the migrator: architecture descriptions, transform rules, the ledger and the loop, once Q22,
   Q23 and Q29 are answered.
