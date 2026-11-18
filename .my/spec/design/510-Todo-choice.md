# The Todo sample: which spec styles I would use

Status: **a choice**, for discussion. Date: 2026-11-18. It applies the [question map](100-Questions.md)
to the whole Todo sample (`samples/Todo`, journeys TOD.01 to TOD.11), where [500-Mixed.md](500-Mixed.md)
sketched one journey. Nothing is rewritten yet; today the sample has one spec,
`samples/Todo/specs/TOD.02.spec.md`, and [journeys.md](../../../samples/Todo/journeys.md) for the rest.

## The choice

Twelve of the nineteen styles, in three groups. Seven are not used.

| Group | Style | Used for, in Todo | How many |
| --- | --- | --- | --- |
| **Carries the rules** | T1 Field table | The Todo form and the User form | 2 |
| | T2 List table | Todos and Users | 2 |
| | P1 Screen card | Every screen: list, detail, form, users, user form | 5 |
| | T4 Decision table | Overdue, the due date switch, Cancel with changes, the sync merge, a user's state | 5 |
| | T7 Example table | Title edge values, the sort order, the hourly rate as typed, dates as shown | 4 |
| | S2 One-line rule | What is kept, sent and refused: storage, sync, errors | about 25 |
| | S3 Gherkin scenario | What happens in order: create, edit, delete, sync between apps, recovery | about 20 |
| **One picture each** | P4 Flow | The five screens and the ways between them | 1 |
| | P5 State diagram | A todo's status; a todo's sync state; the list page's states | 3 |
| | P7 Sequence diagram | A sync: device, backend, second device | 1 |
| **Supports the rest** | T5 Message list | Every refusal and error text of the app | 1 |
| | S4 Prose | The intent of each journey | 11 |

The readme proposes about six styles in use. This is seven a reader must learn (the first group),
because a person reads a flow, a state diagram and a sequence diagram without learning them. If
one must go, it is the example table: the field table's limits make the 100 / 101 tests already,
and the other three could be unit tests with no table in the spec. I would keep it, for the hourly
rate: `1,250.75` is read, `1,5` is not, and no sentence says that as clearly as five rows.

## Why these

- **Field table: the largest gain.** The User form has eleven fields, nearly every one of its
  own kind, with their limits and messages. `UserValidation` has ten messages; today TOD.11.3 is one line in
  `journeys.md` ("each field refuses a bad value") and the facts are only in code. One table of
  eleven rows makes them a spec. The Todo form's four rows replace R1, R2, R3 and R8 of TOD.02.
- **Screen card: it is the Reviews gate.** TOD.10.1 and TOD.10.2 already check that every screen
  loads and its controls exist. The card says what "its controls" are, so the gate has a spec.
- **Decision table: the sample has the tests already.** The Overdue rule, the sync merge and the
  user's state are `[Theory]` tables in the unit tier. The table moves from `[InlineData]` to the
  spec, and `SpecTable` (build item 1 of the [automation plan](automation/000-Readme.md)) feeds the
  same theory.
- **One-line rule: a third of the sample has no screen.** TOD.06.4, TOD.07.7, TOD.07.8 and all of
  TOD.09 are proven at the integration or contract tier. A sentence with an id is all they need;
  Gherkin there would be a scenario with one `Then`.
- **Sequence diagram: sync is the one thing that crosses systems.** Push before pull, users before
  todos, the two headers. One picture serves TOD.07 and TOD.11.9.
- **State diagram, three times.** The status cycle is the readme's own example. The sync state
  (Local only, Waiting to sync, Synced) and the list page's states (Loading, Content, Empty, Error,
  with Retry) are each spread over three journeys today.

## What I would not use

| Style | Why not, here |
| --- | --- |
| T3 Rights matrix | Users have a role, but no role grants anything (`UserRole`: "recorded only"). The first rule that depends on a role brings it back. |
| T6 State transition table | Three states, one action, no "who" and no conditions. The diagram says it all. |
| P2 Sketch or Wireframe | Five small screens that exist on two heads. The card and field table are the fallback **4a** of [310-Screens.md](310-Screens.md); a Wireframe source needs its reader first (build item 4). Todo is a fair second trial for it, after Bouw7's department form. |
| P3 Annotated screenshot | The Reviews gate takes a screenshot per screen (TOD.10.3). I would link that beside the card, without numbers or a table. |
| P6 Domain model | Two things, Todo and User, not linked yet. A picture of two boxes says nothing. |
| P8 Journey map | No journey is longer than three screens. |
| S1 Checklist | `todo-user-journeys.md` stays as the acceptance backbone it is. As a spec style the screen card replaces it. |

