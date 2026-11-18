# Screen: Todo form

| Field | Value |
| --- | --- |
| Product | Todo |
| Where | The app on Windows and Android, and the same app on the web |
| Used by | TOD.02 Create a todo, TOD.04 Edit a todo |
| Status | draft |

## Card

| Todo form | |
| --- | --- |
| Reached by | Add on Todos; Edit on Todo |
| Title | "New todo" from Add; "Edit todo" from Edit |
| Top buttons | Cancel · **Save** |
| Sections | Basics · Schedule · Status |

## Fields

A new form shows each field's default and no message. A message shows under its field, after Save
only.

### Basics

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Title | text | ☑ | empty | 1–100 characters, spaces at either end not counted | Title is required · Title can be at most 100 characters |
| Notes | text, several lines | ☐ | empty | no limit; only spaces is empty | |

### Schedule

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Due date | switch | ☐ | off | | |
| Due date's day | date | ☐ | today | any day, also before today; only used with the switch on | |

### Status

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Status | status with Next | ☐ | Open | Open → In progress → Done → Open | |
