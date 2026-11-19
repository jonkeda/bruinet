# Spec: TOD.01 See my todos

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.01 See my todos |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person opens the app and sees at once what they must do: what is late first, what is finished
last. They narrow the list to one status when it is long, and see which todos have not reached
the server yet.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.01.R1 | Opening the app shows Todos; with nothing on the device to show, it shows "Loading…" until the first sync ends. | [Todos](screens/Todos.md): What the body shows, rows 2 and 3 |
| TOD.01.R2 | Each row shows the todo's title, its due date and its status. | Todos: The list |
| TOD.01.R3 | With no todos the page shows "Nothing to do", and Add stays there. | Todos: What the body shows, row 4; the card |
| TOD.01.R4 | Show narrows the list to the todos of one status. | Todos: The list, Filter; the table "Show" |
| TOD.01.R5 | The rows are ordered: not done before done, then by due date with no due date last, then by title. | Todos: The list, Sort; the table "Order" |
| TOD.01.R6 | A row whose todo is not synced shows the marker. | Todos: The list, Marker; [sync state](app/todo.md) |

## Tables

Both tables use these todos. Today is 26 October 2026.

| Todo | Status kept | Due |
| --- | --- | --- |
| Call the plumber | Open | 20 October 2026 |
| Buy milk | In progress | 1 November 2026 |
| Answer Eva | Open | none |
| ask Finn | Open | none |
| Pay the bill | Done | 20 October 2026 |
| Write the report | Done | none |

### Order (R5)

What is late comes first because its day is the earliest; a finished todo never pushes an open one
down.

| Place | Todo | Why there |
| --- | --- | --- |
| 1 | Call the plumber | Not done, the earliest day |
| 2 | Buy milk | Not done, a later day |
| 3 | Answer Eva | Not done, no day; "Answer" before "ask" |
| 4 | ask Finn | Not done, no day; capitals are ignored |
| 5 | Pay the bill | Done, with a day |
| 6 | Write the report | Done, no day |

### Show (R4)

| # | Show | Rows, in order |
| --- | --- | --- |
| 1 | All | All six, as in "Order" |
| 2 | Open | Call the plumber · Answer Eva · ask Finn |
| 3 | In progress | Buy milk |
| 4 | Done | Pay the bill · Write the report |

Call the plumber shows "Overdue" and is listed under Open: Show goes by the status kept.

## Examples

None in steps: nothing in this journey happens in an order that matters.

## Choices

- The order cannot be changed by the person.
- There is no search.

## Not in this journey

- When a todo shows "Overdue": TOD.05.
- What the page shows when a sync fails: TOD.08.
- What makes a todo synced: TOD.07.
