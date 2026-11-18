# Automating each spec style, the simplest way

Status: **proposals**, for discussion. Date: 2026-11-18. One document per spec style of
[../200-Tables.md](../200-Tables.md), [../300-Pictures.md](../300-Pictures.md) and
[../400-Sentences.md](../400-Sentences.md): how a spec written in that style becomes tests, with Bruinet
and, where it is simpler, plain unit or integration tests. Nothing here is built yet; what exists
today is said so, with the file or guide it is in.

**Names since this was written** (2026-11-18): Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet
**Migrate** ([AD-044](../../../../.docs/decisions/ad-044-one-product-four-modules.md)); `lirenet check` and `lirenet trace` are
`bruinet spec check` and `bruinet spec trace`. The text below keeps the names it was written with.

## What exists already

| Piece | What it gives | Where |
| --- | --- | --- |
| **Runnable specs** | A spec's examples run as scenarios on Bruinet's BDD engine, named and tagged with their rule; each case carries the traits `Rule` and `Spec` | `docs/guides/runnable-specs.md` |
| **Typed page objects** | A member per field, typed by its kind (`TextRow`, `SelectRow`, `CheckRow`), a member per button, typed rows for a list; `[BddControl]` names them for steps | Bouw7's `Modules/*/Pages`, `docs/guides/step-definitions.md` |
| **Built-in phrases** | `{control} says {value}`, `{control} is shown`, `I tap {control}`, `I type {value} in {control}`, `the {page} page is open`, named-user forms | step-definitions guide |
| **Tables in steps** | A step's Markdown table, compared in one go: `table.AssertExactly(items)`, `BddSets.AssertSet` | step-definitions guide, "Tables" |
| **Named data tables** | `## Data: <name>` tables are parsed into the document model, **but nothing consumes them yet** | step-definitions guide, "Tables" |
| **States and test sets** | `[BddState]` puts the app somewhere without clicking; test sets are strict, typed JSON | step-definitions guide, `docs/guides/test-data.md` |
| **Heads** | The same scenario on a browser or device, headless (MAUI), bUnit (Blazor), or the `service` head below the UI | README, `features-below-the-ui.md` |
| **Below the UI** | API contracts against the DTO the app reads; HTTP and SQL replay; a stub backend whose request log a test reads | `contracts.md`, `http-replay.md`, `sql-replay.md` |
| **Several users and apps** | `Daan taps Save` runs on Daan's app; accounts by name from `TestAccounts.md` | step-definitions guide, `test-accounts.md` |
| **The trace** | `lirenet trace … --results` gives each rule its state from the test results | `Lirenet/README.md` |

## The idea: three ways in, one rule id

Every style is automated through one of three entry points, the simplest that fits:

| Entry point | For | What the suite writes |
| --- | --- | --- |
| **A. Table checks** (new) | Tables whose columns have a fixed meaning: field table, list table, rights matrix, screen card, flow, state diagram | **Nothing per row.** Bruinet reads the table and runs one generic check per cell, against the page object member of the same name |
| **B. Spec scenarios** (exists) | Gherkin examples, and example tables used as outlines | Phrases, once per control kind; most are built in |
| **C. Spec-fed unit tests** (new, small) | Decision tables, example tables and state tables whose logic lives in code without a screen | A `[Theory]` whose data attribute reads the table from the spec file |

All three name the rule id (`Rule` trait or `@` tag), so `lirenet trace` sees them the same way. A table
check's test is named after its row and column (`B7.MST.02 · Department form · Name · required`), so the
trace can show a row's state, not only the rule's.

**A** is the one new mechanism that pays for most styles: a `TableCheck` per style, a small class that
knows what a column means and which page object members to look for. It lives in Bruinet's BDD engine
beside the spec runner, consumes the `## Data:`-style tables that are parsed today and used by nothing,
and adds a case per row to the same scenario test class.

## Which layer proves what

The simplest test is the lowest layer that can make the rule false.

| Layer | Bruinet head or tool | Proves | Cost per test |
| --- | --- | --- | --- |
| **Unit** | xUnit, no Bruinet | Pure logic: a validation, a calculation, a state machine, a decision | Microseconds |
| **Integration** | `service` head; contracts; stub backend; SQL and HTTP replay | What is stored and sent: a sync, a refusal by the server, a write | Milliseconds |
| **UI, fast** | `headless` (MAUI), `bunit` (Blazor) | What a screen shows and offers, without a device | Tens of ms |
| **UI, judge** | Browser (Playwright), device (FlaUI, Appium) | The same, on the real thing, at the gate | Seconds |

