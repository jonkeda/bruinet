# D3. The command line

Design for R2-T88, R2-T89 (review 10, P1–P4, P6–P8, and its Renames table). `bruinet` in `tools/Bruinet.Cli`.

**Approved by the owner as proposed, 2026-11-11.**

## Decisions asked

1. **Exit codes.** My proposal: the table below, and nothing else ever exits 1.
2. **Renames now, no aliases.** My proposal: review 10's Renames table in one commit, as for the MCP tools (D2), before the first `Shipped` line.
3. **`voice` becomes a group.** My proposal: `voice list`, `fill`, `buy`, `prune`, `index`; `buy` and `prune` ask, `--yes` skips the question.

## Exit codes

| Code | Means | Examples |
| --- | --- | --- |
| 0 | Ran, nothing found | `sql check` with every recording reviewed |
| 1 | Ran, found something | stale recordings, `data validate` errors, a voice script with errors, a failing scenario |
| 2 | Could not run | a wrong argument, a missing file, an unreachable database, a damaged JSON, `git` not on the path, not confirmed |
| 130 | Stopped by Ctrl+C | |

- `why` with nothing to compare (no failure, no green run) is an answer: 0 with the message on stdout.
- "Not confirmed" stays 2: nothing ran.
- Implementation: `BruinetCommand.RunAsync` turns off System.CommandLine's default exception handler and the wrapper catches every exception: one line `bruinet: <message>` on stderr, exit 2; the stack trace only with `BRUINET_DEBUG=1`. `SqlException`, `JsonException`, `IOException`, `Win32Exception` get plain messages by name. `OperationCanceledException` from Ctrl+C exits 130.
- `controls generate`'s own catch and stack trace go; it uses the wrapper.

## Output

- stdout is the answer only; progress, warnings and counts go to stderr (`controls generate`, `data validate --fix`, `voice`).
- `--json` on the four gates (`sql check`, `sql stale`, `http check`, `http stale`) and on `data list`, `describe`, `references`, `snapshot`. With nothing to report: `[]` or an object, never an empty stdout.
- The hidden `--format json` on `data` goes.

## Options

- `--suite` is a suite's name; a test project's folder is `--project` everywhere (`tests`, `voice`, `scrape`).
- `--solution` walks up to the solution file on every command, `mcp` and `mcp-host` included. Tomorrow's `.mcp.json` passes `--solution .`, which still works.
- A scenario's place is `<file>[:<line>]` everywhere.
- One way to give several values: repeat the option; `controls generate` takes paths as arguments.
- `--version` only; the `version` command goes.

## Tests

`Bruinet.Cli.Tests`: one test per code (0, 1, 2, 130) through the wrapper; a test that every command has a row in `docs/run/cli.md` (R2-T91); a test that `--json` output parses for each command that has it, on an empty input too.

## Who changes with it

| Where | What |
| --- | --- |
| Bruinet | `docs/run/cli.md`, AD-028's command list, `sql-replay.md`, `http-replay.md`, `narrated-recordings.md`, skills naming `voice` or `why`, `tools/Scripts/select-tests.ps1` (reads `--json`, R2-T92), `changes.md` |
| CI | Gates read 1 as "found" and 2 as "broken"; `ci.yml` steps that call `bruinet` are checked |
| tomorrow (phase 9) | `tools/Select-Tests.ps1` (`tests touch-map --suite` → `--project`; it throws on any non-zero, which stays right), `.mcp.json` unchanged |
| construction (phase 9) | `.github/skills/module-videos/SKILL.md:82-83` (`voice --suite … --fill --index` → `voice fill --project …`, `voice index …`; `--prune` → `voice prune --yes`) |
| kofflard | Nothing calls `bruinet` |
