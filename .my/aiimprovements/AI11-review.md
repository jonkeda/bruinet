# AI11 review: HTTP replay

Date: 2026-11-02. Branch `ai11-http-replay` (local, not pushed: a PR waits on `move`). Commits ac190116..close.

## What was built

| Piece | Where | Proven by |
| --- | --- | --- |
| Recording, matching (method, path, query, JSON body by field), volatile values from two recordings, `model` and `oauth` presets, recorded order, misses naming the nearest call | `src/Bruinet.Mocking.Http` | `HttpReplayTests` (17) |
| Redaction (headers, query, JSON and form fields, the call log and misses too), secret scan before saving | `HttpRedaction` | `Keys_are_never_written...`, `A_recording_holding_what_looks_like_a_key...` |
| Streamed answers as parts, replayed at once or at recorded pace | `HttpReplaySession` | `A_streamed_answer_...` (2) |
| In process: `HttpReplayHandler`, `AddBruinetHttpReplay`, the hermetic guard | `HttpReplayHandler.cs` | `Every_factory_client_...`, `A_hermetic_replay_...` |
| Out of process: `MockApiServer.Record`, `CopyRecordedTo`, `Replay`, `ReplayMisses` | `src/Bruinet.Mocking/MockApiServer.Replay.cs` | `MockApiServerReplayTests` (3) |
| `## Dependencies`, `BRUINET_DEP_<NAME>`, `Hermetic`, `HttpRedact`, `HttpReplayPace` | `src/Bruinet.Bdd/BddDependencies.cs` | `BddDependenciesTests` (4) |
| SQLite `copy`: a template per stamp, a copy per test | `src/Bruinet.Mocking.Data/SqliteTemplate.cs` | `SqliteTemplateTests` (2) |
| `bruinet http stale|diff|reviewed` | `tools/Bruinet.Cli/Http/HttpCommands.cs` | library tests only (see gaps) |
| MCP `run_scenario {replay}`, `http_calls` | `BddMcpServer` | `McpRunTests` (2), tool list |
| The Todo app's real sync recorded, then replayed hermetic with the backend gone | `samples/Todo/tests/...IntegrationTests/HttpReplaySyncTests.cs` | it passes; it found the hermetic port bug |

## Checks

- Unit and contract tests: Mocking.Http 17, Mocking 54, Mocking.Data 56, Bdd 283 (2 skipped), Todo integration 62, CLI 380 with
  8 failing that fail the same on the commit before AI11 under `--artifacts-path` (generator integration, data validator).
- Mocking.Data had one failure in one run that did not come back in 7 runs; a timing test on a busy machine, noted.
- The solution build under `--artifacts-path`: the Stride sample cannot build there (its task joins two absolute paths) and
  `Bruinet.Maui.UITests` failed in the parallel build but builds alone; neither is touched by AI11.
- Agent check: not run. It needs a UAT suite whose host gives the app a session, which is the gap below; `checks/AI11.md` says so.

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Blocker: the call log and misses held the URL unredacted | fixed: logged URLs are redacted; tested |
| Blocker: no host makes a session from the Dependencies table in a real UAT run | **not built**: a follow-up (below) |
| A multibyte character split across stream reads | fixed: one decoder per stream |
| A gzipped answer stored as base64 skipped redaction and the scan | fixed: the handler decompresses |
| Refresh made a changed request volatile, so it passed silently | fixed: refresh keeps the old volatile values and reports `request` changes; tested |
| A `Where` with backticks around parts broke the preset | fixed; tested |
| SqliteTemplate: delete-then-move window, lock refusal not retried | fixed: `File.Replace`, retry on access refused |
| `CopyRecordedTo` copied stubbed calls; `Record` twice added a second proxy | fixed; tested |
| `http_calls` searched `bin`, `UnusedExchanges` uncapped | fixed |
| Both reviews wrote `reviewed.json` | fixed: `reviewed.http.json` |
| Volatile values are ignored, not bound into answers (the design said bound) | recorded: a limit in the guide |
| No `HttpReplayTransport` | by design now: `HttpClientTransport` over the handler; in AD-034 |
| The guard covers factory clients, not an app's own `new HttpClient()` | recorded: the app passes `HttpReplayHandler.Create` itself |
| Record buffers a streamed answer before the app sees it | recorded |
| CLI `http` commands have no tests of their own | recorded |

