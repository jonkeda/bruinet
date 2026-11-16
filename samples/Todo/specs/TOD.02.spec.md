# Spec: TOD.02 Create a todo

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.02 Create a todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | accepted 2026-10-26 |

## Intent

A person writes down something they must do, in a few seconds, and finds it in their list at
once, even without a connection. A todo needs only a title; everything else can wait. The app
refuses what it cannot store or show well, and says why on the field.

## Rules

Each rule holds however a todo is created.

| Id | Rule |
| --- | --- |
| TOD.02.R1 | A new todo's form opens with an empty title, empty notes, no due date and status Open, and shows no error. |
| TOD.02.R2 | The title is required: empty or only spaces is refused with "Title is required". |
| TOD.02.R3 | The title is at most 100 characters, not counting spaces at either end; longer is refused with "Title can be at most 100 characters". |
| TOD.02.R4 | A refused save keeps nothing, leaves the form open with what was typed, and shows the error under the title. |
| TOD.02.R5 | An error shows only after Save; typing in the title takes it away. |
| TOD.02.R6 | Spaces at either end of the title and notes are dropped; notes of only spaces count as no notes. |
| TOD.02.R7 | The due date is optional; when it is switched off, the todo has no due date, whatever date was picked. |
| TOD.02.R8 | The status may be changed before the first save; the todo keeps the status shown. |
| TOD.02.R9 | A new todo is kept on the device straight away and shows "Local only" until the next sync. |
| TOD.02.R10 | A successful save returns to the list, which shows the new todo beside the ones already there. |
| TOD.02.R11 | A due date in the past is allowed; the todo's status shows "Overdue" until it is Done. |

## Examples

### R1 A new todo starts empty

Pressing Add on the list opens a form with nothing filled in, ready to type a title. The status
starts at Open and no error is shown yet.

```gherkin
Given the list
When I press Add
Then the title is empty
And the status is Open
And there is no due date
And no error is shown
```

### R2 The title is required

A todo without a title cannot be found again, so the form refuses it. Spaces alone, or a tab and
a new line, count as empty.

```gherkin
Given a new todo's form
When I type "   " as the title
And I press Save
Then I see "Title is required" under the title
```

### R3 The title is at most 100 characters

A title fits on one line of the list. Exactly 100 characters is saved; 101 is refused. Spaces
typed before or after the title don't count.

```gherkin
Given a new todo's form
When I type a title of 101 x's
And I press Save
Then I see "Title can be at most 100 characters" under the title

Given a new todo's form
When I type two spaces, 100 x's and two spaces as the title
And I press Save
Then the list shows a todo of 100 x's
```

### R4 A refused save keeps nothing

The person can fix the title and try again; what they typed is still there, and the list has not
changed.

```gherkin
Given the list holds "Buy milk"
And a new todo's form
When I type "   " as the title
And I type "for the weekend" as the notes
And I press Save
Then I am still on the form
And the notes still say "for the weekend"
And the list holds only "Buy milk"
```

### R5 An error shows only after Save

The form does not complain while the person is still typing. Once an error is shown, the next
key in the title takes it away.

```gherkin
Given a new todo's form showing "Title is required"
When I type "B" in the title
Then no error is shown
```

### R6 Spaces at the ends are dropped

What is kept is what the person meant, not the spaces around it. Notes of only spaces are no
notes at all.

```gherkin
Given a new todo's form
When I type "  Buy milk  " as the title
And I type "   " as the notes
And I press Save
Then the list shows "Buy milk"
And the todo has no notes
```

### R7 The due date is optional

Switching the due date on and off again leaves the todo without one, even if a date was picked
in between. With the due date on, the picked day is kept.

```gherkin
Given a new todo's form
When I switch the due date on
And I pick 1 November 2026
And I switch the due date off
And I type "No deadline" as the title
And I press Save
Then "No deadline" has no due date

Given a new todo's form
When I switch the due date on
And I pick 1 November 2026
And I type "Hand in" as the title
And I press Save
Then "Hand in" is due on 1 November 2026
```

### R8 The status can be set before saving

Someone writing down what they are already doing can say so straight away. Next moves the status
from Open to In progress.

```gherkin
Given a new todo's form
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
And a new todo's form
When I type "Buy milk" as the title
And I press Save
Then the list shows "Buy milk"
And "Buy milk" shows "Local only"
```

### R10 A save returns to the list

After saving, the person sees their new todo among the others, without searching for it.

```gherkin
Given the list holds "Buy milk"
And a new todo's form
When I type "Write the report" as the title
And I press Save
Then I am on the list
And the list shows "Write the report" and "Buy milk"
```

### R11 A due date may be in the past

Someone may write down what they already missed. The app keeps it and makes clear it is late.

```gherkin
Given today is 26 October 2026
And a new todo's form
When I switch the due date on
And I pick 20 October 2026
And I type "Pay the bill" as the title
And I press Save
Then the list shows "Pay the bill"
And "Pay the bill" shows "Overdue"
```

## Choices

What was decided to leave free, so nobody builds a limit for it.

- A todo's notes have no length limit.
- Two todos may have the same title.

## Not in this journey

- Editing, changing the status later, deleting: TOD.04, TOD.05, TOD.06.
- Sending the todo to the server: TOD.07; R9 only says it waits for a sync.
- How a todo looks in the list, its order and markers: TOD.01.
- Giving a todo to someone: not planned yet.
