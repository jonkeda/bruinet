# Spec: TOD.03 Read a todo

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.03 Read a todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person opens one todo to read all of it: the notes, when it is due, and whether it has reached
the server. Days and times read the way their device writes them. Going back, the list is as they
left it.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.03.R1 | Tapping a row opens Todo, which shows every part of that todo. | The [flow](app/flow.md): a todo; [Todo](screens/Todo.md): What each part shows |
| TOD.03.R2 | Days and times show in the device's own form and its local time; they are kept in universal time. | The table "As shown" |
| TOD.03.R3 | The Sync section shows "Synced", "Waiting to sync" or "Local only". | [Sync state](app/todo.md) |
| TOD.03.R4 | Back returns to Todos with Show as it was left. | Example R4 |

## Tables

### As shown (R2)

One todo, made at 08:30 universal time on 26 October 2026, due on 26 October 2026.

| # | The device writes dates as | The device's place | Due shows | Created shows |
| --- | --- | --- | --- | --- |
| 1 | In the Netherlands | Amsterdam | 26-10-2026 | 26-10-2026 09:30 |
| 2 | In the United Kingdom | London | 26/10/2026 | 26/10/2026 08:30 |

A due date is a day, not a moment: it is the same day on every device.

## Examples

Every example starts on Todos.

### R4 Back keeps the list as it was

The person was looking at their finished todos; reading one does not throw that away.

```gherkin
Given the list holds "Buy milk", which is Open, and "Pay the bill", which is Done
When I pick "Done" in Show
And I open "Pay the bill"
And I go back
Then Show says "Done"
And the list holds only "Pay the bill"
```

## Not in this journey

- Changing the todo from this page: TOD.04, TOD.05, TOD.06.
- How a todo comes to be Synced: TOD.07.
