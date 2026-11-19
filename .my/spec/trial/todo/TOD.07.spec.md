# Spec: TOD.07 Sync with the server

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.07 Sync with the server |
| Where | The app on Windows and Android, and the same app on the web; the server |
| Status | draft |

## Intent

A person's todos are the same on every device they use. They work without a connection and press
Sync when they have one; nothing they did is lost, and when two devices changed the same todo, the
later change is the one that stays.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.07.R1 | A sync first sends what changed on the device, then takes the server's list. | [The order](app/sync.md) |
| TOD.07.R2 | After a sync that succeeded, no row shows the marker. | Example R2 |
| TOD.07.R3 | A todo made on the server is on the device after a sync. | [The merge](app/sync.md), row 1 |
| TOD.07.R4 | A todo made on the device is on the server after a sync. | [What is sent](app/sync.md), row 1 |
| TOD.07.R5 | A todo deleted on the device is gone from the server after a sync. | What is sent, row 4 |
| TOD.07.R6 | When both sides changed a todo, the one changed later stays. | The merge, rows 4 and 5 |
| TOD.07.R7 | While nothing has changed on either side, a sync shall store and delete nothing on the server and change nothing on the device. | The rule itself |
| TOD.07.R8 | Every call to the server carries the app's key. | The order: the last point |
| TOD.07.R9 | The app and the server agree on what a todo is: every part the one sends, the other reads. | The rule itself |
| TOD.07.R10 | An app that was given a name sends it with every call, and the server records which client sent each call. | The order: the last point |
| TOD.07.R11 | A todo made on a Windows app reaches a browser after both have synced. | Example R11; the table "Devices", row 1 |
| TOD.07.R12 | A todo made on one Windows app reaches a second Windows app beside it. | Example R11; the table "Devices", row 2 |
| TOD.07.R13 | When two apps changed the same todo, both show the later change after both have synced. | Example R13 |
| TOD.07.R14 | An app that was without a connection has the others' changes after its first sync with one. | Example R14 |
| TOD.07.R15 | A todo made on an Android app reaches a Windows app. | Example R11; the table "Devices", row 3 |

## Tables

### Devices (R11, R12, R15)

Example R11 holds for each pair.

| # | The first person | On | The second person | On |
| --- | --- | --- | --- | --- |
| 1 | Daan | Windows | Finn | a browser |
| 2 | Daan | Windows | Gus | Windows |
| 3 | Eva | Android | Daan | Windows |

## Examples

Every app is on Todos, on the same server, with a connection.

### R2 The markers go

The marker says "not on the server yet"; after a sync that is no longer true of any todo.

```gherkin
Given the list holds "Buy milk" with the marker
When I press Sync
Then the sync line says "Synced" and the time
And "Buy milk" shows no marker
```

### R11 From one device to another

What one person writes down, the other finds, without either doing more than Sync.

```gherkin
When Daan makes the todo "Order the tiles"
And Daan presses Sync
And Finn presses Sync
Then Finn's list shows "Order the tiles"
And Finn's "Order the tiles" shows no marker
```

### R13 The later change stays, on both

Two people correct the same todo. Nobody is asked to choose; the last one to save is right.

```gherkin
Given Daan and Finn both have "Order the tiles", synced
When Daan changes its title to "Order the grey tiles" at 10:00
And Finn changes its title to "Order the white tiles" at 10:05
And Daan presses Sync
And Finn presses Sync
And Daan presses Sync
Then Daan's list shows "Order the white tiles"
And Finn's list shows "Order the white tiles"
```

### R14 Back from being offline

A device that was away catches up in one sync, and brings what was done on it.

```gherkin
Given Gus has no connection
When Daan makes the todo "Order the tiles"
And Daan presses Sync
And Gus makes the todo "Call the plumber"
And Gus gets a connection
And Gus presses Sync
Then Gus's list shows "Order the tiles" and "Call the plumber"
When Daan presses Sync
Then Daan's list shows "Order the tiles" and "Call the plumber"
```

## Choices

- A sync is asked for; the app does not sync by itself, except once when it opens with a connection.
- The later change wins whole: two changes to different parts of one todo are not combined.

## Not in this journey

- What the person sees when a sync fails: TOD.08.
- Users, which a sync fetches first: TOD.11.
- What a sync does with a deleted todo that was never sent: TOD.06, R5.
