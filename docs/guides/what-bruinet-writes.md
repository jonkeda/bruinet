# What Bruinet Writes

Bruinet writes recordings, traces and media while it tests. Some belong in git; most do not, and some can hold what
the app showed or sent. This page says where each goes and what it can hold.

## The Artifacts

| Artifact | Where, and what it can hold | In git |
| --- | --- | --- |
| SQL recordings | The suite's recordings folder. Every command's text and every row it read; secret-named parameters are `***` ([SQL Replay](sql-replay.md#what-a-recording-holds)). | Yes |
| HTTP recordings | The suite's recordings folder. Every call and answer; named headers, fields and query parameters are `***` ([HTTP Replay](http-replay.md#secrets)). | Yes |
| Step traces | `TestResults/…/step-traces/`. Each step's text and, after a failure, what every control showed; password fields are `***` ([UAT reporting](uat-template-guide.md)). | No |
| Call logs and results | `TestResults/…`. Locators, page and control names, messages. | No |
| Screenshots and videos | `TestResults/…/screenshots`, the recordings folder for narrated videos. Whatever was on the screen. | No |
| Scraper snapshots | The scraped project's `Recordings` folder. All text on a captured page, unless the project masks it ([Scraper](scraper.md)). | Yes, for your own app's pages |
| The Scraper's browser profile | Outside the repository. The person's sign-in to the scraped app. | No |
| The speech cache | The suite's speech folder. Narration audio, by its text. | Optional |
| Extended Events files (`.xel`) | On the SQL Server, in the session's folder, kept after `sql capture stop`. Whole command batches. | No |

## What To Keep In Mind

- Record against seeded test data only, never a copy of production: recordings are committed.
- `TestResults` is not for public upload: traces, screenshots and videos show the app's screens.
- `BRUINET_SCRAPER_DEBUG_PORT` opens DevTools on the recording browser, which holds the person's sign-in; any local
  process can drive it while the variable is set.
- An MCP agent that uses `write_code` and then `run_test` or `run_scenario` runs code it wrote, on this machine.
- The SQL replay hook answers callers on the app's own machine, or whoever has `BRUINET_SQL_HOOK_KEY`
  ([SQL Replay](sql-replay.md#an-app-in-a-process-of-its-own)).
- Give `bruinet sql` its connection string with `--connection-from <variable>`: `--connection` puts it in shell
  history and the process list.
