# Proposal: Lirenet, the spec tool

Status: **proposal**, for discussion. Date: 2026-11-14.

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with.

| | |
| --- | --- |
| What | **Lirenet** describes what a system must do, checks that description without a model, and traces it to the tests that prove it. A tool of its own: for ordinary software development as much as for agents, with Bruinet or with another test tool |
| One of four parts | **Bruinet** tests, **Lirenet** describes, **Gaminet** migrates, **Frainet** is the factory that runs the agents (`.my/lifecycle/lifecycle-proposal.md`, section 8) |
| Comes from | The spec code that lives in `test-tooling/Frainet/` today: the spec format, `check` (25 findings), `trace`, one spec (Todo's TOD.02). That code becomes Lirenet; the name Frainet stays with the factory |
| Goes with | `.my/ears/ears-proposal.md`: EARS as the sentence shape of a rule. That proposal is the detail of section 4.1 here |
| Replaces | The earlier idea of folding the spec code into Bruinet as `bruinet spec`. Decided against in discussion: Bruinet stays a test tool |

## 1. In one paragraph

Today a spec is one journey: intent, rules, examples. That is enough to say what a person can do,
but not enough to describe a system: the words the rules use are defined nowhere, the screens and
the ways between them are not written down, and what must hold for the data or at an API has no
place. Lirenet keeps the journey spec and adds what is missing: a **product** file, a **domain
model**, **screens**, **flows** and **interfaces**, all in plain Markdown a non-programmer can
read, all with ids. Rules are EARS sentences. Every check is exact and needs no model: the form
of each file, the words a rule may use, the screens a flow may name, and whether each rule is
reached by a test and passing. Lirenet reads tests and results as files, so it does not depend on
Bruinet, and a team with plain xUnit or Playwright can use it.

## 2. Who it is for

| User | Uses Lirenet for | Without |
| --- | --- | --- |
| A team writing software by hand | Specs in the repository, a check in CI, a trace that fails when a rule loses its test | Agents, Bruinet |
| A team with agents (Frainet) | Exact specs as the brief, exact checks as the feedback that lets a loop run dark | — |
| A migration (Gaminet) | The behaviour of the old system, captured once, as the fixed point of the migration | — |
| A product owner | One readable description to accept, and a report of what is proven | Reading code or tests |

## 3. What a system's description holds

One folder per product, in the product's own repository:

```text
specs/<product>/
  product.md                  the product: places, roles, the system's name, index of journeys
  model.md                    the domain model: things, relations, terms, invariants
  screens.md                  the screens: what each shows and offers        (or screens/<name>.md)
  flows.md                    how a person gets from screen to screen
  journeys/TOD.02.spec.md     one journey: intent, rules, examples            (today's spec)
  interfaces/todo-api.md      one interface: rules at an API
```

| File | Holds | New? |
| --- | --- | --- |
| `product.md` | The product's name and id prefix; its **places** (web, phone); its **roles**; the **system name** rules use; a table of journeys with their status | New (F8 in the Bouw7 proposal) |
| `model.md` | The **things** a person works with (a Todo, an Invoice, an Hours line), their fields as a person names them, and how they relate, as a Mermaid `erDiagram`; a **glossary** of terms and states ("Open", "Overdue"); **invariants**: rules that hold for all data | New |
| `screens.md` | Per screen: its name, what it is for, what it shows, what a person can do there (the buttons by their words), who can see it, a link to its design | New |
| `flows.md` | Mermaid flowcharts whose nodes are screens and whose arrows are a person's actions | New |
| A journey | As today: intent, rules, examples, choices, not in this journey, open questions. Rules in EARS | Changed: EARS, and header fields `Rules`, `System`, `Places` |
| An interface | Rules at an API in EARS, with the API as the system: "If the token has expired, then the API shall answer 401." Examples as request and answer | New |

What stays true of every file: written as if the code did not exist, in the words a person sees.
No class, table, file or test names. A domain model is **not** the database schema, a screen is
**not** a pixel design, a flow is **not** a test script.

### 3.1 Ids

Every line that can be proven or pointed at has an id that is never reused.

| Thing | Id | Example |
| --- | --- | --- |
| Journey rule | `<journey>.R<n>` | `TOD.02.R3` |
| Invariant | `<product>.M.R<n>` | `TOD.M.R2` |
| Interface rule | `<product>.I.<interface>.R<n>` | `TOD.I.API.R4` |
| Screen | Its name, unique in the product | `Todo list` |
| Term | Its word, unique in the product | `Overdue` |

Tests name rule ids, as now. Screens and terms are named by rules, not by tests.

## 4. The checks, all without a model

`lirenet check <folder>` checks each file's form and then the files against each other.

### 4.1 Per file

- **Journeys**: the 25 findings there are today, plus the EARS findings of the EARS proposal
  (`ears-system`, `ears-shall`, `ears-shape`, …) when the journey says `Rules: EARS`.
- **Product, model, screens, flows, interfaces**: the same kind of finding: title and header,
  sections and their order, ids well-formed and used once, no vague words, no code words, each
  invariant and interface rule one EARS sentence with an example.
- **Diagrams**: a Mermaid block is read only as far as needed: the names of nodes and what
  connects to what. Lirenet does not draw.

### 4.2 Across files

| Check | Fires when |
| --- | --- |
| `term-unknown` | A rule names a thing, a state or a button that `model.md` and `screens.md` do not define |
| `term-unused` | A term or a screen that no rule and no flow names (a warning: it may be planned) |
| `screen-unknown` | A flow, a journey or a rule names a screen that `screens.md` lacks |
| `screen-unreachable` | A screen no flow leads to |
| `flow-uncovered` | An arrow in a flow that no journey's examples walk, and that is not listed under "Not in this journey" anywhere |
| `journey-off-flow` | A journey's example goes from one screen to another with no arrow between them |
| `role-unknown`, `place-unknown` | A rule names a role or a place `product.md` does not list |
| `index-drift` | A journey file missing from the product's index, or an index row with no file |
| `relation-unknown` | The model's diagram relates a thing the model does not describe |

**How `term-unknown` works without a model** is the one hard part. Proposal: a rule's *names* are
its quoted texts and its words capitalised in mid-sentence ("press **Add**", "status **Open**",
"shows **Overdue**"), which is how TOD.02 already writes them. Each must be a term, a state, a
screen or an action of a screen. Ordinary words are not checked. It starts as a warning; the
model-based review (section 7) covers what a keyword check cannot see.

## 5. The trace: from a description to its proof

`lirenet trace` answers, per rule: which tests name it, and what state it is in.

| Reads | How | Depends on Bruinet? |
| --- | --- | --- |
| **Test sources**, as text | A rule id in a trait (`[Trait("Rule", "TOD.02.R3")]`) or a tag (`@TOD.02.R3`), as today. More patterns in a config table, so Playwright, pytest or Reqnroll tests can name a rule their own way | No |
| **Test results**, as files | TRX and JUnit XML, which every test tool writes; Bruinet's result JSON when present, for the head a test ran on | No |
| **Places to heads** | A table in the suite, not in the spec: place "phone" is proven by these test projects or heads | No: the spec never names a head |

States per rule (F1): **not reached**, **reached, not run**, **failing**, **passing**. With EARS,
counted per pattern too: "If: 3 of 4 passing".

`lirenet coverage` writes it as one report per product (F9): rules × state × place, in Markdown
for people and JSON for tools. Frainet's loops and Gaminet's ledger read the JSON.

## 6. The description against the running system

A description drifts from the app unless something compares them. Lirenet does not drive apps, so
it compares against **inventory files** another tool writes:

| Inventory | Written by | Lirenet reports |
| --- | --- | --- |
| Screens and the ways between them | Bruinet's Scraper (Explore), or page objects listed by Bruinet's CLI | A described screen the app does not have; a screen of the app nobody described; an arrow that exists on one side only |
| Endpoints | An OpenAPI document, or Bruinet's observed traffic | An interface rule about an endpoint that is not there; an endpoint with no rule |
| Tables | A schema snapshot | Only a report of things with no counterpart; never a failure (the model is not the schema) |

The inventory format is Lirenet's own small JSON, documented, so any tool can write it. This is
also how a description is **drafted** from a running system: the inventory first, then an agent
turns it into screens, flows and draft rules, each unclear point an open question (F3).

## 7. What needs a model, kept apart

The exact checks stay exact. Beside them, as skills an agent runs, never as part of `check`:

| Skill | Does |
| --- | --- |
| `spec-rule` | Write, split or correct a rule in EARS |
| `spec-from-app` | Draft a description from inventories and recordings; open questions for what is unclear |
| `spec-review` | Find ambiguity, missing cases and contradictions; for each **When** ask for the **If**; write findings as open questions with proposals (F7) |

## 8. The tool

| | |
| --- | --- |
| Library | `Lirenet.Specs`: the formats, the checks, the trace. No reference to Bruinet |
| Command line | `lirenet`, a .NET tool: `check`, `trace`, `coverage`, `init` (a product folder with empty files), `rule <id>` (a rule's sentence, examples and state) |
| For agents | `lirenet mcp`: the same commands as MCP tools; the skills of section 7 |
| For editors | Bruinet's language server may read Lirenet's files to show a rule on hover over `@TOD.02.R3`. An optional reader in Bruinet, not a dependency of Lirenet |
| In CI | `lirenet check` and `lirenet trace` as two steps with exit codes |

Later, from the lifecycle paper, once the above stands: acceptance recorded per rule (L5), change
proposals to an accepted spec and what a change reaches (L8), the evidence pack per release (L6).

## 9. Out of Frainet's folder

| Today | Becomes |
| --- | --- |
| `Frainet/src/Frainet.Specs` | `Lirenet/src/Lirenet.Specs` |
| `Frainet/src/Frainet.Cli` (`frainet check`, `frainet trace`) | `Lirenet/src/Lirenet.Cli` (`lirenet …`) |
| `Frainet/tests/Frainet.Specs.Tests` | `Lirenet/tests/Lirenet.Specs.Tests` |
| `Frainet/docs/spec-format.md` | `Lirenet/docs/`, one page per kind of file |
| `Frainet/specs/todo/TOD.02.spec.md` | `samples/Todo/specs/todo/journeys/TOD.02.spec.md`, beside the product |
| `Frainet/.my/` (loops, plan, the factory's research) | Stays: that is Frainet, the factory |

Lirenet starts as a folder of its own with its own solution in Bruinet's repository, as Frainet is
now, and gets its own repository when someone uses it without Bruinet.

## 10. Steps

Nothing ahead of a sample that needs it.

| # | Step | Done when |
| --- | --- | --- |
| 1 | Move and rename: the spec code out of Frainet's folder into `Lirenet/`; `lirenet` as a .NET tool | The 29 tests pass under the new names; `lirenet check` and `trace` run on Todo from an installed tool |
| 2 | EARS trial on TOD.02 (EARS proposal, steps 1–3) | The owner accepts or rejects EARS; the sentence findings exist |
| 3 | `product.md` and `model.md` for Todo, with `term-unknown` | TOD.02's rules pass; a rule with an undefined name warns |
| 4 | `screens.md` and `flows.md` for Todo, with the screen and flow checks | Todo's journeys walk the flows; a made-up screen fails |
| 5 | Trace with results: TRX and JUnit XML, states per rule, `coverage` | Todo's report shows passing, failing and not run, not only "named" |
| 6 | Bouw7's first module described: inventory from the Scraper, `spec-from-app`, the owner corrects | One module accepted, its rules traced to `Bouw7.Web.UITests` |
| 7 | A second test tool: a small suite in plain xUnit or Playwright traced by pattern | Lirenet proven without Bruinet |
| 8 | Interfaces, when Bruinet's API head has something to prove them with | One interface with rules, traced |
| 9 | Decisions recorded, this folder deleted | Per the docs rules: extract, then delete |

Steps 1–2 are small. Steps 3–5 make it a system description. Step 7 is the proof of "also for
ordinary software development".

## 11. Risks

- **Too much to write.** Six kinds of file can feel like a documentation project. Mitigation: only
  `product.md` and journeys are required; each other file is asked for by the first rule that
  needs it, and most are drafted from a running app.
- **`term-unknown` is a heuristic.** Capitalised and quoted names catch what TOD.02 does; another
  writer may write differently. It stays a warning until Bouw7 has given numbers.
- **Two sources of truth for screens**: `screens.md` and page objects. The inventory comparison
  (section 6) is what keeps them honest; without it, one will rot.
- **Mermaid as data.** Reading diagrams as text is fragile if people use the full syntax.
  Mitigation: a small documented subset, and a finding for anything outside it.
- **A second name to explain.** Frainet's notes and the Bouw7 proposal say "Frainet" for specs.
  They are updated in step 1.

## 12. Open questions

- Is the Todo spec moved beside the sample (section 9), or kept in Lirenet's folder as its own
  example? Proposal: beside the sample; Lirenet's tests keep a copy as test data.
- One `screens.md`, or a file per screen? Proposal: one file until it passes about twenty screens,
  then a folder; `check` accepts both.
- Are invariants and interface rules proven by tests like journey rules, with the same trait?
  Proposal: yes, one way to name a rule, whatever kind it is.
- Does Lirenet get its own MCP server, or do its tools sit in Bruinet's? Proposal: its own
  (`lirenet mcp`), or it is not usable without Bruinet.
- Dutch products: may terms and screen names be Dutch while the EARS keywords stay English?
  Proposal: yes; names are the screen's own words, in quotes or capitalised.
- Should `term-unknown` also read examples, not only rules? Proposal: rules first; examples in
  step 6, where Bouw7 shows whether it helps or only adds noise.
- Which result formats first? Proposal: TRX (what `dotnet test` writes) and JUnit XML; Bruinet's
  JSON only for the head.
