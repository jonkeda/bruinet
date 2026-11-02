# Feature: TOD.07 Sync between two devices

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.Todo.Web |
| Area | Sync |
| Target | BLAZOR |
| Journey | TOD.07 |
| Tags | web, sync, multi-app |

@TOD.07.3 @web @sync @multi-app @needs-stub
## Scenario: TOD.07.3 A todo Alice syncs appears on Bob's device after Bob syncs

```gherkin
Given Bob is on the Todo List page
And Alice is on the Todo List page
When Alice taps Add
Then Alice should be on the Todo Edit page
When Alice sets Title to "Pack for the trip"
And Alice taps Save
Then Alice should be on the Todo List page
When Alice taps Sync
Then the todo "Pack for the trip" should be synced
And the server should have received the todo "Pack for the trip" from Alice
And Bob should be on the Todo List page
And Todos should not have todo "Pack for the trip"
When Bob taps Sync
Then Todos should have todo "Pack for the trip"
And the todo "Pack for the trip" should be synced
And the database should have todos
  | Title             |
  | ----------------- |
  | Pack for the trip |
```
