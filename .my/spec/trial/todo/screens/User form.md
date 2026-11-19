# Screen: User form

| Field | Value |
| --- | --- |
| Product | Todo |
| Where | The app on Windows and Android. Not on the web |
| Used by | TOD.11 |
| Status | draft |

## Card

| User form | |
| --- | --- |
| Reached by | Add on Users; a user on Users |
| Title | "New user" from Add; "Edit user" from a user |
| Top buttons | Cancel · **Save** |
| Top of the page | Without a connection: "You are offline. Users can be viewed, but not changed."; after a save the server could not take: "Not saved." and why |
| Sections | Person · Access · Work |
| Bottom of the page | For a user that exists: "Last changed" with day and time, as 10-Mar-2026 09:00 |

Save is disabled without a connection, and only then. Cancel leaves without asking.

## Fields

A new form shows each field's default and no message. Save shows every message at once, each
under its field. Typing in a field takes that field's message away.

### Person

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Name | text | ☑ | empty | 1–50 characters, spaces at either end not counted; not the name of another user who is not Inactive, capitals ignored | Name is required · Name can be at most 50 characters · Another user already has this name |
| Email | text | ☐ | empty | One @ with text before it, a dot in the part after it, no spaces, no dot at the end | Enter an email address such as eva@example.com |
| Notes | text, several lines | ☐ | empty | At most 500 characters, spaces at either end not counted | Notes can be at most 500 characters |

### Access

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Status | radio | ☑ | Active | Active, On leave, Inactive | |
| Has an end date | checkbox | ☐ | off | | |
| End date | date | ☐ | today | Any day, also before today; only used, and only enabled, with the box ticked; shown as 10-Mar-2026 | |
| Role | select | ☑ | Member | Member, Lead, Admin | |

### Work

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Hours per week | number with plus and minus | ☑ | 40 | 1–60, whole hours; the buttons reach 0–99 | Hours per week must be between 1 and 60 |
| Hourly rate | text | ☑ | 0.00 | 0–10,000.00, at most two decimals, written as 1,250.75 | Enter a rate such as 1,250.75 · Hourly rate must be between 0 and 10,000.00 · Hourly rate can have at most two decimals |
| Start of day | time | ☑ | 09:00 | Shown as 09:00, 24 hours | |
| Receives reminders | switch | ☐ | on | | |
| Workload | slider | ☑ | 0 | 0–100, in steps of 5 | Workload must be 0 to 100, in steps of 5 |

A name that an Inactive user has may be used again, and an Inactive user may keep a name another
user has.
