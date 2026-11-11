# HTTP Replay

A test can run without the services its app calls: Bruinet records each outgoing HTTP call and its answer once, against
the real service, then answers the same calls from the recording. With [SQL replay](sql-replay.md) beside it, a UI test
or an integration test needs nothing outside its process: it is **hermetic**. The real services stay the judge at the
gate, as the real database does for SQL replay. The decision is
[AD-034](../../.docs/decisions/ad-034-a-dependency-is-replayed-stubbed-or-copied-by-its-kind.md).

## When To Use It

- **Third parties a service or app calls**: payment, maps, identity, a model. The app's own code runs; what it calls is
  pinned to what it answered once.
- **Model calls**: no tokens spent, no five-second waits, the same answer every run (the `model` preset).
- **Parallel runs and CI without services**: a replayed test shares nothing with another.
- **Not for your own backend's behaviour**: a hand-written stub (`MockApiServer`) says what a test means better than a
  recording, and a test that checks what the backend received reads the stub's request log.

## Recording And Replaying

`BRUINET_HTTP_REPLAY` sets the mode: `Record` (calls go to the real service and are written), `Replay` (answered from
the recording, nothing leaves the process), `Refresh` (record again and list which answers changed). Unset, replay is
off. Each test has its own recording, `Recordings/http/<test>.json`, beside SQL replay's.

```csharp
// The test's session: one per test, never shared.
var session = HttpReplaySession.FromEnvironment(recordingsFolder, testName, options);

// An app in the test process: every IHttpClientFactory client goes through it...
services.AddBruinetHttpReplay(session);
// ...or a client the app makes itself, or an Azure SDK client.
var client = new HttpClient(HttpReplayHandler.Create(session));
var transport = new HttpClientTransport(new HttpClient(HttpReplayHandler.Create(session)));

// At the test's end: save (record), write the call log, and fail on misses.
session.Finish();
session.ThrowIfMissed();
```

Record twice. What differs between the two recordings of a call (a request id, a time, an id in the path) is marked
**volatile**: it matches anything, and the live value is **bound**, so later answers that carry the recorded one carry
the live one instead (an app that makes a new id each run gets its own id back). Presets name the usual ones without a second recording: `model` (request ids, `temperature`, `seed`,
`user`, timestamps) and `oauth` (state, nonces, tokens).

## Matching

A call is answered by the recorded exchange with the same method, path and query, and the same body: a JSON body field
by field, its volatile fields and a preset's left out. The same call made again gets the recorded answers in order (a
poll that says "pending", then "done"); one made more often than recorded gets the last answer again. A call the
recording can't answer is a **miss**: it fails with the call, the nearest recorded one and how they differ.

```text
HTTP replay miss in Add a todo.json: POST /todos; nearest recorded is exchange 1 POST /todos:
$.title is '"Bread"' where '"Milk"' was recorded.
```

A call that got no answer when recorded (a refused connection, an unreachable backend) fails the same way in replay.
Each answer's duration is recorded; `| HttpReplayPace | recorded |` waits it out, for a test that shows a slow server.

## Secrets

- `Authorization`, `api-key`, `X-Api-Key`, `Ocp-Apim-Subscription-Key`, cookies and named query parameters and JSON
  fields (`password`, `client_secret`, tokens) are written as `***`, and a `***` matches whatever the live call sends.
- `| HttpRedact | x-functions-key |` adds a suite's own.
- The same names are redacted inside XML and SOAP bodies (an element's text, an attribute's value) and in each part of
  a streamed answer, and a `Location` header's query is redacted too, its OAuth `code` included. Elsewhere `code` is
  kept: in a body it is too often an error or country code, which replay hands back to the app.
- Before saving, a recording is scanned for what looks like a key (model and cloud keys, tokens, private keys, an Azure
  resource key after a key-like name); one found stops the save, naming the exchange and where in it.
- A body is otherwise kept whole: names, emails and prompts sent to a model go into git with the recording. Record
  against test data.

## Streamed Answers

Server-sent events and other streamed answers are recorded part by part with the gaps between them. They replay at once
by default; `| HttpReplayPace | recorded |` keeps the gaps.

## An App In A Process Of Its Own

