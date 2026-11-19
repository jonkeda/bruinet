# Spec: TOD.05 Change status

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.05 Change status |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person says how far they are with one tap: started, finished, or open again. The app shows by
itself what is late, so nobody has to mark it.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.05.R1 | Next moves the status from Open to In progress, to Done, and to Open again. | [Status](app/todo.md): the diagram |
| TOD.05.R2 | A todo that is not Done and whose due date is before today shows "Overdue". | [Overdue](app/todo.md): the table |
| TOD.05.R3 | "Overdue" shows on the todo's row on Todos and on Todo. | [Todos](screens/Todos.md): The list, Status; [Todo](screens/Todo.md): Status |
| TOD.05.R4 | The status on a row cannot be changed there: it has no Next. | Todos: The list, Status |
| TOD.05.R5 | When Next is pressed on Todo, the app shall save the new status at once, without the form. | Example R5 |

## Examples

Every example starts on Todos. Today is 26 October 2026.

### R5 Next saves by itself

One tap is the whole change; there is no Save to forget.

```gherkin
Given the list holds "Buy milk", which is Open
When I open "Buy milk"
And I press Next on the status
And I go back
Then "Buy milk" is In progress
And "Buy milk" shows the marker
```

### R2 Next on a late todo moves on from the status kept

"Overdue" is shown, not kept: a late todo that is Open goes to In progress, and is still late.

```gherkin
Given the list holds "Call the plumber", which is Open and due on 20 October 2026
When I open "Call the plumber"
Then the status says "Overdue"
When I press Next on the status
Then the status says "Overdue"
When I press Next on the status
Then the status says "Done"
```

## Choices

- A todo due today is not late.
- A Done todo is never late, however old its due date.

## Not in this journey

- Setting the status of a new todo before the first save: TOD.02, R8.
- Showing only the todos of one status: TOD.01.
