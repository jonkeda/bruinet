# T4 Decision table: automation

The style: [../200-Tables.md](../200-Tables.md#t4-decision-table). Entry point **C, spec-fed unit tests**:
the table is the test data. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## Decision: Overdue

| Status | Due date | Shows Overdue |
| --- | --- | :---: |
| Open | before today | ☑ |
| Open | today | ☐ |
| Open | none | ☐ |
| In progress | before today | ☑ |
| Done | before today | ☐ |
```

## What proves it, by layer

| | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| Every row | **✓** the function that decides | | | |
| That the screen shows the outcome at all | | | ✓ one row ☑, one row ☐ | once |

A decision is logic; logic is a unit test. The screen needs two rows, to show it reads the answer.

## The simplest automation

**A theory fed by the spec.** A small xUnit data attribute reads the named table from the spec file and
gives one case per row. The test maps the words to the app's types once:

```csharp
[Theory]
[SpecTable("specs/TOD.05.spec.md", "Overdue")]   // rows of "## Decision: Overdue", Rule = TOD.05.D.Overdue
public void Overdue(SpecRow row)
{
    var todo = Todos.With(
        status: row.Enum<TodoStatus>("Status"),
        due: row.RelativeDate("Due date", today: Clock.Today));     // "before today", "today", "none"
    Assert.Equal(row.Tick("Shows Overdue"), todo.IsOverdue(Clock.Today));
}
```

- `SpecTable`, `SpecRow` and its helpers are the new piece; `Todos.With`, `Clock` and `IsOverdue` stand for
  whatever the app's own types and function are.
- Each case is named by its row (`Overdue [Open, before today]`) and carries the trait `Rule`, so the trace
  shows a state per row.
- The test reads the spec at run time: change a row in the spec, and the test changes. There is no copy of
  the table in C# to drift.
- Todo already tests this rule this way by hand (TOD.05.2, "Rule table: status × due × today",
  `[Theory]`); `SpecTable` replaces the hand-copied `InlineData`.

**The screen**: two scenarios in the spec's examples (one row that shows Overdue, one that does not), run
by the existing spec runner on the fast head with a pinned clock (`TODO_NOW` in Todo). Not every row.

**When the decision is not in one function** (spread over a view model and the server): an integration test
on the `service` head, still fed by `SpecTable`, putting each row's state in with a `[BddState]` or a test
set.

## What it needs

| Need | Exists? |
| --- | --- |
| A `SpecTable` data attribute: read a named table from a spec, a case per row, the `Rule` trait | **New**, small; belongs to Lirenet (no Bruinet needed) |
| Cell helpers: tick, enum by name, relative dates ("before today"), numbers, "any" | **New**, small and fixed |
| A pinned clock in the app | Per app (Todo has it) |

## Checks Lirenet can do on the table, before any test

- Two rows with the same conditions and different outcomes: a contradiction.
- For conditions with a small fixed list of values, combinations no row covers: an open question.
- A dash ("any") that makes two rows overlap.

## Questions

- Where do relative values ("before today") get their meaning? Proposal: a fixed small vocabulary in
  Lirenet (today, before today, after today, none, any), so every spec says them the same way.
- Does the unit test reference the spec by path? Proposal: by spec id (`TOD.05`), found from the test
  project's configured spec folders, as runnable specs are.
