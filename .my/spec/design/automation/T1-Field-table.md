# T1 Field table: automation

The style: [../200-Tables.md](../200-Tables.md#t1-field-table). Entry point **A, table checks**. Back to the
[readme](000-Readme.md).

## What the spec says

```markdown
## Fields: Department form

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Name | text | ☑ | empty | 1–100 characters | Name is required |
| Branch | select | ☐ | Main office | Main office, North | |
| Active | checkbox | ☐ | on | | |
```

## What proves it, by layer

| Column | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| Kind | | | **✓** the member is a `TextRow`, `SelectRow`, `CheckRow` | |
| Required + Message | ✓ the app's own validator, if it has one | ✓ the server refuses the empty value (stub log or `service` head) | **✓** empty → the message under the field | once, for one field |
| Default | | | **✓** a new form shows it | |
| Limits | **✓** the validator at the edges | | ✓ one edge, that the message shows | |
| Choices of a select | | ✓ if the choices come from the API: the contract | **✓** offers exactly these | |

So the bold cells are the default: one generated UI check per cell, and the limits in a unit test where
the app has a validator to call.

## The simplest automation

**Nothing per field.** Bruinet's table check for fields reads the table and, for each row, finds the page
object member by the Field name (its `[BddControl]` name, then its member name, as phrases do). The page
object already exists in this shape: Bouw7's `DepartmentFormPage` has `DepartmentName` as a `TextRow`,
`Branch` as a `SelectRow`, `Active` as a `CheckRow`.

| Column | Generated check (one test each) | Uses |
| --- | --- | --- |
| Kind | The member's type is the kind's control | Reflection on the page object; no browser needed |
| Required ☑ | Open the form, leave the field empty, press the form's submit, see Message under the field; the form stays open | The screen card's submit button (P1), `FormRow`'s error text |
| Required ☐ | Submitting with it empty raises no message for this field | as above |
| Default | A new form shows the default | `TextRow.Value`, `SelectRow.Selected`, `CheckRow.IsChecked` |
| Limits (length) | The longest allowed value is kept; one longer is refused with Message | Typing through the member |
| Limits (choices) | The select offers exactly these, in this order | `SelectRow` options, `AssertExactly` |

Each test is named `<spec> · <form> · <field> · <column>` and carries the trait `Rule = <spec>.F.<Field>`,
so `lirenet trace` shows a state per field.

**Where the form opens and how it submits** comes from the screen card (P1): `Reached by` and the primary
button. The field table never says it again.

**Unit tests for limits**, where the app has a validator (Todo's `TodoValidation.ValidateTitle`): a
`[SpecTable]` theory (see T4) over the table's rows with Limits, calling the validator with the edge
values. The browser then checks only that the message is shown, once.

## What Bruinet needs

| Need | Exists? |
| --- | --- |
| Typed members per field, found by label | ✓ (Bouw7's forms) |
| Reading a field's error text and required mark | ✓ `FormRow` |
| Reading the table from the spec, binding rows to members | **New**: the table check for fields |
| A submit button and an opening page for the form | From the screen card (P1), **new** |
| Edge values from a "Limits" cell (`1–100 characters`) | **New**, a small fixed grammar: a length range, a number range, a list of choices, `date not before …` |

## Limits of the style

- Fields that depend on each other ("VAT number required when Country is not NL"): a decision table (T4)
  or a one-line rule with a scenario.
- A form whose submit has side effects (it creates a record): the required checks submit only empty or
  refused values, so nothing is created. A Default check opens the form and never submits.
- Generated checks are many but cheap: on the headless or bUnit head a field costs milliseconds; on a
  browser, group them per form so the form opens once per group.

## Questions

- One form open per check, or one per form? Proposal: per check on fast heads (isolation is free), per form
  on the browser, reset by reopening.
- Does a field missing from the page object fail the run? Proposal: yes, before anything runs, as an
  unbound step does; the message names the row and suggests the member.
