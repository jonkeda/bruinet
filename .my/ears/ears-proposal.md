# Proposal: EARS rules in Lirenet's specs

Status: **proposal**, for discussion. Date: 2026-11-14.

| | |
| --- | --- |
| What | EARS (Easy Approach to Requirements Syntax, Mavin et al., Rolls-Royce 2009) as the sentence shape of a rule in a Frainet spec, checked without a model, and carried through to Bruinet's scenarios, trace and agents |
| Where it lands | **Lirenet**, the spec tool (`.my/lirenet/lirenet-proposal.md`), which is the spec code in `test-tooling/Frainet/` today. Nothing in Bruinet's packages; Bruinet's language server may show a rule on hover |
| Names, since this was written | Decided 2026-11-14: the spec tool is **Lirenet**, and Frainet is the factory. Where this text says Frainet's format, `frainet check` or `frainet trace`, read Lirenet and `lirenet` |
| Names, since 2026-11-18 | Lirenet is now Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; the commands are `bruinet spec check` and `bruinet spec trace` (AD-044, `.docs/decisions/ad-044-one-product-four-modules.md`) |
| Starts from | Frainet's own open question in `.my/develop/competitors.md`: "Should Frainet's specs follow EARS? Proposal: not yet; try it on TOD.02's rules after step 5 and keep it only if the rules read as well to a person." This proposal is that trial, written out |
| First products | Todo's TOD.02 (rewrite, appendix A), then Bouw7's first journeys (written in EARS from the start) |

## 1. In one paragraph

Frainet already asks each rule to be "one sentence about what a person can see or do, exact enough
to be false", and bans vague words. EARS gives that sentence a **fixed shape**: an optional
condition, the system, `shall`, one response. The shape is what makes a rule checkable *without a
model*: the check can tell whether a rule has a trigger, a state, an unwanted case or none, and
from that what its example and its tests must show. We make EARS an **opt-in form per spec**,
checked by `frainet check`, mapped onto the Given/When/Then the examples already use, and shown by
`frainet trace` and the coverage report per pattern, so "every error case is proven" becomes a
question a tool can answer. The spec stays in plain English for a person; EARS adds six keywords,
not a language.

## 2. EARS in one table

