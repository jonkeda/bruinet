# Trial: the Todo sample in mixed styles

Status: **a trial**, for discussion. Date: 2026-11-18. The example specs of
[510-Todo-choice.md](../../design/510-Todo-choice.md): every journey of the Todo sample, TOD.01 to
TOD.11, written in the chosen styles. Specs only: no test, no check and no reader is built for
them yet.

## The files

| File | Holds | Styles |
| --- | --- | --- |
| [screens/Todos.md](screens/Todos.md) | The list page | Screen card, list table, decision table, state diagram |
| [screens/Todo.md](screens/Todo.md) | One todo | Screen card |
| [screens/Todo form.md](screens/Todo%20form.md) | The form, new and edit | Screen card, field table |
| [screens/Users.md](screens/Users.md) | The users list | Screen card, list table |
| [screens/User form.md](screens/User%20form.md) | The user's form, twelve fields | Screen card, field table |
| [app/flow.md](app/flow.md) | The ways between the five screens | Flow |
| [app/messages.md](app/messages.md) | Every text the app shows | Message list |
| [app/todo.md](app/todo.md) | A todo's status, Overdue, its sync state | Two state diagrams, decision table |
| [app/sync.md](app/sync.md) | A sync: the order, what is sent, the merge, what a failure shows | Sequence diagram, three decision tables |
| [TOD.01](TOD.01.spec.md) See my todos | 6 rules | Two example tables |
| [TOD.02](TOD.02.spec.md) Create a todo | 12 rules | Example table, decision table, 5 scenarios |
| [TOD.03](TOD.03.spec.md) Read a todo | 4 rules | Example table, 1 scenario |
| [TOD.04](TOD.04.spec.md) Edit a todo | 5 rules | Decision table, 2 scenarios, a one-line rule |
| [TOD.05](TOD.05.spec.md) Change status | 5 rules | 2 scenarios |
| [TOD.06](TOD.06.spec.md) Delete a todo | 5 rules | Decision table, 1 scenario, one-line rules |
| [TOD.07](TOD.07.spec.md) Sync with the server | 15 rules | Example table of devices, 4 scenarios with named people, one-line rules |
| [TOD.08](TOD.08.spec.md) Things go wrong | 8 rules | One-line rules (`If … then …`), 2 scenarios |
| [TOD.09](TOD.09.spec.md) Persistence and startup | 4 rules | One-line rules, 2 scenarios |
| [TOD.10](TOD.10.spec.md) Look and accessibility | 4 rules | Example table of screens to visit |
| [TOD.11](TOD.11.spec.md) Manage users | 10 rules | Two decision tables, two example tables, 4 scenarios |

Every journey has prose for its intent, and a rules table whose third column, "Said by", names the
row, table or example that says the rule.

## Why it is here and not in the sample

The accepted spec, `samples/Todo/specs/TOD.02.spec.md`, is not touched. It is the fixture of
`tests/Bruinet.Spec.Tests/SpecCheckTests.cs` (nineteen broken copies are made from its exact text),
the example of `docs/spec/spec-format.md`, and the owner accepted it on 2026-10-26. The new styles
are not in the format yet, so these files in `samples/Todo/specs/` would fail the check and that
test. When the styles are designed and the check reads them, this folder moves to
`samples/Todo/specs/` and is deleted here.

## Where the facts come from

- **The rules** are the subtasks of `samples/Todo/journeys.md`: rule `TOD.05.R2` is subtask
  `TOD.05.2`, for every journey but TOD.02, whose ids are the accepted spec's (R1 to R11, and R12
  from the trial). Tests keep their `Journey` trait; nothing is renumbered (510, Q1).
- **The limits, messages, orders and states** are read from the app's code: its rules, view
  models and pages. **No example was run on the app**: every spec is a `draft` until that is done
  and the owner accepts it.
- **To check on the app first**: the Dutch and British day and time in TOD.03's table "As shown",
  which are what those devices are expected to write, not what was seen.

## The count

