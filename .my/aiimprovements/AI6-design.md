# AI6 design: what the app said during a step

Status: **built**, 2026-11-02, the owner taking the four proposals. Bruinet has no hook into an app's logging today: no `ILoggerProvider`, no browser
console capture, no unhandled-exception hook. Everything below is new.

## Sources, per head

| Head | Logs | Unhandled exceptions | Browser console |
| --- | --- | --- | --- |
| MauiHeadless, bUnit, service | an `ILoggerProvider` added to the app's services: by the MauiHeadless head itself; a bUnit or service suite that composes the app adds `AddBruinetStepLog()` where it does | `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` | n/a |
| Web (Playwright) | as built: the app runs in its own process (`WebAppHost`), so its log is not read; a later item | n/a | `IPage.Console` and `IPage.PageError` |
| Windows, Android, iOS | out of process: not now (a file or adb logcat later) | not now | n/a |

## Shape (as built, after the review)

- `Bruinet.Core.Logging.StepLog`: a step takes a `Mark()` when its handler starts and `Since(mark, scope)` when it
  ends (`BddStepExecutionSession.RunNextAsync`), so its result carries what was said in its own run
  (`BddStepResult.AppEvents`): a re-run or a same-named step elsewhere never shares it. An entry said off the test's
  flow (the app's UI thread, Playwright's event loop) is filed under `TestScope.Latest`, which goes back when a scope
  ends. The newest 500 entries of the process; each text 500 characters. No exception hooks: an unhandled exception
  ends the process before a report, an unobserved task's fires at finalization.
- `Bruinet.Logging` (net8 to net10): `StepLogProvider` and `AddBruinetStepLog()`; the MauiHeadless head adds it after
  the app's registrations; `BRUINET_STEP_LOG=off` leaves it out, `all` keeps information.
- Playwright's context keeps console errors and warnings and page errors, `elsewhere`.
- `__result.json`: `appEvents` on every step that has any (level, source, message, exception, times), at most 10, a
  repeated line once; `BddStepRun.AppEvents`; MCP `get_failure` (the failed step's) and `step_log {path, line}`
  (file, line, events, at most 20 steps).

## Open questions, with proposals

| # | Question | Proposed | Why |
| --- | --- | --- | --- |
| Q1 | A package of its own or in Core | `Bruinet.Logging` | Core takes no Microsoft.Extensions.Logging today (no architecture test checks it) |
| Q2 | Which levels | Warning and up; Information with `BRUINET_STEP_LOG=all` | an agent needs the errors; all info floods a result |
| Q3 | Out-of-process heads | not now; Android's logcat as a later item | the in-process heads are where agents work (scope: Html and MAUI first) |
| Q4 | Wired by each suite or by the fixtures' base classes | the base classes (`MauiTestFixtureBase`, the web host), opt-out by config | no suite has to remember it |

Done when (as built): a Todo scenario whose app logs an error shows it on its step, through the gate and through
run_scenario on MauiHeadless (TOD.08.1, line 26); a re-run shows its own only (`McpRunTests`). Not done: the web
head's app runs in its own process, its log not read (a follow-up).