A rule is proven **once**, at the lowest layer that can prove it, and **seen once** at the judge head
where a person would notice it broken. The style documents say which.

## The documents

| Style | Document | Entry point | Lowest layer |
| --- | --- | --- | --- |
| T1 Field table | [T1-Field-table.md](T1-Field-table.md) | A | UI fast; the rule itself as a unit test |
| T2 List table | [T2-List-table.md](T2-List-table.md) | A | UI fast; sort and filter in unit |
| T3 Rights matrix | [T3-Rights-matrix.md](T3-Rights-matrix.md) | A | Integration (the API), then UI |
| T4 Decision table | [T4-Decision-table.md](T4-Decision-table.md) | C | Unit |
| T5 Message list | [T5-Message-list.md](T5-Message-list.md) | none: checked by Lirenet, used by A and B | — |
| T6 State transitions | [T6-State-transitions.md](T6-State-transitions.md) | C, then A | Unit |
| T7 Example table | [T7-Example-table.md](T7-Example-table.md) | B (outline) or C | Unit or UI |
| P1 Screen card | [P1-Screen-card.md](P1-Screen-card.md) | A | UI fast |
| P2 Sketch / Wireframe | [P2-Sketch-and-Wireframe.md](P2-Sketch-and-Wireframe.md) | A, through the screen card | UI fast |
| P3 Annotated screenshot | [P3-Annotated-screenshot.md](P3-Annotated-screenshot.md) | none as a test; a drift report | UI judge |
| P4 Flow | [P4-Flow.md](P4-Flow.md) | A | UI fast |
| P5 State diagram | [P5-State-diagram.md](P5-State-diagram.md) | as T6 | Unit |
| P6 Domain model | [P6-Domain-model.md](P6-Domain-model.md) | none as a test; checks on test data and contracts | Integration |
| P7 Sequence diagram | [P7-Sequence-diagram.md](P7-Sequence-diagram.md) | B, mixed head | Integration, then UI |
| P8 Journey map | [P8-Journey-map.md](P8-Journey-map.md) | none: a link check | — |
| S1 Checklist | [S1-Checklist.md](S1-Checklist.md) | B, one phrase per line | UI fast |
| S2 One-line rule | [S2-One-line-rule.md](S2-One-line-rule.md) | through its proof | any |
| S3 Gherkin scenario | [S3-Gherkin.md](S3-Gherkin.md) | B (exists) | the head its tag names |
| S4 Prose | [S4-Prose.md](S4-Prose.md) | none | — |

## What would be built, in order

| # | Piece | Unlocks | Size |
| --- | --- | --- | --- |
| 1 | **`SpecTable` data attribute** for xUnit: `[SpecTable("TOD.05.spec.md", "Overdue")]` gives a theory one case per row, named and traited with the rule | T4, T6, T7 as unit tests | Small |
| 2 | **Table checks** in the spec runner, starting with the field table and the screen card | T1, P1, then T2, T3, P4 | Medium; each further style is small |
| 3 | **Lirenet reads the new tables** (their form, their ids, and the cross-file checks: a field, screen or state named in one file exists in another) and the trace shows per-row states | All A styles | Small per style |
| 4 | **A Wireframe reader** that turns a `.wire` screen into the screen card and field table of 1 and 2 | P2 | Small, after `parse --json` (done in Wireframe) |
| 5 | **Drift reports**: screenshot, places list and inventory against the spec | P3, P4, P6 | Medium |

## Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Do table checks run as separate test cases, one per row, or one test per table? | One per row and column: a failure names the cell, and the trace shows it |
| Q2 | Where do table checks find the page object member for a row? | By the `[BddControl]` name, then the member's name, as phrases do; a row with no member is a binding error before anything runs, like an unbound step |
| Q3 | May a suite turn a generated check off for one row? | Yes, with a reason in the table's Note column; Lirenet reports it as skipped, not passed |
| Q4 | Is a spec-fed unit test a Bruinet feature or a Lirenet one? | Lirenet's: it needs only the spec file and xUnit, so a team without Bruinet uses it too |
| Q5 | Which head runs table checks by default? | The fast head of the stack (headless, bUnit); the judge head at the gate, as for scenarios |
