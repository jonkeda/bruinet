# AI11 design: HTTP replay, for hermetic integration and UI tests

Status: **design**, 2026-11-02. Open questions at the end, each with a proposal.

## Outcome

A test answers its app's outgoing HTTP calls from a recording, as SQL replay answers its database: record once
against the real services, replay with none. With SQL replay beside it, a UI test or an integration test needs
nothing outside its process: **hermetic**. It serves three uses:

| Use | What is replayed | Why |
| --- | --- | --- |
| Hermetic UI tests | the app's backend and third-party APIs, its database (SQL replay) | parallel runs, no shared state, no flaky network, CI without services |
| Integration tests | the third parties a service calls (payment, maps, identity, a model) | the service's own code runs for real; what it depends on is pinned |
| Model calls (AI apps, Tomorrow's Foundry) | chat and image calls | no token spend, no 5-second waits, the same answer each run |

## What exists

- `Bruinet.Mocking`: `MockApiServer` over WireMock.Net (stubs by hand: `Get(path).ReturnsJson(...)`, `FailsFirst`,
  `WithDelay`, request counts and waits). The Todo sample stubs its backend with it. Stubs are written, not recorded.
- `Bruinet.Mocking.Data`: SQL replay (AD-031): `BRUINET_SQL_REPLAY=Record|Replay`, a recording per test, record twice
  to find volatile values, a miss names the command, `bruinet sql stale|diff|reviewed`. HTTP replay copies its shape,
  so a suite learns one way of working.

## Shape

### Modes and files

- `BRUINET_HTTP_REPLAY`: `Record` (calls go to the real service and are written), `Replay` (answered from the
  recording; nothing leaves the process), `Refresh` (record again and show what changed), unset for neither.
- One recording per test beside SQL replay's: `Recordings/http/<test>.json`, each exchange
  `{method, url, request headers kept, body, status, response headers kept, body, chunks?, volatile fields}`.
- `Hermetic` in `uat.config.md` (`| Hermetic | true |`): every dependency answered inside the process, as its own
  setting below says, and a call that reaches the network or a database server fails (the guard below).

### A setting per dependency

Not every app needs every replay. A suite names, per dependency, how its tests get it, in `uat.config.md`'s
`## Dependencies` table (an environment variable `BRUINET_DEP_<NAME>` overrides one row for a run):

| Name | Kind | Mode | Where |
| --- | --- | --- | --- |
| backend | http | replay | `https://api.example.com` |
| maps | http | stub | `MapsStubs` (a class of `MockApiServer` stubs) |
| model | http | replay | `https://*.openai.azure.com`, preset `model` |
| db | sqlite | copy | `TestData/seed.db` |
| reporting | sqlserver | replay | the app's `ReportingDb` connection |

| Kind | Modes | What each means |
| --- | --- | --- |
| `http` | `real`, `record`, `replay`, `stub` | the real service; record it; answer from the recording; hand-written stubs (`MockApiServer`, as today) |
| `sqlserver` | `real`, `record`, `replay` | SQL replay (AD-031) as today |
| `sqlite` | `real`, `copy`, `record`, `replay` | `copy`: a fresh copy of a seeded file per test, the real engine in the process |

**SQLite is not mocked by default.** An embedded database already runs in the test process, needs no server and
answers in microseconds: replaying it saves nothing and loses what it proves (the real queries, constraints and
migrations). `copy` gives each test its own file from a seeded template (`TestData/seed.db`, made once per build by the
app's own migrations and seed), so tests stay parallel and share nothing. That is hermetic already. `replay` stays for
a suite that wants a SQLite app's SQL pinned anyway (Todo's samples, which prove SQL replay itself).

So Construction (a SQLite app) runs hermetic with `db | sqlite | copy` and its APIs as `http | replay` or `stub`;
Tomorrow (SQL Server and Foundry) with `sqlserver | replay` and `model | http | replay`. A suite with no
`## Dependencies` table behaves as today.

### Two transports

| The app runs | Transport | How the app is pointed at it |
| --- | --- | --- |
| In the test process (MauiHeadless, bUnit, service head, integration tests) | `HttpReplayHandler : DelegatingHandler`, no port | `services.AddBruinetHttpReplay()`: every `IHttpClientFactory` client gets the handler; an Azure SDK client takes `HttpReplayTransport` (its `HttpPipelineTransport`) |
| In a process of its own (web head's `WebAppHost`, Windows, Android) | WireMock.Net as a proxy: `ProxyAndRecord` while recording, its mappings while replaying | the app's base URLs from its launch settings point at the proxy (`MockApiServer`'s port), as the Todo backend's already does |

Both write and read the same recording format, so a recording made in process replays out of process.

### Matching

- By method, path, query and body. A JSON body matches field by field, ignoring the **volatile** ones.
- Volatile fields come from recording twice, as SQL replay finds new ids and clocks: what differs between the two
  recordings is bound to the live run's value instead of matched.
- Presets name the usual ones without a second recording:
  - `model`: request ids, `temperature`, `seed`, `user`, `stream_options`, timestamps; matched on model and messages;
  - `oauth`: tokens, nonces, `state`.
- Several calls with the same request replay in recorded order (a poll that answers "pending", then "done").

### Secrets

- Never written: `Authorization`, `api-key`, `Ocp-Apim-Subscription-Key`, cookies and any header or query parameter a
  suite names (`| HttpRedact | x-functions-key |`) are recorded as `***`.
- A recording is scanned before it is saved: a value that looks like a key (the gitleaks patterns AD-033 already runs)
  stops the save, naming the field.

### Misses and the guard

- A call the recording cannot answer is a miss: `HttpReplayMissException` names the method, URL, the nearest recorded
  call and how they differ. In replay nothing is made up. A test checks its misses at its end, as SQL replay's does.
- `Hermetic`: the handler and the proxy refuse any host not in the recording, and a `SocketsHttpHandler` connect
  callback on the in-process heads refuses the rest, so a call that slipped past the factory fails by name, not by a
  timeout.

### Streaming

- Server-sent events and chunked responses are recorded as chunks with their gaps, replayed at recorded pace or at once
  (`| HttpReplayPace | recorded or none |`). A spike settles it first: WireMock.Net's streaming in proxy mode, and an
  Azure OpenAI streamed chat replayed into the Foundry client.

### Review

- `bruinet http stale|diff|reviewed`, as `bruinet sql`: `diff` after a `Refresh` shows each exchange whose answer
  changed (a model's new wording, an API's new field), `--accept` keeps them, `reviewed` fails while a recording went
  in unread.

### For agents (the AI plan)

- `run_scenario {replay: "record"|"replay"}` runs with the mode set.
- `http_calls {path, line, step?}`: the calls a step made in its newest run (method, URL, status, matched or missed),
  from the recording beside its result; `get_failure` gains the misses.

## Packages

| Package | What |
| --- | --- |
| `Bruinet.Mocking.Http` (new) | the recording, matching, `HttpReplayHandler`, `HttpReplayTransport` (Azure.Core), `AddBruinetHttpReplay()` |
| `Bruinet.Mocking` | `MockApiServer` gains `Record(target)` and `Replay(recording)` over WireMock.Net's proxy and mappings |
| `Bruinet.Uat` | the `Hermetic`, `HttpRedact`, `HttpReplayPace` config fields and the `## Dependencies` table (`BRUINET_DEP_<NAME>` overrides); a scenario's misses fail it |
| `Bruinet.Mocking.Data` | `sqlite` `copy`: the seeded template made once per build, a copy per test, deleted after |
| `tools/Bruinet.Cli` | `bruinet http ...`; the MCP tools above |

## Steps

1. Spike (a day): an Azure OpenAI streamed chat and a plain JSON API, recorded through WireMock.Net's proxy and through
   a `DelegatingHandler`, replayed into the Foundry client. Settles the streaming question and the format.
2. `Bruinet.Mocking.Http`: the recording, matching with volatile fields from two recordings, presets, redaction,
   misses; unit tests over recorded fixtures.
3. In process: `AddBruinetHttpReplay()`, the Azure transport, the hermetic connect guard; the Todo sample's sync
   recorded and replayed on MauiHeadless and bUnit.
4. Out of process: the proxy in `MockApiServer`; the Todo web head replayed in Chromium.
5. `Hermetic` in UAT: the Todo suite runs hermetic, HTTP and SQL replayed, in parallel.
6. `bruinet http stale|diff|reviewed`; the MCP tools; docs (a guide beside `sql-replay.md`), an AD beside AD-031.

## Done when

- The Todo UAT suite runs with `| Hermetic | true |` on MauiHeadless, bUnit and Chromium with no backend and no
  database running, every scenario green, in parallel collections.
- A model call (the spike's Azure OpenAI chat, streamed and not) replays into the Foundry client with no network.
- A changed backend answer shows in `bruinet http diff` and fails `reviewed` until accepted.
- A SQLite suite runs hermetic with `db | sqlite | copy`: each test its own seeded file, in parallel.
- No recording holds a secret (a test records a call with a key and finds `***`).

## Open questions, with proposals

| # | Question | Proposed | Why |
| --- | --- | --- | --- |
| Q1 | The other project's integration and hermetic UI tests: copy its way, or this one | look at it first: name the repository and the tests, and this design takes what works there | it is already in use; agreeing with it saves a second way |
| Q2 | WireMock.Net for both transports, or a handler in process | both: the handler in process (no port, fastest, works on bUnit and MauiHeadless), WireMock.Net's proxy for an app in a process of its own | one format, the cheapest transport per head |
| Q3 | One recording for HTTP and SQL, or two files | two files per test, one `Recordings` folder, one mode switch (`Hermetic`) | each replay keeps its own review commands; a suite may use one without the other |
| Q4 | Record twice by default | yes, as SQL replay: the second recording finds the volatile fields; presets cover the usual ones for a single recording | a model's request ids and an API's timestamps differ every run |
| Q5 | Replay a stream at recorded pace | at once by default; recorded pace when a test measures waiting (a loading spinner) | most tests want speed; a few need the timing |
| Q7 | A SQLite app (Construction): replay its database or not | not by default: `sqlite | copy`, a seeded copy per test; `replay` only when a suite asks | the engine runs in process already; replaying it saves nothing and stops proving the real SQL |
| Q8 | Where the settings live | `uat.config.md`'s `## Dependencies`, a run's `BRUINET_DEP_<NAME>` over a row | one place per suite, as its other settings; a run (CI, an agent) can switch one dependency to `real` or `record` |
| Q6 | Tomorrow's model calls: through this, or Tomorrow's own call queue fakes | through this, after the spike: Tomorrow's Foundry adapter takes `HttpReplayTransport` | real responses, recorded once, instead of hand-written fakes |
