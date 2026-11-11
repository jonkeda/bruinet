# SQL Replay

A UI test can run without its database: Bruinet records what the test sends to SQL Server (or
SQLite) and what comes back, then answers the same commands from the recording. What it removes is
having a database at all: making one, seeding it, resetting it before each test, and sharing it
between tests that then can't run side by side. The real database stays the judge at every gate.
The decision is [AD-031](../../.docs/decisions/ad-031-a-replayed-test-answers-from-the-database-it-recorded.md).

## When To Use It

- **During development**, with the bUnit head: a Blazor suite with no browser and no database.
- **For parallel runs**: a replayed test shares nothing with another, so suites kept serial by a
  shared test database can run at once.
- **Not at a gate.** Replay can't prove the database still accepts the commands; the gate runs
  against it (`BRUINET_SQL_REPLAY` unset), as the browser judges the bUnit head.

Measure first. The SQL log gives the database's share of each test and how many tests a typical
change reaches; a small share or a wide reach means replay costs more to keep than it saves.

## The Log

`Bruinet.Mocking.Data` logs every command with its time and the objects it names, never its
values. EF Core: `options.UseSqlLog(log)`. ADO.NET: wrap the connection in `LoggingDbConnection`.
An app you can't change is logged by SQL Server itself:

```bash
bruinet sql capture start --connection-from TEST_DATABASE
bruinet sql capture stop --connection-from TEST_DATABASE
bruinet sql capture read --connection-from TEST_DATABASE --out sql-log
bruinet sql report --log sql-log --timings <timings> --schema schema.json
```

`TEST_DATABASE` is an environment variable holding the connection string: `--connection-from` keeps a password out of
shell history and the process list (`--connection "<string>"` works too).

## Recording And Replaying

A run's mode is `BRUINET_SQL_REPLAY`: `Record` against the real database, `Replay` with none,
unset for neither. Each test has a `ReplaySession` and a recording of its own,
`Recordings/<head>/<test>.json`, kept in git.

Record twice. The second recording finds the values that change from run to run (new ids, clocks)
and marks them volatile; replay binds them to the live run's values instead of matching them.

```bash
BRUINET_HEAD=bunit BRUINET_SQL_REPLAY=Record dotnet test <suite>
BRUINET_HEAD=bunit BRUINET_SQL_REPLAY=Record dotnet test <suite>
BRUINET_HEAD=bunit BRUINET_SQL_REPLAY=Replay dotnet test <suite>
```

A command the recording can't answer is a miss: `ReplayMissException` names the command, its
parameters and where it parted from the recording. A test checks `session.Misses` at its end, as the
app may have caught one.

## Wiring The App

The session reaches the app's data access through its composition, never through a static.

| The app reaches its database through | Use |
| --- | --- |
| EF Core, in the test's process (bUnit, a host per test) | `options.UseSqlReplay(session)` in the test's `ConfigureDbContext` |
| EF Core with SQL Server's spatial types | also `options.UseSpatialReplay()` (`Bruinet.Mocking.Data.EntityFrameworkCore.SqlServer`) |
| ADO.NET with a connection it is given | `new ReplayingDbConnection(connection, session)` |
| ADO.NET connections it makes itself | `ReplayingDbProviderFactory` over its provider's factory; the test enters `ReplayScope.Enter(session)` |
| An app in a process of its own (IIS, a started exe) | the test-id hook, below |

In replay the real connection is never opened. Output parameters, return values, several result sets
and errors come back as the provider gives them: a broken key is `SqlException` 2627 again.

## An App In A Process Of Its Own

The app keeps its tests' sessions itself. Give it `BRUINET_SQL_REPLAY` and
`BRUINET_SQL_RECORDINGS=<folder>` in its environment, and the hook in its pipeline:
`app.UseSqlReplayHook()` (`Bruinet.Mocking.Data.AspNetCore`) or `SqlReplayModule` in web.config
(`Bruinet.Mocking.Data.SystemWeb`). Its connections come from `ReplayingDbProviderFactory` or EF's
`UseSqlReplay()`.

