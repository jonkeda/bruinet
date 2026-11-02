# Feature: TOD.07 Sync with the backend

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.Todo.App |
| Area | Sync |
| Target | MAUI, BLAZOR |
| Journey | TOD.07 |
| TestData | one-todo |
| Tags | maui, web, sync |

@TOD.07.1 @TOD.11.9 @service @sync
## Scenario: TOD.07.1 A sync fetches the users, pushes local changes, then pulls the server list

```gherkin
When I create a todo "Order paint"
And I sync
Then the sync should succeed with 1 pushed
And the server should have received, in order
  | Method | Resource |
  | ------ | -------- |
  | GET    | users    |
  | PUT    | todos    |
  | GET    | todos    |
And the todo "Order paint" should be synced
And the server should have todos
  | Title       |
  | ----------- |
  | Order paint |
```

@TOD.07.2 @smoke @maui @web @sync @needs-stub
## Scenario: TOD.07.2 After a successful sync the pending markers disappear

```gherkin
Given I am on the Todo List page
When I create a todo "Order paint"
Then I should be on the Todo List page
And the todo "Order paint" should be waiting to sync
When I tap Sync
Then the todo "Order paint" should be synced
And the server should have todos
  | Title       |
  | ----------- |
  | Order paint |
And the database should have todos
  | Title       | SyncState |
  | ----------- | --------- |
  | Order paint | Synced    |
```
