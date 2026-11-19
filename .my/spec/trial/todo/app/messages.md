# Todo: the words the app shows

| Field | Value |
| --- | --- |
| Product | Todo |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

One place for the exact words. A rule, a table or an example that quotes a message quotes one of
these.

## Refused on a form

| When | Where | Message |
| --- | --- | --- |
| The title is empty or only spaces | under Title, Todo form | Title is required |
| The title is over 100 characters | under Title, Todo form | Title can be at most 100 characters |
| The name is empty or only spaces | under Name, User form | Name is required |
| The name is over 50 characters | under Name, User form | Name can be at most 50 characters |
| Another user who is not Inactive has the name | under Name, User form | Another user already has this name |
| The email address is not one | under Email, User form | Enter an email address such as eva@example.com |
| The notes are over 500 characters | under Notes, User form | Notes can be at most 500 characters |
| The hours are under 1 or over 60 | under Hours per week, User form | Hours per week must be between 1 and 60 |
| The rate cannot be read as a number | under Hourly rate, User form | Enter a rate such as 1,250.75 |
| The rate is under 0 or over 10,000.00 | under Hourly rate, User form | Hourly rate must be between 0 and 10,000.00 |
| The rate has more than two decimals | under Hourly rate, User form | Hourly rate can have at most two decimals |
| The workload is outside 0–100 or between steps | under Workload, User form | Workload must be 0 to 100, in steps of 5 |

## The sync line on Todos

| When | Message |
| --- | --- |
| No sync has run since the app opened | Not synced yet |
| The last sync succeeded | Synced 09:41 (the time, in the device's short form) |
| There is no connection | Offline |
| The server could not be reached | Server unreachable |
| The server did not answer in time | Sync timed out |
| The server refused the app's key | Not authorised |
| The server had a problem | Sync failed |

## In place of the list on Todos

| When | Message |
| --- | --- |
| A sync runs and there is nothing to show | Loading… |
| There is nothing to show | Nothing to do |
| The server had a problem | The server had a problem. Try again. |
| The server refused the app's key | The server refused the app's key. Check the API key and try again. |

## A todo's sync state

| When | Where | Message |
| --- | --- | --- |
| The server has this version | Sync, on Todo | Synced |
| Changed on this device since the last sync | Sync, on Todo | Waiting to sync |
| Made on this device and never sent | Sync, on Todo | Local only |
| Not synced, of either kind | the marker on a row, read out by a screen reader | Not synced yet |

## Users

| When | Where | Message |
| --- | --- | --- |
| There is no connection | above the list on Users; top of the User form | You are offline. Users can be viewed, but not changed. |
| There are no users | in place of the list on Users | No users yet |
| Save, and the server could not be reached | top of the User form | Not saved. The server could not be reached. |
| Save, and the server did not answer in time | top of the User form | Not saved. The server did not answer in time. |
| Save, and the server had a problem | top of the User form | Not saved. The server had a problem. Try again. |
| Save, and the server refused the app's key | top of the User form | Not saved. The server refused the app's key. Check the API key and try again. |

## Questions

| When | Title | Text | Answers |
| --- | --- | --- | --- |
| Cancel on a changed Todo form | Discard changes? | Your changes to this todo have not been saved. | Discard · Keep editing |
| Delete on Todo | Delete todo? | "Buy milk" will be deleted. (the todo's title, in quotes) | Delete · Cancel |
