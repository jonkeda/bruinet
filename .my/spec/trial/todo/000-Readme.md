# Trial: TOD.02 in mixed styles

Status: **a trial**, for discussion. Date: 2026-11-18. Step 1 of the order in
[510-Todo-choice.md](../../design/510-Todo-choice.md): the one Todo spec that exists, rewritten in
the chosen styles, so old and new can be counted side by side.

| File | Holds | Styles |
| --- | --- | --- |
| [TOD.02.spec.md](TOD.02.spec.md) | The journey: intent, twelve rules, two tables, five examples | S4 prose, S2 one-line rule, T7 example table, T4 decision table, S3 Gherkin |
| [screens/Todo form.md](screens/Todo%20form.md) | The form, for TOD.02 and TOD.04 | P1 screen card, T1 field table |
| [app/flow.md](app/flow.md) | The ways between all five screens | P4 flow |

## Why it is here and not in the sample

The accepted spec, `samples/Todo/specs/TOD.02.spec.md`, is not touched. It is the fixture of
`tests/Bruinet.Spec.Tests/SpecCheckTests.cs` (nineteen broken copies are made from its exact text),
the example of `docs/spec/spec-format.md`, and the owner accepted it on 2026-10-26. The new styles
are not in the format yet, so a rewrite in place would fail the check and those tests. When the
styles are designed and the check reads them, this folder moves to `samples/Todo/specs/` and is
deleted here.

The rule ids are the accepted spec's, R1 to R11, none retired, and one new, R12 (below): 27 tests and scenarios carry them,
and the ids stay as they are (decided in 510, Q1).

## The count

| | Accepted spec | Trial |
| --- | --- | --- |
| Lines, the journey file | 209 | 146 |
| Lines, with the form and the flow | 209 | 219 |
| Gherkin step lines | 73 | 24 |
| Examples in Gherkin | 13, for 11 rules | 5, for 5 rules |
| Rules | 11 | 12 |
| Rules said by a table row or cell | 0 | 7 (R1, R2, R3, R6, R7, R11, R12) |
| "Given a new todo's form" | 12 times | once, as a sentence |
| Cases a reader can count | 13 | 9 rows as typed, 4 rows of due date, 5 examples |

## What the trial says

- **The journey file is under a third shorter, not half.** [500-Mixed.md](../../design/500-Mixed.md)
  guessed half. Each rule still has its line in the rules table, and each example keeps its "why"
  paragraph; only the Gherkin went.
- **Counting the form and the flow, the total is longer** (219 against 209). They pay back with the
  next journey: TOD.04 uses the same form and the same flow, and writes neither again.
- **Two thirds of the Gherkin is gone** (73 to 24 lines), and what is left is about what happens:
  a refused save, a message going away, no connection.
- **The tables say more than the examples did.** The accepted spec has two title examples for R3;
  the table has nine rows and shows the tab-and-new-line case that R2's paragraph only mentioned.
- **A table found a gap.** Writing the due date as conditions asked: switch on, no day picked,
  which day? The accepted spec has no rule for it. Answered on 2026-11-18: today, the day the form
  shows. It is R12 and row 4 of the table. The accepted spec does not have R12 yet, and no test
  names it.
- **R11 does not sit well here.** "Shows Overdue" is TOD.05's rule; the trial keeps one row of it.
  When TOD.05 has its Overdue table, that row points there.

## What the check says

`bruinet spec check` on the trial file gives fourteen findings of four kinds. Each is something the
format and the check must decide before a spec like this can be accepted:

| Finding | Count | Why | To decide |
| --- | --- | --- | --- |
| `rules-table` | 1 | The rules table has a third column, "Said by" | Is the column written, or does the trace fill it (500's question)? |
| `example-unknown` | 5 | Follows from the first: the rules are not read, so no example finds its rule | Goes away with the first |
| `section-unknown` | 1 | `## Tables` is not a section | A section for tables, or tables under Examples with the rules in their heading |
| `code-word` | 7 | A typed value in backticks, so its spaces show: `  Buy milk  ` | Backticks allowed in a table cell for a typed value, or another way to show spaces |

Not found, but true: R1, R2, R3, R6, R7, R11 and R12 have no example, which the format asks for. The
check stops at the rules table before it gets there. And the two screen files are not specs to the
check at all: it has no form for them yet.

## Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | The "Said by" column: keep it? | Keep it, written by hand, checked by the tool: it names a part of the spec, never a test, and a reader finds a rule's proof without searching. |
| Q2 | Examples start "on a new todo's form, reached as the flow says", as a sentence. Is that a Background? | Yes: a Background step the suite binds to the flow's arrow, so the sentence can be false. |
| Q3 | Do the tests named R1, R2, R3, R6, R7 and R11 change? | No. They keep their rule ids; when table checks exist, the generated tests carry the same ids plus the row. |