The web head's app, a Windows app or an Android app calls a `MockApiServer`, its base URLs pointed there as for stubs:

```csharp
proxy.Record("https://api.example.com");   // what no stub answers goes on to the real service
// ... the test runs ...
proxy.CopyRecordedTo(session); session.Finish();

replay.Replay(HttpRecording.Load(path)!);  // answers as recorded, in order
Assert.Empty(replay.ReplayMisses());       // a miss answers 599 with the miss as its body
```

The recording is the same format either way, so one made in process replays through the server and back.

## A Suite's Dependencies

A suite says per dependency how its tests get it, in `uat.config.md`:

```markdown
## Runtime

| Field | Value |
| --- | --- |
| Hermetic | true |

## Dependencies

| Name | Kind | Mode | Where |
| --- | --- | --- | --- |
| backend | http | stub | `TodoServerStub` |
| model | http | replay | `https://*.openai.azure.com, preset model` |
| db | sqlite | copy | `TestData/seed.db` |
```

| Kind | Modes |
| --- | --- |
| `http` | `real`, `record`, `replay`, `refresh`, `stub` |
| `sqlserver` | `real`, `record`, `replay` |
| `sqlite` | `real`, `copy`, `record`, `replay` |

`BRUINET_DEP_<NAME>=real` overrides one row for a run. A `Hermetic` suite refuses a dependency that leaves the process
(`BddDependencies.Problems`). The suite's host reads the rows (`BddDependencies.Find`) and makes each: a session
(`HttpReplaySession.ForMode`, `HttpReplayOptions.ForSuite`), its stubs, or a copy.

**SQLite is copied, not replayed.** An embedded database already runs in the test process and answers in microseconds;
replaying it saves nothing and stops proving the real queries, constraints and migrations. `SqliteTemplate.EnsureAsync`
makes a seeded template once per stamp (the app's own migrations and seed), and `SqliteTemplate.Copy` gives each test
its own file, deleted after.

## A Suite Of Scenarios

The Todo sample's UAT suites show the wiring (`TodoHttpReplay`): the fixture hands the app one handler
(`TodoPlatform.HttpHandler`), each scenario gets a session in `BeforeScenario`, and `AfterScenario` ends it, a miss
failing the scenario. Recordings are kept in git (`Recordings/<head>/http`, with `reviewed.http.json`); call logs
(`*.calls.json`) are not.

```powershell
# working directory: the repository root
$env:BRUINET_HEAD='headless'; $env:BRUINET_HTTP_REPLAY='Record'; dotnet test samples/Todo/tests/Bruinet.Samples.Todo.Uat   # twice
$env:BRUINET_HTTP_REPLAY='Replay'; dotnet test samples/Todo/tests/Bruinet.Samples.Todo.Uat                                # no backend reached
```

A scenario that reads its stub back ("the server should have todos", what one app sent another) has nothing to read in
replay. Tag it `@needs-stub`: a run that records or replays skips it, recording too, so both run the same scenarios in
the same order (an app's work outlives a scenario, and a skipped one changes what the next one calls).

## Review

```bash
# working directory: the repository root
bruinet http stale --recordings <suite>/Recordings/http      # misses or unused calls in the newest replay
bruinet http diff --recordings <suite>/Recordings/http       # what a refresh changed against git
bruinet http accept --recordings <suite>/Recordings/http     # after reading it: every recording marked read
bruinet http check --recordings <suite>/Recordings/http      # the gate: exit 1 while a recording went in unread
```

For agents, the MCP server's `run_scenario {replay: "record" | "replay" | "refresh"}` runs with the mode set, and
`http_calls` gives a scenario's calls in its newest replayed run: each matched, recorded or missed, the misses, and the
recorded calls it no longer makes.

## What Replay Can't Do

- Prove the service still answers that way: the gate runs against the real one, and `Refresh` shows what changed.
- Replace a stub a test reads back: replay sends nothing, so a "the backend received" check needs a stub, or the
  session's `Calls`.
- Guard a client the app makes itself: `AddBruinetHttpReplay` covers `IHttpClientFactory` clients; give any other
  `HttpReplayHandler.Create(session)`.
- Stream while recording: a streamed answer is read whole, then given to the app; replay streams it.