A browser test opens `ReplayHook.BeginUrl(app, test)` first; its cookie names the test on every
request after. An API test calls `ReplayHook.BeginAsync` and sends `X-Bruinet-Test`. At its end the
test calls `ReplayHook.EndAsync`, which saves the recording or throws the misses. Without
`BRUINET_SQL_REPLAY` the hook adds nothing.

The control paths begin and end sessions, so they answer callers on the app's own machine only.
An app on another machine (a shared test server) needs `BRUINET_SQL_HOOK_KEY` set in its
environment and in the test's: `BeginAsync` and `EndAsync` then send it as `X-Bruinet-Key`, and
`BeginUrl` puts it in the address, since a browser's first navigation cannot add a header. Anyone
else gets 404. Never set `BRUINET_SQL_REPLAY` on a server others can reach without a key.

## What A Recording Holds

A recording keeps each command's text, its parameters and every row it read, and it goes into git.
Record against seeded test data only, never a copy of production.

- A parameter named like a secret (`password`, `secret`, `token`, `apikey`, `SecurityStamp`,
  `AuthenticatorKey`) is written `***` and is volatile: replay binds the live value, it does not match it.
- A result column is kept as it was read, because replay gives it back to the app: a `PasswordHash`
  written `***` would fail every replayed sign-in. Name a column to redact in the app's environment,
  `BRUINET_SQL_REDACT=Pin,Iban`; the app then reads `***` in replay.
- A value that still looks like a key (a JSON web token, a private key, an AWS, OpenAI or GitHub
  key) stops the save, naming the exchange and column.
- A miss message shows a secret-named parameter as `***`.

## Stale Recordings

A recording is stamped with the objects its commands touched: tables, views and procedures, and the
tables their bodies reach. The stamp comes from a schema snapshot kept in git:

```bash
bruinet sql schema --connection-from TEST_DATABASE --out Recordings/schema.json --data dbo.Kinds --references references.json
bruinet sql stale --schema Recordings/schema.json --recordings Recordings
```

`stale` lists the recordings a schema change reached, and why, without replaying them. A table's
triggers, defaults and checks count as the table. `--data` hashes a reference table's rows with it.
Dynamic SQL (`sp_executesql`) has no dependencies SQL Server can read: list what it reads in
`--references`, or a change to those tables is missed. `--default-schema` is the app's user's.

On SQLite, add `--provider sqlite` (the connection string can't tell the two apart):

```bash
bruinet sql schema --provider sqlite --connection "Data Source=<file>" --out Recordings/schema.json --data Kinds
```

Objects are the tables and views in `main` (`main.places`). A table's hash is its `CREATE TABLE`
text with its indexes and triggers; SQLite keeps no dependencies, so a view's and a trigger's SQL is
read for the tables it names. `--data` hashes the rows in any order. In code, `SchemaSnapshot.ReadAsync`
reads SQLite's catalog when given a `SqliteConnection`.

## Re-recording

A re-record changes files nobody reads line by line. Read the summary, then accept it:

```bash
bruinet sql diff --recordings Recordings
bruinet sql diff --recordings Recordings --accept
bruinet sql reviewed --recordings Recordings
```

`diff` shows each changed test against git: commands added and removed, rows, columns, the columns
whose values changed, rows affected and errors. `--accept` writes `Recordings/reviewed.json`;
`reviewed` fails while a recording went in unread, and `RecordingReview.Unreviewed` does the same in
a test. A re-record that comes out the same needs no review: volatile values and how often a read
repeats don't count.

## What Replay Can't Do

- Answer a command it didn't record: it never makes up a result. Record again.
- Test the database itself: a migration, a procedure's logic, locking, timing.
- Follow code that branches on whether a connection opens (EF's `EnsureCreated` on SQLite): in
  replay nothing opens.
- Reach a `new SqlConnection` in the app's code: the app's connections must come from a factory.
- Share recordings across providers or heads: a recording is one test's, on one head.
