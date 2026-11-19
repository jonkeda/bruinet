# Spec: TOD.08 Things go wrong

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.08 Things go wrong |
| Where | The app on Windows and Android, and the same app on the web; the server |
| Status | draft |

## Intent

The server is slow, down or out of reach, and the person still has their todos. The app says what
is wrong in a few words, keeps every change for later, and lets them try again. It never hides
their own work because a network is slow.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.08.R1 | If the server has a problem during a sync, then the app shall show "The server had a problem. Try again." with Retry, and keep everything on the device. | [When a sync fails](app/sync.md), row 5 |
| TOD.08.R2 | Retry, once the server works again, shows the list. | [Todos](screens/Todos.md): the diagram, Error to List; example R2 |
| TOD.08.R3 | While the server has not answered and there is nothing to show, the app shall show "Loading…". | Todos: What the body shows, row 3 |
| TOD.08.R4 | If the server does not answer in time, then the app shall say "Sync timed out" and keep every change waiting. | When a sync fails, row 3 |
| TOD.08.R5 | If the server refuses the app's key, then the app shall say "Not authorised" and show the message for it with Retry, and go on working. | When a sync fails, row 4 |
| TOD.08.R6 | If the connection is lost during a sync on a real device, then the app shall lose nothing and say why in the sync line. | When a sync fails, rows 1 to 3; judged by a person on a device |
| TOD.08.R7 | If the app is stopped during a save, then the todo shall be kept whole or not at all. | The rule itself |
| TOD.08.R8 | While there is no connection, Sync is disabled, the sync line says "Offline" and changes stay waiting; with a connection again, a sync sends them. | When a sync fails, row 1; example R8 |

## Examples

Every example starts on Todos.

### R2 Retry after the server is back

The person does not have to restart anything: the same button, once the server answers.

```gherkin
Given the server has a problem
When I press Sync
Then the page shows "The server had a problem. Try again."
When the server works again
And I press Retry
Then the list shows the server's todos
And the sync line says "Synced" and the time
```

### R8 Without a connection, and with one again

The person goes on working. What they did waits, visibly, and goes out with the first sync.

```gherkin
Given no connection
Then Sync is disabled
And the sync line says "Offline"
When I make the todo "Buy milk"
Then "Buy milk" shows the marker
When the connection is back
And I press Sync
Then "Buy milk" shows no marker
```

## Choices

- A server that cannot be reached, or is too slow, does not replace the list with a message: the rows stay and the sync line says why.
- The app does not try again by itself.

## Not in this journey

- What a sync does when it works: TOD.07.
- A user that cannot be saved: TOD.11.
- The app opening with the server out of reach: TOD.09, R4.
