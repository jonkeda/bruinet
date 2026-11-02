# AI6 design: what the app said during a step

Status: **built**, 2026-11-02, the owner taking the four proposals. Bruinet has no hook into an app's logging today: no `ILoggerProvider`, no browser
console capture, no unhandled-exception hook. Everything below is new.

## Sources, per head

| Head | Logs | Unhandled exceptions | Browser console |
| --- | --- | --- | --- |
| MauiHeadless, bUnit, service | an `ILoggerProvider` added to the app's services: by the MauiHeadless head itself; a bUnit or service suite that composes the app adds `AddBruinetStepLog()` where it does | `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` | n/a |
| Web (Playwright) | as built: the app runs in its own process (`WebAppHost`), so its log is not read; a later item | n/a | `IPage.Console` and `IPage.PageError` |
| Windows, Android, iOS | out of process: not now (a file or adb logcat later) | not now | n/a |

## Shape

- `Bruinet.Core.Logging.StepLog`: a bounded in-memory list per step (`TestScope.Current.Step` keys it), entries
  `{at, level, category, message, exception?}`; warnings and up kept, 50 entries per step, each message 500 characters.
- `StepLogProvider : ILoggerProvider` in a new small package (`Bruinet.Logging`, Microsoft.Extensions.Logging only),
  so Core stays free of it: a suite's fixture adds it where it builds the app's services.
- The Playwright context subscribes `Console` (errors and warnings) and `PageError` into the same list.
- `UatStepFailure` adds `appEvents` (the failed step's entries, newest 10); `__result.json` keeps them on the failed step.
- MCP: `get_failure` shows them; `step_log {path, line, step}` reads a step's entries from the newest result.

## Open questions, with proposals

| # | Question | Proposed | Why |
| --- | --- | --- | --- |
| Q1 | A package of its own or in Core | `Bruinet.Logging` | Core references no Microsoft.Extensions.Logging today (architecture test) |
| Q2 | Which levels | Warning and up; Information with `BRUINET_STEP_LOG=all` | an agent needs the errors; all info floods a result |
| Q3 | Out-of-process heads | not now; Android's logcat as a later item | the in-process heads are where agents work (scope: Html and MAUI first) |
| Q4 | Wired by each suite or by the fixtures' base classes | the base classes (`MauiTestFixtureBase`, the web host), opt-out by config | no suite has to remember it |

Done when: a Todo scenario failing because the app logged an error and threw shows the error and the exception in
`get_failure` on MauiHeadless and on the web head.