## Not done in AI11: the design's step 5 (built in AI11b, below)

The Todo UAT suites on MauiHeadless, bUnit and Chromium running hermetic. It needs the suites' hosts to make a session per
scenario from the Dependencies table, end it and fail the scenario on its misses, and Todo's backend phrases (23 places read
the stub server's request log) to read the session's calls in replay. A model call into a real Azure OpenAI client (the
spike) needs a deployment and was not run.

# AI11b: the Todo UAT suites replay their backend

Commits cbde293a..close, on the same local branch.

## What was built

- `TodoHttpReplay` (TestSupport): the app's handler through `TodoPlatform.HttpHandler`, a session per scenario begun in
  `BeforeScenario` and ended in `AfterScenario` (misses fail it), a startup session; the mode is the run's
  `BRUINET_HTTP_REPLAY`, else the suite's `backend` row. Off (and no handler) without a config or replay.
- The MAUI headless suite and the bUnit web suite (its main app; a scenario with two apps reads the backend they share and
  needs the stub) have `Hermetic`, `HttpReplayPace recorded` and `## Dependencies`; recordings in git, call logs not.
- `@needs-stub`: a scenario that reads its stub back is skipped while a run records or replays (by the variable or a
  dependency's mode), so recording and replay run the same scenarios in order. Seven scenarios carry it.
- What replay needed from the suites: an id in the path volatile and calls paired by the path's shape; volatile GUIDs bound
  into later answers (whole JSON strings and path segments only); a call that got no answer recorded and failing the same
  way after its recorded wait; each answer's duration waited out at recorded pace; `"Kind": "http"` so SQL replay's review
  skips an HTTP recording with no calls; `bruinet http diff` reading a long-named recording (`--`).

## Checks

| Run | Result |
| --- | --- |
| Todo UAT headless: record twice, replay | 36 passed, 7 skipped, 0 failed, no backend reached (hermetic guard) |
| Todo UAT headless with the stub | 42 passed, 1 skipped |
| Todo web UAT bUnit: record twice, replay | 30 passed, 5 skipped, 0 failed |
| Todo web UAT bUnit with the stub | 33 passed, 2 skipped; TOD.08.8 flakes about 1 run in 4, as on the commit before AI11 (3 in 6 there) |
| Todo UI tests headless, web UI tests bUnit (the SQL review gate), integration | 45, 11, 62 passed |
| Mocking.Http, Mocking, Mocking.Data, Bdd, CLI http | 21, 54, 56, 282, 2 passed |

Agent check (`checks/AI11.md`): a fresh agent fixed a seeded request bug in 5 tool calls from the miss alone; without
replay the bug is not seen at all.

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Blocker: with replay off the app still got the replay's handler (decompression, a pool across restarts) | fixed: no handler when off |
| Blocker: `@needs-stub` read only the variable, not the suite's `backend` row | fixed: either; tested |
| Binding rewrote any substring (a seeded date, part of a number) | fixed: GUIDs only, whole JSON strings and path segments; tested |
| A streamed answer waited twice at recorded pace; a recorded failure did not wait | fixed |
| A cancelled wait left its answer undisposed | fixed |
| A suite without a config with the variable set kept one startup session forever | fixed: off without a config |
| The web fixture does not wait for the app's work to go quiet between scenarios | recorded: bUnit renders in the test's thread; three record-and-replay runs agreed |
| A client timeout is not recorded (it arrives as the caller's cancellation) | recorded: no Todo scenario times out |
| Out of process a recorded failure is an empty response, not a refused connection | recorded |
| A hermetic violation outside a session shows as the app's "Unreachable", not as a failure by name | recorded |
