# AD-031: A Replayed Test Answers From The Database It Recorded, And The Database Judges The Gate

`Bruinet.Mocking.Data` records the commands a test sends to its database and what came back, and
replays them with no database: a test's own recording (`<head>/<test>.json`), matched by command text
with writes as ordered barriers, values that change between runs bound rather than matched. A run's
mode is `BRUINET_SQL_REPLAY`. Replay answers only what was recorded; anything else is a miss that
names its command. A recording is made against the real database, recorded twice, stamped with the
objects it touched, and reviewed before it goes in.

A test replays during development and in parallel runs. It runs against the real database at the
gate, and whenever it tests the database itself: a migration, a procedure's logic, a trigger,
locking or timing. A stale recording or a miss is recorded again, never edited by hand.

**Why:** the cost of a database in a UI test is having one: making, seeding and resetting it, and
sharing it, which keeps a suite serial. On the spike the app's own queries were about 5% of a test
and resetting one table 37%; on Tomorrow's studio suite the database was a median 23% of a test's
steps. A system of 2000 tables pays the reset and the sharing many times over, and that is what
replay removes (`.my/sql-replay/proposal.md`, `spike-results.md`, `tomorrow-measure.md`). The spike
and the package replayed 20 runs per head in parallel without a miss.

**Ruled out:**
- an in-memory or SQLite database standing in for SQL Server: another engine, so other SQL, other
  errors and no procedures;
- mocking the app's repositories: the code under them, its SQL included, goes untested, and a UI
  test can't reach them;
- making up results for commands not recorded: a test would pass on data no database gave;
- a proxy speaking SQL Server's wire protocol (TDS): a large protocol to keep, for what the
  provider's interfaces already show;
- a shared test database cleaned between tests: the collisions and the serial runs replay removes.

**The packages earn their place by AD-026:** the core (netstandard2.0) has no provider; EF Core,
SQL Server's spatial types, ASP.NET Core and System.Web each have their own package, so an app takes
only what it uses.

**Broken when:** replay answers a command it didn't record; a gate passes on replay alone; a test's
session is found through a static shared by tests in parallel; a recording goes in without its
review (`bruinet sql check`); or a schema change to an object a recording touched leaves it
replaying without a stale report.
