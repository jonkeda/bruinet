# T6 State transition table: automation

The style: [../200-Tables.md](../200-Tables.md#t6-state-transition-table). Entry point **C, spec-fed unit
tests** for the state machine, then **A** for what the screen offers in each state. P5, the state diagram,
is automated the same way. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## States: Invoice

| From | Action | To | Who |
| --- | --- | --- | --- |
| Draft | Send | Sent | NormalUser, Admin |
| Sent | Mark paid | Paid | Admin |
| Sent | Credit | Credited | Admin |
| Draft | Delete | (gone) | NormalUser, Admin |
```

## What proves it, by layer

| | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| Each row: from this state, this action leads there | **✓** the domain's state machine | ✓ through the API, if the server owns the states | | one path, end to end |
| What is **not** in the table is refused | **✓** every other state × action | ✓ the API refuses | | |
| The screen offers exactly the table's actions in each state | | | **✓** | |
| Who | | ✓ (see T3) | ✓ (see T3) | |

## The simplest automation

**Unit: two theories, both fed by the spec.**

```csharp
[Theory]
[SpecTable("INV.03", "States: Invoice")]          // one case per row
public void Moves(SpecRow row)
{
    var invoice = Invoices.In(row.Enum<InvoiceState>("From"));
    invoice.Apply(row.Text("Action"));
    Assert.Equal(row.Text("To"), invoice.StateName);
}

[Theory]
[SpecTableComplement("INV.03", "States: Invoice")] // every From × Action pair the table does not list
public void Refuses(string from, string action)
{
    var invoice = Invoices.In(Enum.Parse<InvoiceState>(from));
    Assert.Throws<InvalidOperationException>(() => invoice.Apply(action));
}
```

The second theory is the valuable one: a state machine that allows one move too many is the usual bug, and
nobody writes those tests by hand. `SpecTableComplement` builds the pairs from the distinct values of From
and Action. As in T4, `SpecTable` is new and the invoice types stand for the app's own.

**Screen: one table check per state.** Put a record in each From state (`[BddState]` or a test set), open
its page, and check that the actions offered are exactly that state's Actions: `AssertExactly` over the
page's action buttons. One test per state, generated from the table.

**One path at the judge head**: a scenario in the spec's examples walking Draft → Sent → Paid, so a person
sees it once on the real screen.

## What it needs

| Need | Exists? |
| --- | --- |
| `SpecTable`, and `SpecTableComplement` for the forbidden pairs | **New**, small (Lirenet) |
| Putting a record in a state | ✓ `[BddState]`, test sets; on a shared environment, only for records the run made |
| A page's action buttons as a set | Per page; **new**: a convention (`[BddControl]` on an "actions" member) the check can find |
| Reading the table into a screen check | **New**: the state table check |

## Questions

- `(gone)` and other end states: a special word? Proposal: parentheses mark a pseudo-state; Lirenet knows
  `(gone)` and `(start)`.
- Is Who checked here or only in the rights matrix? Proposal: here only as data for the screen check (sign in
  as the first role listed); the rights matrix proves the refusals.
