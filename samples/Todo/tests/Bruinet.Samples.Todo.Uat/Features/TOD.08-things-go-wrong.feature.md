# Feature: TOD.08 Things go wrong

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.Todo.App |
| Area | Errors |
| Target | MAUI, BLAZOR |
| Journey | TOD.08 |
| TestData | empty |
| Tags | maui, web, errors |

@TOD.08.1 @smoke @maui @web @errors
## Scenario: TOD.08.1 A server error on sync shows the Error state with Retry; local data is kept

```gherkin
Given I am on the Todo List page
And the server also has todos
  | Title         |
  | ------------- |
  | Pay the bills |
When I tap Sync
Then Todos should have todo "Pay the bills"
Given the server fails with status 500
When I tap Sync
Then the list should show the Error state
And Error Message should contain "server had a problem"
And Last Sync should equal "Sync failed"
And Retry should be enabled
And the database should have todos
  | Title         | SyncState |
  | ------------- | --------- |
  | Pay the bills | Synced    |
When the server recovers
And I tap Retry
Then the list should show the Content state
And Todos should have todo "Pay the bills"
```

@TOD.08.2 @maui @web @errors
## Scenario: TOD.08.2 Retry after the server recovers shows the content

```gherkin
Given I am on the Todo List page
And the server also has todos
  | Title           |
  | --------------- |
  | Renew the lease |
And the server fails once with status 503
When I tap Sync
Then the list should show the Error state
When I tap Retry
Then the list should show the Content state
And Todos should have todo "Renew the lease"
```

@TOD.08.3 @maui @web @errors
## Scenario: TOD.08.3 A slow server shows Loading until the answer arrives

```gherkin
Given I am on the Todo List page
When I tap Sync
Then the list should show the Empty state
Given the server answers after 2 seconds
When I tap Sync
Then the list should show the Loading state
And the list should show the Empty state
```

@TOD.08.8 @maui @web @errors @offline @windows @needs-stub
## Scenario: TOD.08.8 Offline disables Sync and shows "Offline"; back online, Sync clears the pending edits

```gherkin
Given I am on the Todo List page
And the device is offline
Then Last Sync should equal "Offline"
And Sync should be disabled
When I create a todo "Buy stamps"
Then I should be on the Todo List page
And the todo "Buy stamps" should be waiting to sync
When the device is back online
Then Sync should be enabled
When I tap Sync
Then the todo "Buy stamps" should be synced
And the server should have todos
  | Title      |
  | ---------- |
  | Buy stamps |
```
