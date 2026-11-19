# Spec: TOD.11 Manage users

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.11 Manage users |
| Where | The app on Windows and Android. Not on the web |
| Status | draft |

## Intent

Someone keeps the list of people who work with the todos: who they are, what they do, and whether
they are there now. The list is the server's, so it is the same for everyone; a change is made
with a connection or not at all. A wrong value is refused on its own field, in words that say
what is allowed.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.11.R1 | Users lists the available users first, then the others, each with its role and its state. | [Users](screens/Users.md): The list; the table "A user's state" |
| TOD.11.R2 | Add with allowed values saves the user on the server, and Users shows it. | Example R2 |
| TOD.11.R3 | Each field refuses a value outside its limits, with its message under that field. | [User form](screens/User%20form.md): Limits and Message; the tables "Rate as typed" and "A name used twice" |
| TOD.11.R4 | Opening a user shows every field with that user's value; Save keeps the change. | User form: the card; example R4 |
| TOD.11.R5 | With "Has an end date" off at Save, the user has no end date, whatever day was picked; with it on, the day is kept, also one before today. | The table "End date" |
| TOD.11.R6 | An Active user whose end date is before today shows "Expired"; one whose end date is today or later does not. | The table "A user's state", rows 2 to 4 |
| TOD.11.R7 | A user set Inactive stays in the list, and their name may be given to another user. | The table "A user's state", row 6; the table "A name used twice" |
| TOD.11.R8 | While there is no connection, Add and Save are disabled and both pages say so; the list and a user's form can still be read. | Users: the card; User form: the card; example R8 |
| TOD.11.R9 | A sync fetches the users before the todos, and replaces the users on the device only when the whole list has arrived. | [The order](app/sync.md) |
| TOD.11.R10 | The server refuses what the form refuses, for the same field and with the same message. | The rule itself; User form: Limits and Message |

## Tables

Today is 26 October 2026.

### A user's state (R1, R6, R7)

A dash is any.

| # | Status | End date | State shown | Available, so listed first |
| --- | --- | --- | --- | :---: |
| 1 | Active | none | | ☑ |
| 2 | Active | 27 October 2026 | | ☑ |
| 3 | Active | 26 October 2026 | | ☑ |
| 4 | Active | 25 October 2026 | Expired | ☐ |
| 5 | On leave | — | On leave | ☐ |
| 6 | Inactive | — | Inactive | ☐ |

### End date (R5)

| # | "Has an end date" at Save | Day picked | End date kept |
| --- | --- | --- | --- |
| 1 | off | — | none |
| 2 | on | 31 December 2026 | 31 December 2026 |
| 3 | on | 25 October 2026 | 25 October 2026 |
| 4 | on | none | 26 October 2026 |

### Rate as typed (R3)

A rate is written with a point for the decimals and, if at all, a comma before every three
digits, on every device.

| # | Typed | Saved | Kept and shown as | Message under Hourly rate |
| --- | --- | :---: | --- | --- |
| 1 | `1,250.75` | ☑ | 1,250.75 | |
| 2 | `1250.75` | ☑ | 1,250.75 | |
| 3 | `0` | ☑ | 0.00 | |
| 4 | `10,000.00` | ☑ | 10,000.00 | |
| 5 | `10,000.01` | ☐ | | Hourly rate must be between 0 and 10,000.00 |
| 6 | `-1` | ☐ | | Hourly rate must be between 0 and 10,000.00 |
| 7 | `12.345` | ☐ | | Hourly rate can have at most two decimals |
| 8 | `1,5` | ☐ | | Enter a rate such as 1,250.75 |
| 9 | `abc` | ☐ | | Enter a rate such as 1,250.75 |
| 10 | | ☐ | | Enter a rate such as 1,250.75 |

### A name used twice (R3, R7)

The list already has one user named Eva.

| # | Eva's status | The new user's name | Saved | Message under Name |
| --- | --- | --- | :---: | --- |
| 1 | Active | `Eva` | ☐ | Another user already has this name |
| 2 | Active | `eva` | ☐ | Another user already has this name |
| 3 | On leave | `Eva` | ☐ | Another user already has this name |
| 4 | Inactive | `Eva` | ☑ | |

## Examples

Every example starts on Users, with a connection unless it says otherwise.

### R2 A new user is on the server

A name is enough; every other field has a default that can be saved.

```gherkin
When I press Add
And I type "Hana" as the name
And I press Save
Then I am on Users
And the list shows "Hana" as Member
And the server has the user "Hana"
```

### R3 Every message at once

The person sees all that is wrong in one go, each on its own field, and nothing is saved.

```gherkin
When I press Add
And I type "hana@" as the email
And I type "1,5" as the hourly rate
And I press Save
Then I see "Name is required" under Name
And I see "Enter an email address such as eva@example.com" under Email
And I see "Enter a rate such as 1,250.75" under Hourly rate
And the server has no new user
```

### R4 A change is kept

The form shows what the user has now, so one field can be changed and the rest left.

```gherkin
Given the list shows "Eva" as Member
When I open "Eva"
Then the page is titled "Edit user"
And the name says "Eva"
When I pick "Lead" as the role
And I press Save
Then the list shows "Eva" as Lead
```

### R8 Without a connection

The people can still be looked up; changing them waits for a connection.

```gherkin
Given no connection
Then I see "You are offline. Users can be viewed, but not changed."
And Add is disabled
When I open "Eva"
Then the name says "Eva"
And Save is disabled
```

## Choices

- A user is never deleted; Inactive is how a user goes.
- A role is recorded only: no role allows or forbids anything.
- Cancel on the User form leaves without asking, also with changes.
- Users are not tied to todos.

## Not in this journey

- The rest of a sync: TOD.07.
- Users on the web: not planned yet.
