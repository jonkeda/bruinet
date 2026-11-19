# Screen: Users

| Field | Value |
| --- | --- |
| Product | Todo |
| Where | The app on Windows and Android. Not on the web |
| Used by | TOD.11 |
| Status | draft |

## Card

| Users | |
| --- | --- |
| Reached by | Users on Todos; Save or Cancel on the User form |
| Title | "Users" |
| Top buttons | Back · Add |
| Above the list | Without a connection: "You are offline. Users can be viewed, but not changed." |
| Body | The list, or "No users yet" |

Add is disabled without a connection.

## The list

| Column | Shows | Sort | Filter |
| --- | --- | :---: | :---: |
| Name | The user's name | ☑ second, A→Z, capitals ignored | ☐ |
| Role | Member, Lead or Admin | ☐ | ☐ |
| State | "Expired", "On leave" or "Inactive"; nothing for a user who is available | ☑ first, available users before the others | ☐ |

- The order is fixed; a person cannot change it.
- Which state a user shows, and who is available: the table "A user's state" in TOD.11.
- Tapping a row opens that user's form, also without a connection, to read it.
- All users are on one page.
