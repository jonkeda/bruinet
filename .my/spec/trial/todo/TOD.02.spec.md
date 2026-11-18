# Spec: TOD.02 Create a todo

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.02 Create a todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person writes down something they must do, in a few seconds, and finds it in their list at
once, even without a connection. A todo needs only a title; everything else can wait. The app
refuses what it cannot store or show well, and says why on the field.

## Rules

Each rule holds however a todo is created. A rule is said once: by a row of the
[Todo form](screens/Todo%20form.md), by a table below, or by an example below.

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.02.R1 | A new todo's form opens with every field at its default and shows no message. | Todo form: Default |
| TOD.02.R2 | The title is required. | Todo form: Title, Required and Message |
| TOD.02.R3 | The title is at most 100 characters, not counting spaces at either end. | Todo form: Title, Limits and Message; the table "As typed" |
| TOD.02.R4 | If a save is refused, then the app shall keep nothing, leave the form open with what was typed, and show the message under the title. | Example R4 |
| TOD.02.R5 | A message shows only after Save; typing in the title takes it away. | Example R5 |
| TOD.02.R6 | Spaces at either end of the title and notes are dropped; notes of only spaces count as no notes. | The table "As typed" |
| TOD.02.R7 | The due date is optional; with the switch off at Save, the todo has no due date, whatever day was picked. | The table "Due date" |
| TOD.02.R8 | The status may be changed before the first save; the todo keeps the status shown. | Example R8 |
| TOD.02.R9 | When a new todo is saved, the app shall keep it on the device and show "Local only" until the next sync. | Example R9 |
| TOD.02.R10 | A successful save returns to the list, which shows the new todo beside the ones already there. | The [flow](app/flow.md): Save; example R10 |
| TOD.02.R11 | A due date before today is allowed; the todo shows "Overdue" until it is Done. | The table "Due date" |
| TOD.02.R12 | With the switch on and no day picked, the due date is today, the day the form shows. | The table "Due date"; Todo form: Due date's day, Default |

## Tables

### As typed (R2, R3, R6)

What is kept is what the person meant, not the spaces around it. A title fits on one line of the
list.

| # | Title typed | Notes typed | Saved | Title kept | Notes kept | Message under the title |
| --- | --- | --- | :---: | --- | --- | --- |
| 1 | `Buy milk` | | ☑ | Buy milk | none | |
| 2 | `  Buy milk  ` | `   ` | ☑ | Buy milk | none | |
| 3 | `Buy milk` | `  for the weekend  ` | ☑ | Buy milk | for the weekend | |
| 4 | | | ☐ | | | Title is required |
| 5 | `   ` | | ☐ | | | Title is required |
| 6 | a tab and a new line | | ☐ | | | Title is required |
| 7 | 100 × `x` | | ☑ | 100 × `x` | none | |
| 8 | two spaces, 100 × `x`, two spaces | | ☑ | 100 × `x` | none | |
| 9 | 101 × `x` | | ☐ | | | Title can be at most 100 characters |

### Due date (R7, R11, R12)

Someone may switch the due date on and off again, or write down what they already missed. Today
is 26 October 2026. A dash is any day, also one picked while the switch was on.

| # | Switch at Save | Day picked | Due date kept | Shows "Overdue" |
| --- | --- | --- | --- | :---: |
| 1 | off | — | none | ☐ |
| 2 | on | 1 November 2026 | 1 November 2026 | ☐ |
| 3 | on | 20 October 2026 | 20 October 2026 | ☑ |
| 4 | on | none | 26 October 2026 | ☐ |

## Examples

Every example starts on a new todo's form, reached as the [flow](app/flow.md) says.

### R4 A refused save keeps nothing

The person can fix the title and try again; what they typed is still there, and the list has not
changed.

```gherkin
Given the list holds "Buy milk"
When I type "   " as the title
And I type "for the weekend" as the notes
And I press Save
Then I am still on the form
And the notes still say "for the weekend"
And the list holds only "Buy milk"
```

### R5 A message shows only after Save

The form does not complain while the person is still typing. Once a message is shown, the next
key in the title takes it away.

```gherkin
Given the form shows "Title is required"
When I type "B" in the title
Then no message is shown
```

### R8 The status can be set before saving

Someone writing down what they are already doing can say so straight away.

```gherkin
When I press Next on the status
And I type "Busy" as the title
And I press Save
Then "Busy" is In progress
```

### R9 A new todo is kept on the device first

The todo is there even without a connection, and the person can see it has not reached the
server yet.

```gherkin
Given no connection to the server
When I type "Buy milk" as the title
And I press Save
Then the list shows "Buy milk"
And "Buy milk" shows "Local only"
```

### R10 A save returns to the list

After saving, the person sees their new todo among the others, without searching for it.

```gherkin
Given the list holds "Buy milk"
When I type "Write the report" as the title
And I press Save
Then I am on the list
And the list shows "Write the report" and "Buy milk"
```

## Choices

What was decided to leave free, so nobody builds a limit for it.

- A todo's notes have no length limit.
- Two todos may have the same title.

## Not in this journey

- Editing, changing the status later, deleting: TOD.04, TOD.05, TOD.06.
- Sending the todo to the server: TOD.07; R9 only says it waits for a sync.
- How a todo looks in the list, its order and markers: TOD.01.
- When exactly a todo is overdue, and for which statuses: TOD.05; the "Due date" table has one row of it.
- Giving a todo to someone: not planned yet.
