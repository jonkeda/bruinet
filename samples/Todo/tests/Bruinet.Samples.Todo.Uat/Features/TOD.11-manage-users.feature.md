# Feature: TOD.11 Manage users

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.Todo.App |
| Area | Users |
| Target | MAUI |
| Journey | TOD.11 |
| TestData | team |
| Tags | maui, users |
| Using | Users |

@TOD.11.1 @TOD.11.6 @maui @users
## Scenario: TOD.11.1 Users lists available users first, each with its role and state

```gherkin
Given I am on the Todo List page
When I tap Users
Then I should be on the User List page
And the users should be listed as "Daan", "Eva", "Finn", "Gus" and "Hana"
And the user "Daan" should show role "Lead"
And the user "Eva" should show no state
And the user "Finn" should show state "Expired"
And the user "Gus" should show state "Inactive"
And the user "Hana" should show state "On leave"
```

@TOD.11.2 @maui @users @needs-stub
## Scenario: TOD.11.2 A user added with every field shows in the list, and the server has each value

```gherkin
Given I am on the Todo List page
When I tap Users
And I tap Add
Then I should be on the User Edit page
When I set Name to "Ivo"
And I set Email to "ivo@example.com"
And I select "Lead" from Role
And I set the hours per week to 32
And I set Rate to "1,250.75"
And I set the start of day to 08:30
And I uncheck Reminders
And I set the workload to 35 %
And I tap Save
Then I should be on the User List page
And Users should have user "Ivo"
And the user "Ivo" should show role "Lead"
And the server should have users
  | Name | Role | Hours Per Week | Hourly Rate | Start Of Day | Receives Reminders | Workload |
  | ---- | ---- | -------------- | ----------- | ------------ | ------------------ | -------- |
  | Ivo  | Lead | 32             | 1,250.75    | 08:30        | off                | 35       |
```

@TOD.11.3 @maui @users
## Scenario: TOD.11.3 Bad values show an error on each field, and the form stays

```gherkin
Given I am on the Todo List page
When I tap Users
And I tap Add
And I set Email to "eva@"
And I set Rate to "1,5"
And I tap Save
Then I should be on the User Edit page
And Name Error should equal "Name is required"
And Email Error should equal "Enter an email address such as eva@example.com"
And Rate Error should equal "Enter a rate such as 1,250.75"
When I set Name to "Eva"
And I tap Save
Then Name Error should equal "Another user already has this name"
```

@TOD.11.4 @maui @users @needs-stub
## Scenario: TOD.11.4 Editing a user changes what the list shows

```gherkin
Given I am on the Todo List page
When I tap Users
And I open the user "Hana"
Then I should be on the User Edit page
And the status should be "On leave"
When I select "Lead" from Role
And I set the status to "Active"
And I tap Save
Then I should be on the User List page
And the user "Hana" should show role "Lead"
And the user "Hana" should show no state
And the server should have users
  | Name | Role | Status |
  | ---- | ---- | ------ |
  | Hana | Lead | Active |
```

@TOD.11.5 @TOD.11.6 @maui @users @needs-stub
## Scenario: TOD.11.5 An end date is set only when checked; a past one shows Expired

```gherkin
Given I am on the Todo List page
When I tap Users
And I tap Add
And I set Name to "Kees"
Then Allowed Until should be disabled
When I set the end date to 09-Mar-2026
And I tap Save
Then I should be on the User List page
And the user "Kees" should show state "Expired"
And the server should have users
  | Name | Allowed Until |
  | ---- | ------------- |
  | Kees | 09-Mar-2026   |
```

@TOD.11.7 @maui @users @needs-stub
## Scenario: TOD.11.7 An inactive user stays listed, and their name can be used again

```gherkin
Given I am on the Todo List page
When I tap Users
And I add a user "Mo"
And I open the user "Mo"
And I set the status to "Inactive"
And I tap Save
Then I should be on the User List page
And the user "Mo" should show state "Inactive"
When I add a user "Mo"
Then the server should have users
  | Name | Status   |
  | ---- | -------- |
  | Mo   | Inactive |
  | Mo   | Active   |
```

@TOD.11.8 @maui @users
## Scenario: TOD.11.8 Offline, the list still shows, Add is disabled and the page says why

```gherkin
Given the device is offline
And I am on the Todo List page
When I tap Users
Then I should be on the User List page
And Offline Notice should equal "You are offline. Users can be viewed, but not changed."
And Add should be disabled
And Users should have user "Eva"
```

@TOD.11.9 @maui @users
## Scenario: TOD.11.9 A user added on another device shows after a sync

```gherkin
Given the server also has a user "Noor"
And the server also has todos
  | Title        |
  | ------------ |
  | Welcome Noor |
And I am on the Todo List page
When I tap Sync
Then Todos should have todo "Welcome Noor"
When I tap Users
Then I should be on the User List page
And Users should have user "Noor"
```
