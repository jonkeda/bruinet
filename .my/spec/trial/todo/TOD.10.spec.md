# Spec: TOD.10 Look and accessibility

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.10 Look and accessibility |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

Every screen is whole, and usable by someone who does not see it or reads it large. What a
machine can check about that is checked on every run; what needs an eye is looked at by a person,
from pictures the run keeps.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.10.R1 | Every screen opens and shows the parts on its card. | The table "Screens to visit"; each screen's card |
| TOD.10.R2 | Every part a person can use has a name a screen reader reads. | The rule itself; each screen's card names the parts |
| TOD.10.R3 | Each run keeps a picture of every screen in the table, for a person to look at. | The table "Screens to visit" |
| TOD.10.R4 | In dark mode, at 200 % text size, and with the keyboard up on a small screen, every part stays readable and reachable. | Judged by a person on a device |

## Tables

### Screens to visit (R1, R3)

| # | Screen | As | Card |
| --- | --- | --- | --- |
| 1 | Todos | The list, with todos | [Todos](screens/Todos.md) |
| 2 | Todos | "Nothing to do" | Todos: What the body shows, row 4 |
| 3 | Todos | The message and Retry | Todos: What the body shows, row 1 |
| 4 | Todo | A todo with notes and a due date | [Todo](screens/Todo.md) |
| 5 | Todo form | A new todo | [Todo form](screens/Todo%20form.md) |
| 6 | Users | The list, with users | [Users](screens/Users.md) |
| 7 | User form | A new user | [User form](screens/User%20form.md) |

Rows 6 and 7 are not on the web.

## Examples

None in steps: a card says what a screen holds.

## Not in this journey

- What each screen does: the journey that uses it.
