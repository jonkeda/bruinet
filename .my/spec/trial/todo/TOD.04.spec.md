# Spec: TOD.04 Edit a todo

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.04 Edit a todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person corrects a todo: a better title, a new day, more notes. The form is the one they made it
with, so nothing is new to learn. They cannot lose a change by a slip of the finger, and they are
not asked anything when there is nothing to lose.

## Rules

The [Todo form](screens/Todo%20form.md) and its rules of TOD.02 hold here too: what the title may
be, the due date switch, the messages.

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.04.R1 | Edit opens the form titled "Edit todo", with every field showing the todo's value. | The [flow](app/flow.md): Edit; Todo form: the card; example R1 |
| TOD.04.R2 | Save returns to Todo, which shows the change; Todos shows it too, and the todo is not synced. | The flow: Save; [sync state](app/todo.md); example R2 |
| TOD.04.R3 | Cancel on a form with nothing changed leaves without asking. | The table "Cancel" |
| TOD.04.R4 | Cancel on a changed form asks "Discard changes?"; "Keep editing" stays with what was typed, "Discard" leaves the todo as it was. | The table "Cancel" |
| TOD.04.R5 | When a todo is saved, the app shall set Updated to that moment and leave Created as it was. | The rule itself |

## Tables

### Cancel (R3, R4)

A dash is not asked.

| # | On the form | Answer | Then |
| --- | --- | --- | --- |
| 1 | Nothing changed | — | Back on Todo; the todo as it was |
| 2 | Only spaces added at either end of the title or the notes | — | Back on Todo; the todo as it was |
| 3 | Something changed | Keep editing | Still on the form, with what was typed |
| 4 | Something changed | Discard | Back on Todo; the todo as it was |

A change is a different title, notes, due date or status from what the form opened with.

## Examples

Every example starts on Todo, for the todo "Buy milk": Open, no notes, no due date, synced.

### R1 The form opens with the todo in it

The person changes what is there; they do not type it again.

```gherkin
When I press Edit
Then the page is titled "Edit todo"
And the title says "Buy milk"
And the status is Open
And no message is shown
```

### R2 A saved change shows everywhere

After saving, the person sees the change where they came from, and sees it has not reached the
server yet.

```gherkin
When I press Edit
And I type "Buy oat milk" as the title
And I press Save
Then I am on Todo
And the title says "Buy oat milk"
And Sync says "Waiting to sync"
When I go back
Then the list shows "Buy oat milk" with the marker
```

## Choices

- Two people editing the same todo on two devices are not warned; the later save wins at the next sync (TOD.07).

## Not in this journey

- Changing only the status, without the form: TOD.05.
- Deleting: TOD.06.
- What the title, notes and due date may be: TOD.02.