| Pattern | Shape | Use for |
| --- | --- | --- |
| Ubiquitous | The `<system>` shall `<response>`. | What always holds |
| Event-driven | **When** `<trigger>`, the `<system>` shall `<response>`. | What a person's action or an event causes |
| State-driven | **While** `<state>`, the `<system>` shall `<response>`. | What holds as long as a state lasts |
| Unwanted behaviour | **If** `<unwanted trigger>`, **then** the `<system>` shall `<response>`. | Errors, refusals, failures, lost connections |
| Optional feature | **Where** `<feature is present>`, the `<system>` shall `<response>`. | What holds only on a place, a configuration or a module |
| Complex | Keywords combined, in the order **Where**, **While**, **When**/**If**. | A trigger inside a state, a state on one place |

Rules of the notation that matter here: one `shall` per rule (one response), the system named the
same way in every rule, and the condition before the response.

## 3. Why EARS fits Frainet and Bruinet

| Frainet / Bruinet today | What EARS adds |
| --- | --- |
| "One sentence, exact enough to be false" is a style rule a person judges | The shape is checkable by keyword, no model: condition, system, `shall`, response |
| `should` is a banned vague word | EARS uses `shall` and nothing else; the ban and the notation agree |
| Examples are Given / When / Then | EARS maps onto them: **While** → Given, **When** / **If** → When, `shall` → Then (section 5) |
| A rule like TOD.02.R5 holds two things in one sentence | One `shall` per rule makes the check find it: R5 and R11 split (appendix A) |
| The spec's `Where` field names places (F5 in the Bouw7 proposal), Bruinet has heads | EARS **Where** names an optional feature: a place, a head, a Bouw7 module an organization has. Trace can check it on that place |
| Rights per role (F4) | A role is the subject of the trigger: "When a Reader opens an invoice, …" |
| Coverage counts rules | Coverage per pattern: are the **If** rules, the error handling, proven as well as the happy paths? |
| Kiro, the most structured competitor, writes requirements in EARS | Specs our agents write read like what agents elsewhere are trained on; no IDE or licence needed |

## 4. The spec format, changed

Only additions; a spec without them is checked as today.

1. **Header field `Rules`**: `EARS`, or left out for free sentences. A spec opts in as a whole, so
   one product can move journey by journey.
2. **Header field `System`**: the name every rule uses for the system, such as `the app` (Todo) or
   `the web app` (Bouw7). Required when `Rules` is `EARS`.
3. **A rule is one EARS sentence.** The table keeps `Id` and `Rule`; the pattern is not a column,
   because the first keyword says it and a column would drift.
4. **Keywords are in English and capitalised at the start**: `When`, `While`, `If … then`, `Where`.
   Screen texts stay in quotes in the product's own language (Dutch for Bouw7).
5. **The header field `Where` is renamed `Places`** to avoid two meanings of "Where". Old specs
   keep working; `check` accepts both for one release and says to rename.
6. **Roles.** A trigger may name an account role from the suite's `TestAccounts.md` ("When a
   Reader …"), never an account name or a credential. Ties into F4.

What a spec never holds is unchanged: no code words, no tests, no vague words.

## 5. Rule pattern ⇄ example ⇄ scenario

The examples stay as they are; the pattern only says what they must contain.

| Pattern | The example must have | Example of a finding |
| --- | --- | --- |
| Ubiquitous | Any Given / When / Then | none |
| When | A `When` step for the trigger, a `Then` for the response | `ears-example-trigger`: R10 is event-driven but its example has no When |
| While | A `Given` putting the system in the state | `ears-example-state`: R7's example never switches the due date off |
| If … then | At least one example where the unwanted trigger happens, and its `Then` | `ears-example-unwanted`: R3 only shows a title that is saved |
| Where | The example's place or feature in a `Given` or in the example's heading | `ears-example-feature`: no example says it is on the phone |

These checks match words of the rule's condition against the example's steps loosely (shared
nouns and quoted texts), and report a **warning**, not an error, until the trial shows how often
they are wrong. Bruinet's `*.feature.md` scenarios are not changed: they keep their `@TOD.02.R3`
tags, and F2 (spec examples ⇄ scenarios) gets the same pattern-aware comparison.

## 6. The check: new findings, still without a model

`frainet check` adds these when `Rules` is `EARS`. All are keyword and order checks on one
sentence, in the style of the 25 findings it has.

| Finding | When it fires |
| --- | --- |
| `ears-system` | The rule does not name the header's `System` before `shall`, or names another system |
| `ears-shall` | No `shall`, or more than one: one rule is one response, split it |
| `ears-shape` | The sentence matches no pattern: an unknown first word, a condition after `shall` |
| `ears-if-then` | `If` without `then`, or `then` without `If` |
| `ears-order` | Keywords out of order in a complex rule: Where, While, When / If |
| `ears-comma` | No comma between condition and system, so the condition's end is unclear |
| `ears-and-response` | A response that joins two verbs with "and" (warning: often one response, sometimes two) |
| `ears-system-header` | `Rules` is `EARS` and `System` is missing |
| `ears-example-*` | The four example checks of section 5, as warnings |

Frainet's tests get one broken copy of the EARS TOD.02 per finding, as they have now.

## 7. The trace and the coverage report

- `frainet trace` prints each rule's pattern beside its tests. No change in how tests name a rule
  (`[Trait("Rule", …)]`, `@<rule>`).
- The coverage report (F9) gets a pattern column and a line per pattern: "If: 3 of 4 proven".
  Unwanted-behaviour rules are where suites are thinnest, so they get their own count.
- **Where rules and heads** (with F5): a rule `Where the app runs on a phone, …` must be reached by
  a test or scenario run on a phone head (`BRUINET_HEAD`, the `Head` trait in `Bruinet.Traits`).
  A Where clause names a place from the spec's `Places`; the suite maps places to heads in one
  table, as `apps.md` maps subjects to apps. Trace fails on a Where rule proven on no matching head.
- With F1 (trace with results) the states become per pattern too: an If rule that is named by a
  test but never run is reported as such.

## 8. Bruinet: editor and agents

Nothing in the packages; the work is in the tools around them.

| Where | Change |
| --- | --- |
| Language server (`bruinet lsp`), VS and VS Code | Hover over `@TOD.02.R3` in a feature file or a `Rule` trait shows the rule's EARS sentence and pattern; go to the rule from the tag (part of F10) |
| MCP server | A tool `spec_rule` returning a rule's sentence, pattern and examples, so an agent writing a scenario works from the rule, not from memory |
| Skill `spec-rule` (new, in Frainet) | How to write or split a rule in EARS: the six patterns, the Todo examples, the findings and what each asks |
| `spec-from-app` (F3) | Drafts rules in EARS from recordings; anything the app does that the agent cannot place in a pattern becomes an open question with a proposal |
| `frainet review` (F7, with a model, kept apart) | Uses the patterns to ask: for each **When**, is there an **If** for when it goes wrong? For each **While**, what happens when the state ends? Answers go to Open questions |
| Orchestrator (Bouw7 proposal, section 6) | Its definition of done can say "every If rule passing" for a module, not only "every rule reached" |

## 9. Options

| | Option | For | Against |
| --- | --- | --- | --- |
| A | EARS as a writing guide only, no check | Nothing to build | Nothing holds it; drifts back to free sentences, and the dark loop gains nothing |
| **B** | **EARS opt-in per spec, checked, mapped to examples and trace** (this proposal) | Exact, model-free, migrates journey by journey; Todo and Bouw7 can differ | Two rule forms in one repo for a while |
| C | EARS required for every spec | One form | Forces a rewrite before the trial says it reads well |

**Recommendation: B**, and move to C only if Bouw7's first module reads well to its owner.

## 10. Steps

In Frainet's style: nothing ahead of a sample that needs it.

| # | Step | Done when |
| --- | --- | --- |
| 1 | Rewrite TOD.02's rules in EARS beside the current file (appendix A), with the owner | The owner says the EARS version reads as well, or says where not. Decides go / no-go |
| 2 | `spec-format.md`: the `Rules` and `System` fields, the patterns, `Places` | One section added, from what step 1 used |
| 3 | `SpecCheck`: the sentence findings of section 6 (not the example checks yet) | Every finding has a broken copy that fails; TOD.02 in EARS passes |
| 4 | Example checks of section 5, as warnings | Run on TOD.02 and on two hand-broken examples; false warnings counted |
| 5 | Trace and coverage: the pattern column, per-pattern counts | `frainet trace` on Todo shows "If: n of m" |
| 6 | Bouw7: first journey (Hours or Invoices) drafted by `spec-from-app` in EARS, roles from `TestAccounts.md` | Accepted by the owner, traced to Bouw7.Web.UITests |
| 7 | Where ⇄ heads, with F5 | A Where rule proven on the wrong head fails the trace |
| 8 | LSP hover and the MCP `spec_rule` tool | Hover on a rule tag shows its sentence |
| 9 | Decision record in `.docs/decisions/`: "a rule is one EARS sentence" (or not), and this folder deleted | Per Bruinet's docs rules: extract, then delete |

Steps 1–3 are small and answer the real question; everything after waits on step 1.

## 11. Risks

- **Stiffness.** "The app shall" reads like a contract, not like Frainet's plain voice. Mitigation:
  a short system name, and step 1 as the gate.
- **Splitting too far.** One `shall` per rule may turn 11 rules into 15. That is often right
  (appendix A's R5 and R11), but ids are never reused, so a split gets new ids and old tests are
  retagged: the trace reports the old id as unknown, which is what we want.
- **Loose example matching** gives false warnings. They stay warnings until step 4 has numbers.
- **Not everything is a sentence.** Data limits, tables of allowed values, timing across apps
  (F6). EARS covers the rule, a table under the rule can still hold the values.
- **Dutch screens, English rules.** Bouw7 owners read Dutch; the keywords stay English, the
  quoted texts Dutch. If the owner wants Dutch rules, the keywords get Dutch twins (`Wanneer`,
  `Zolang`, `Als … dan`, `Waar`, `zal`) as a later option, not now.

## 12. Open questions

- Opt-in per spec or per product? Proposal: per spec, so a product moves journey by journey; a
  product index (F8) shows which are EARS.
- Should `shall` be required, or may a rule say "the app shows"? Proposal: `shall`; it is what
  makes the response findable without a model, and it is what EARS readers expect.
- Does a Where rule need its own place list, or the heads directly? Proposal: places in the spec
  (no code words), mapped to heads in the suite; the spec must not know Bruinet's heads.
- Do the `ears-example-*` checks ever become errors? Proposal: decide after step 4's numbers.
- Should Frainet also read Kiro's `requirements.md`? Proposal: no, until a product of ours uses
  Kiro (same answer as for Spec Kit in `competitors.md`).

## Appendix A. TOD.02's rules in EARS (draft for step 1)

`System: the app`. Splits get new ids; the old id is retired.

| Id | Pattern | Rule |
| --- | --- | --- |
| TOD.02.R1 | When | When a person presses Add on the list, the app shall open a form with an empty title, empty notes, no due date and status Open, showing no error. |
| TOD.02.R2 | If | If Save is pressed with a title that is empty or only spaces, then the app shall refuse the save with "Title is required". |
| TOD.02.R3 | If | If Save is pressed with a title of more than 100 characters, not counting spaces at either end, then the app shall refuse the save with "Title can be at most 100 characters". |
| TOD.02.R4 | If | If a save is refused, then the app shall keep nothing, leave the form open with what was typed, and show the error under the title. |
| ~~TOD.02.R5~~ | | Split into R12 and R13: it held two responses. |
| TOD.02.R6 | When | When a todo is saved, the app shall drop spaces at either end of the title and the notes, and keep notes of only spaces as no notes. |
| TOD.02.R7 | While | While the due date is switched off, the app shall save the todo with no due date, whatever date was picked. |
| TOD.02.R8 | When | When the status is changed before the first save, the app shall save the todo with the status shown. |
| TOD.02.R9 | When | When a new todo is saved, the app shall keep it on the device at once and show "Local only" on it until the next sync. |
| TOD.02.R10 | When | When a save succeeds, the app shall return to the list, showing the new todo beside the ones already there. |
| ~~TOD.02.R11~~ | | Split into R14 and R15: it held a permission and a display. |
| TOD.02.R12 | While | While Save has not been pressed, the app shall show no error on the form. |
| TOD.02.R13 | When | When a person types in the title while an error is shown, the app shall take the error away. |
| TOD.02.R14 | Ubiquitous | The app shall accept a due date in the past. |
| TOD.02.R15 | While | While a todo's due date is in the past and its status is not Done, the app shall show "Overdue" on it. |

What the rewrite showed, before any check exists:

- Two rules (R5, R11) held two responses each; one `shall` made that visible.
- Three of the eleven are unwanted-behaviour rules (R2–R4); the trace can now count error handling
  apart from the happy paths.
- R4 and R6 still join responses with "and": `ears-and-response` would warn. R4 reads as one
  outcome to a person (the save is refused, as a whole); R6 is arguably two. A question for the
  owner in step 1.
- No rule needed Where: TOD.02 holds on every place. A Where example comes with Bouw7's phone
  journeys or a rule only on Android.

## Appendix B. A Bouw7 rule, for a feel

`System: the web app`. Roles from `Bouw7/TestAccounts.md`.

| Id | Rule |
| --- | --- |
| B7.INV.01.R1 | When an Admin opens an invoice, the web app shall show "Bewerken". |
| B7.INV.01.R2 | When a Reader opens an invoice, the web app shall show no "Bewerken". |
| B7.INV.01.R3 | If the invoice list cannot be loaded, then the web app shall show "Er is iets misgegaan" and keep the filters as they were. |
| B7.HRS.01.R4 | Where the organization has the Hours module, the web app shall show Uren in the top navigation. |

These are illustrations of the shape, not Bouw7's real behaviour: step 6 drafts the real ones from
the app.
