# T5 Message list: automation

The style: [../200-Tables.md](../200-Tables.md#t5-message-list). **No tests of its own.** It is the
vocabulary the other styles' tests use, checked by Lirenet. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## Messages

| When | Where | Message |
| --- | --- | --- |
| Name empty on Create | under Name | Name is required |
| The server cannot be reached | a bar at the top | No connection. Your changes are kept on this device. |
```

## Why no tests of its own

A message is only right in a situation: "Name is required" under Name after Create on an empty form. The
situation is what the field table (T1), a one-line rule (S2) or a scenario (S3) describes, and their tests
already check the words. A test that only checks "this text exists somewhere" proves nothing a person
cares about.

## What is automated

| Check | By | Layer |
| --- | --- | --- |
| Every quoted message in a rule, a field table's Message column or an example is in the list | Lirenet `check` | none (text) |
| Every message in the list is used by at least one rule, field or example | Lirenet `check`, a warning | none |
| A message's row is proven: some test that checks its words passed | Lirenet `trace`, from the tests that check those words (the field table's generated tests name the message row too) | as those tests |
| The app's own message resources hold the same words | **Optional**, a unit test reading the app's resource file (`.resx`, `en.json`) and comparing it with the list | Unit |

The last one is the only real test, and it is cheap: one theory fed by `SpecTable` (see T4) over the list,
asserting each message exists in the app's resources. It catches a reworded message before any UI test
runs, and it is how translations are kept complete: one column per language, one row per message.

## What it needs

| Need | Exists? |
| --- | --- |
| Lirenet reading the list and cross-checking quotes | **New**, small |
| `SpecTable` for the resource test | as T4 |
| Knowing where the app keeps its messages | Per app; the suite names the file |

## Questions

- Should Where be checked ("under Name", "a bar at the top")? Proposal: only through the field table and
  scenarios, which already say where; in the list it is for the reader.
- Messages with values ("3 todos deleted")? Proposal: a placeholder in the list (`{count} todos deleted`);
  a test matches it as a pattern.
