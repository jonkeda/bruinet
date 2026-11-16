# Generic list and dialog steps

Status: proposed, 2026-11-16. Needed by [the Contacts pilot](030-Contacts-Pilot.md) and by
[the native iOS plan](ios/000-Plan.md).

## The problem

Bruinet's text, field, button, toggle and picker steps are declared on Core's control
interfaces, so every platform whose controls implement them gets the steps for free. The
list and dialog steps are not. `{control} shows only {values}`, `is empty`, `has rows` and
`every row of {control} has {value}` are extension verbs on the Html `TableControl` only.
`should ask`, `I choose {value} in {control}` and `no {control} should be shown` are declared
on MAUI's `ContentDialog` only. So a Contacts journey that checks the contact list or answers
an alert binds on one platform and not on the others.

## The change

Move both families to Core, in the same way as the text steps: two small interfaces with the
step attributes on their methods, implemented by each platform's list and dialog.

- **A list:** `ICollectionControlObject` with the row count and each row's key text, which is
  what a person reads first in a row. The verbs `shows only`, `is empty`, `has rows` and
  `every row has` move from `TableVerbs` to this interface. Core's `CollectionObjectBase`
  implements it once, which covers MAUI's lists, native Android's `RecyclerView` and
  `ListView`, and native iOS's tables. The Html `TableControl` implements it with its key
  column.
- **A table:** `ITableControlObject` extends the list with column headers. Only
  `has the columns` stays here, because a phone list has no columns.
- **A dialog:** `IDialogControlObject` with the title and message text, the choice of a
  button by its text, and whether a dialog is shown. MAUI's `ContentDialog`, native Android's
  `AlertDialog`, native iOS's alert and action sheet, and an Html dialog implement it.

The phrases stay word for word, so no feature file changes.

## Risks

- **Row keys differ per platform.** Android keys a row by a chosen view's text, MAUI by its
  item, Html by the key column. The interface asks for "the row's key text", and each page
  object can still name a different key view where the default reads the wrong text.
- **The wait.** `shows only` must wait until the list stops narrowing after a search. The
  existing collection waits already compare item by item, so the verb uses them.

## Who does it

The Windows machine does it in the Brinell submodule on the `move` branch, as part of pilot
step 4, and adds a line to [the handoff board](ios/100-Handoff.md) when it lands. The Mac
implements the two interfaces on the native iOS controls after that.