## Per journey

| Journey | Styles | What each holds |
| --- | --- | --- |
| TOD.01 See my todos | Screen card, list table, state diagram, example table | The list's parts; its columns, filter and pending marker; Loading → Content / Empty; the sort order by rows in and rows shown |
| TOD.02 Create a todo | Field table, decision table, one-line rules, 2 scenarios | As in [500-Mixed.md](500-Mixed.md): four rows for the form, the due date switch, R4 and R9 as sentences, R5 and R10 as scenarios |
| TOD.03 Read a todo | Screen card, state diagram, example table, 1 scenario | The three cards of the detail; the sync state's texts; dates as shown; Back returns to the same filter |
| TOD.04 Edit a todo | The Todo form's field table again, decision table, 1 scenario, one-line rule | Changed or not × the answer → asks, stays, leaves; the save round trip; `UpdatedAt` changes, `CreatedAt` never |
| TOD.05 Change status | State diagram, decision table, list table, 1 scenario | The cycle; Overdue by status × due date × today; read-only in the row; saved from the detail without Edit |
| TOD.06 Delete a todo | 1 scenario, one-line rules | Confirm removes the row; Cancel keeps it; the tombstone; no server call for a todo that was never synced |
| TOD.07 Sync | Sequence diagram, decision table, one-line rules, scenarios with named users | The order of a sync; the newer `UpdatedAt` wins; idempotence and the headers; `Daan taps Sync` across apps |
| TOD.08 Things go wrong | One-line rules (`If … then …`), message list, the list's state diagram, 2 scenarios | One sentence per failure; the exact words; Error and Retry; recovery and offline, which have steps |
| TOD.09 Persistence and startup | One-line rules, 1 scenario | Fresh install, upgrade, restart; the app starts with the backend unreachable |
| TOD.10 Look and accessibility | The five screen cards | The cards are what the Reviews gate checks. Dark mode and font scaling stay charters. |
| TOD.11 Manage users | Field table, list table, decision table, example table, one-line rules, the sequence diagram | Eleven fields; the users list and its order; status × end date × today → Available, Expired, On leave, Inactive; the rate as typed; offline is read-only; users are fetched first |

## Where it would live

The readme's proposal: per screen for what a screen is, per journey for what a person does.

```text
samples/Todo/specs/
  screens/Todo list.md        screen card, list table, the page's state diagram
  screens/Todo detail.md      screen card
  screens/Todo form.md        screen card, field table
  screens/Users.md            screen card, list table
  screens/User form.md        screen card, field table
  app/flow.md                 the flow
  app/messages.md             the message list
  app/todo.md                 the status and sync state diagrams, the Overdue table
  app/sync.md                 the sequence diagram, the merge table
  TOD.01.spec.md … TOD.11.spec.md   intent, one-line rules, journey tables, scenarios
```

## The order I would write them in

| # | What | Why first |
| --- | --- | --- |
| 1 | `screens/Todo form.md`, `app/flow.md`, and TOD.02 rewritten | The one spec that exists: old and new can be counted side by side  **Done as a trial**, beside the accepted spec: [../trial/todo](../trial/todo/000-Readme.md) |
| 2 | `app/todo.md` and TOD.05 | The Overdue theory exists, so `SpecTable` has a real first user |
| 3 | `screens/User form.md` and TOD.11 | The biggest field table; the real test of table checks (build item 2) |
| 4 | `app/sync.md`, TOD.07 and TOD.08 | The sequence diagram and the `If … then …` rules; no new mechanism needed |
| 5 | The rest | By then the styles are settled |

## Decided

Answered on 2026-11-18.

| # | Question | Decision |
| --- | --- | --- |
| Q1 | Todo has two id systems: subtasks in `journeys.md` (`TOD.02.3`) and rules in the spec (`TOD.02.R2`). Which do rows and tests carry? | **Left as it is.** Both stay: tests keep their `Journey` trait with the subtask id, `journeys.md` and the coverage report are not changed, and a spec's rules keep their own ids. |
| Q2 | A table check runs on a UI head. Todo's rule is "all variants in U, one example in H". Does a field table then make eleven H tests for the User form? | **No.** The limits and messages bind to `UserValidation` as a spec-fed theory (U); the table check on the fast head proves only that the field exists, is of its kind, and shows its message once. |
| Q3 | Are the three state diagrams specs or pictures of a decision table? | **Specs.** Each arrow is a row for a theory, as T6 would be. |
| Q4 | The web head has no users pages. Does `screens/Users.md` say so? | **Yes.** A `Where` line per screen, as the spec header has today. |
