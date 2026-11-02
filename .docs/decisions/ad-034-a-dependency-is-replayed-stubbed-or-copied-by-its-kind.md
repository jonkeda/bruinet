# AD-034: A Dependency Is Replayed, Stubbed Or Copied By Its Kind, And The Real One Judges The Gate

`Bruinet.Mocking.Http` records each outgoing HTTP call a test's app makes and its answer, and replays them with no
service: a test's own recording (`Recordings/http/<test>.json`), matched by method, path, query and a JSON body field by
field, values that change between runs left out of the match. A run's mode is `BRUINET_HTTP_REPLAY`, the same shape as
SQL replay (AD-031). A suite says per dependency how its tests get it, in `uat.config.md`'s `## Dependencies`: an HTTP
service real, recorded, replayed or stubbed; SQL Server real or replayed; SQLite real, copied or replayed.

A scenario that reads its stub back is tagged `@needs-stub` and skipped while a run records or replays, so both run the
same scenarios in order. A SQLite dependency is **copied by default**, not replayed: each test gets its own file from a template the app's own
migrations and seed made once. A `Hermetic` suite refuses any dependency that leaves the process. The real services and
databases judge the gate.

**Why:** an embedded database already runs in the test process and answers in microseconds; replaying it saves nothing
and stops proving the real queries, constraints and migrations, while a copy per test keeps tests parallel. An HTTP
service is the opposite: slow, shared, paid per call (a model) or not reachable from CI, so pinning its answers is what
makes a test hermetic. A per-dependency setting lets one suite replay its model calls, stub its own backend and copy its
SQLite file.

**Ruled out:**
- replaying SQLite by default: no gain, and the real SQL goes unproven (replay stays for a suite that wants it pinned);
- one recording for HTTP and SQL: each keeps its own review commands, and a suite may use one without the other;
- WireMock.Net's scenarios for the order of repeated calls out of process: the second of two quick calls still got the
  first answer; a counter per call hands out the answers instead;
- matching on host and port: a local backend has a new port every run; a hermetic run checks the host;
- an Azure-specific transport: an Azure SDK client takes `HttpClientTransport` over the replay handler;
- replaying a scenario that reads its stub back from the session's calls: what the stub holds is the stub's own logic,
  which a replay does not run; such a scenario needs the stub.

**Broken when:** replay answers a call it did not record; a recording is saved with a secret in it; a hermetic suite
reaches the network; a gate passes on replay alone; or a recording goes in without its review (`bruinet http reviewed`).
