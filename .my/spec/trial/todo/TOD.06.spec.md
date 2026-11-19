# Spec: TOD.06 Delete a todo

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.06 Delete a todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person removes what no longer matters, and it stays gone. One tap cannot do it by accident, and
a todo the server never saw is not mentioned to the server.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.06.R1 | Delete asks "Delete todo?" and names the todo. | [Todo](screens/Todo.md): Questions the screen asks |
| TOD.06.R2 | "Delete" returns to Todos, where the todo's row is gone. | The table "The answer"; the [flow](app/flow.md): Delete |
| TOD.06.R3 | "Cancel" keeps the todo and stays on Todo. | The table "The answer" |
| TOD.06.R4 | While the app is closed and opened again, a deleted todo shall stay gone. | Example R4 |
| TOD.06.R5 | If a deleted todo was never sent to the server, then the app shall drop it without a call. | [What is sent](app/sync.md), row 3 |

## Tables

### The answer (R2, R3)

| # | Answer | Then | The todo |
| --- | --- | --- | --- |
| 1 | Delete | On Todos | Not in the list |
| 2 | Cancel | Still on Todo | As it was |

## Examples

Every example starts on Todos, which holds "Buy milk" and "Write the report".

### R4 Deleted stays deleted

A todo that came back after a restart would be worse than one that was never removed.

```gherkin
When I open "Buy milk"
And I press Delete
And I answer "Delete"
And I close the app and open it again
Then the list holds only "Write the report"
```

## Choices

- A deleted todo cannot be brought back.
- A todo is deleted from its own page only, never from its row.

## Not in this journey

- How the server hears of a deleted todo: TOD.07.
- A todo deleted on another device: TOD.07.
