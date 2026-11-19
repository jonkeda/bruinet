# Spec: TOD.09 Persistence and startup

| Field | Value |
| --- | --- |
| Product | Todo |
| Journey | TOD.09 Persistence and startup |
| Where | The app on Windows and Android, and the same app on the web |
| Status | draft |

## Intent

A person's todos are on their device, and they are still there tomorrow, after an update of the
app, and when the server cannot be reached. Opening the app never needs the server.

## Rules

| Id | Rule | Said by |
| --- | --- | --- |
| TOD.09.R1 | When the app opens for the first time on a device, it shall start with no todos and be ready to keep them. | The rule itself |
| TOD.09.R2 | When the app is replaced by a newer one, it shall keep every todo, with every part. | The rule itself |
| TOD.09.R3 | While the app is closed and opened again, every todo shall be kept as it was. | Example R3 |
| TOD.09.R4 | If the server cannot be reached when the app opens, then the app shall show the todos on the device and say "Server unreachable" in the sync line. | [When a sync fails](app/sync.md), row 2; example R4 |

## Examples

### R3 Still there after a restart

Closing the app is not a way to lose work.

```gherkin
Given the list holds "Buy milk" and "Write the report"
When I close the app and open it again
Then the list holds "Buy milk" and "Write the report"
```

### R4 Opening without the server

The person sees their todos without waiting for a server, and is told the server is not there.

```gherkin
Given the device holds "Buy milk"
And the server cannot be reached
When I open the app
Then the list shows "Buy milk"
And the sync line says "Server unreachable"
```

## Not in this journey

- What a sync does once the server is back: TOD.07, TOD.08.
- The app stopped in the middle of a save: TOD.08, R7.