| | |
| --- | --- |
| Journeys | 11, with 78 rules |
| Lines, the eleven journey files | 928 |
| Lines, the five screens | 239 |
| Lines, the four app files | 263 |
| Gherkin step lines | 137, in 23 examples |
| Screen cards · field tables · list tables | 5 · 2 · 2 |
| Decision tables | 10 |
| Example tables | 8 |
| State diagrams · flow · sequence diagram · message list | 3 · 1 · 1 · 1 |

For TOD.02 alone, against the accepted spec: 209 lines became 146, and 73 Gherkin step lines
became 24. With its share of the form and the flow it is longer; those two files serve TOD.04 too.

## What writing them showed

- **Three tables do most of the app-wide work.** "What is sent", "The merge" and "When a sync
  fails" in [app/sync.md](app/sync.md) say rules of TOD.06, TOD.07, TOD.08, TOD.09 and TOD.11. In
  `journeys.md` those are single lines in five places.
- **A journey can be nearly empty.** TOD.05 and TOD.10 are little more than a rules table that
  points elsewhere. That is the question of the design readme: per screen for what a screen is,
  per journey for what a person does. It works, but a reader of TOD.05 opens three files.
- **Tables found what the sentences left out**, four times:
  - TOD.02: the due date switch on with no day picked (now R12).
  - TOD.04: adding only spaces to the title is not a change, so Cancel does not ask.
  - TOD.05: Next on an overdue todo moves on from the status kept, so it still says "Overdue".
  - TOD.11: a user On leave with a passed end date shows "On leave", not "Expired".
- **One scenario serves three rules.** TOD.07's R11, R12 and R15 are one scenario and a table of
  device pairs. Nothing in the format says a scenario may run once per row of a table beside it.
- **Scenarios with named people read well** (`Daan presses Sync`, `Finn's list shows …`), and are
  the only place where more than four steps are needed.
- **Some rules have no simple proof in a spec.** "Kept whole or not at all", "the app and the
  server agree on what a todo is", "judged by a person on a device": the sentence is the spec, and
  "Said by" says "the rule itself". The trace must take a test's `Rule` trait as their proof.

## What the check says

`bruinet spec check` on this folder reads the eleven journey files and gives 71 findings, of five
kinds. The screens and app files are not specs to it: it has no form for them. Each kind is
something the format must decide.

| Finding | Count | Why | To decide |
| --- | --- | --- | --- |
| `rules-table` | 11 | The third column, "Said by" | Is it written, or filled by the trace? |
| `example-unknown` | 23 | Follows from the first: the rules are not read | Goes away with the first |
| `section-unknown` | 8 | `## Tables` | A section for tables, or tables under Examples |
| `code-word` | 20 | A typed value in backticks, so its spaces show | Backticks for a typed value in a table cell |
| `gherkin-order` | 9 | A second When after a Then: the page shows the message, the server works again, Retry, the list | Allow it where something is seen between two actions, or ask for two examples |

Not found, but true: the format asks for an example per rule, and most rules here have none. That
is the point of the trial; the check stops at the rules table before it gets there.

## Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | The "Said by" column: keep it? | Keep it, written by hand, checked by the tool: it names a part of the spec, never a test, and a reader finds a rule's proof without searching. |
| Q2 | Examples start with a sentence: "Every example starts on Todos." Is that a Background? | Yes: a Background step the suite binds to the flow, so the sentence can be false. |
| Q3 | May a table sit beside a scenario and run it once per row (TOD.07's "Devices")? | Yes, as an outline: the table's columns are the scenario's names. |
| Q4 | A rule said by a table in another file (`app/sync.md`, row 4): how does a test name it? | By the rule's id; the row is in the test's name, as for a field table. |
| Q5 | A state diagram uses names with no spaces (`InProgress`, `LocalOnly`) because Mermaid asks for it. | A table under the diagram gives each its words on the screen, as `app/todo.md` does. |
| Q6 | Do the tests that carry TOD.02's rule ids change? | No. They keep their ids; generated tests will carry the same ids plus the row. |
