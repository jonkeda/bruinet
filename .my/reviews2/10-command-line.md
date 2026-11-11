# Bruinet's command line: review and proposals

Written 2026-11-11 at `b2ea4570`. Review 10 of `additional-reviews-proposal.md`.
Read from `tools/Bruinet.Cli`, `tools/Scripts`, `samples/TestRunner.ps1`, `docs/run/cli.md`, AD-028 and `.github/PIPELINE.md`.
Nothing was built. The built copy in `.bruinet/mcp` was run with `--help` only, on the root and 27 commands; every one printed help and exited 0.

## Outcome

AD-028 holds in the large: there is one executable, every command has help text, a wrong call exits 2 with a pointer to `--help`, and stdout and stderr are kept apart almost everywhere.
The command tree has grown past what the decision and the guide describe: `sql`, `http`, `tests`, `why`, `voice` and `mcp-host` came later, and they did not all take the shared words.
The weak part is the exit codes. Only a named list of exceptions becomes exit 2, so a database that does not answer or a broken JSON file ends as exit 1, the same code the four gate commands use for "found some".
The scripts are mostly sound; the six `Create….Bat` files are one command written six times, and `samples/TestRunner.ps1` is dead.

Nothing is shipped, so every rename below is still free.

## What is good and should stay

- One `Action` wrapper turns known problems into a message and exit 2 (`BruinetCommand.cs:74-101`).
- Parse errors go to stderr with `See 'bruinet <command> --help'.` and exit 2 (`BruinetCommand.cs:61-67`).
- A group on its own prints its help (`BruinetCommand.cs:121-134`).
- `--base` means the same in `tests affected`, `sql diff` and `http diff` (`SelectionCommands.cs:41`, `SqlCommands.cs:84`, `HttpCommands.cs:43`).
- `--out` is the one word for an output path (`ScenarioCommands.cs:23`, `SelectionCommands.cs:21`, `SqlCommands.cs:38`, `:251`, `:276`).
- A command that would ask does not start without `--yes` when no one is at a terminal (`CommonOptions.cs:39-45`).

## The commands

Three things hold for every row and are left out of the table.
A wrong call exits 2 (`BruinetCommand.cs:61-67`).
Ctrl+C exits 1 with "Stopped." (`BruinetCommand.cs:81-85`).
An exception outside the caught list exits 1 with a stack trace; see finding 1.

| Command | What it does | Exit codes of its own |
| --- | --- | --- |
| `scrape new <name> --url` | Makes a scraped UI test project | 0 made; 2 a field is wrong or not confirmed (`ScrapeCommands.cs:57`, `:64`) |
| `scrape list` | Lists the scraped projects | 0, also when there are none (`:97-101`) |
| `scrape status` | The eight steps and the next one | 0 |
| `scrape check` | Opens the start URL | 0 answers and signed in; 1 not (`:146`) |
| `scrape explore` | Explores the app under its guards | 0 within guards; 1 a guard stopped it (`:209`); 2 not confirmed (`:193`) |
| `scrape record` | Records by hand | 0; 2 no terminal (`:226`) |
| `scrape draft` | Drafts page objects | 0; 1 a draft was put back (`:280`) |
| `scrape verify` | Looks members up in the app | 0; 1 a member not found once (`:376`) |
| `scrape resolve <session>` | Resolves a test recording | 0; 1 gaps (`:311`) |
| `scrape draft-test <session>` | Drafts a recorded test | 0; 1 gaps, problems or a file not written (`:345`) |
| `scrape config get` | Prints `scraper.config.md` | 0 |
| `scrape config set`, `add`, `remove` | Changes `scraper.config.md` | 0, also when unchanged (`:415-416`) |
| `scrape install-browser` | Installs Chromium | 0; 2 the install failed (`:427`) |
| `data validate [file]` | Checks test sets and scenarios | 0; 1 problems (`DataValidatorApp.cs:138`); 2 no config or assembly (`:37-38`) |
| `data phrases [file]` | The phrases steps can use | 0; 2 |
| `data references <namespace>` | Who uses a namespace | 0; 2 |
| `data snippet <file:line>` | A method for an unbound step | 0; 2 |
| `data describe <type>` | A test-data type's fields | 0; 2 no such type (`:380`) |
| `data list` | The test sets | 0; 2 |
| `data schema [--check]` | Writes or checks `testdata.schema.json` | 0; 1 stale (`:458`); 2 |
| `scenario preview <file>` | A feature file as an HTML page | 0; 2 no file (`ScenarioCommands.cs:30`) |
| `scenario results delete` | Deletes old results | 0; 2 nothing named or not confirmed (`:63`, `:70`, `:77`) |
| `tests touch-map <timings>…` | Writes `touch-map.json` | 0 |
| `tests affected` | The tests a change reaches | 0 always; 2 no touch map (`TouchMap.cs:81-84`) |
| `sql schema --connection` | Writes the schema snapshot | 0; 1 the database does not answer (finding 1) |
| `sql stale --schema --recordings` | Recordings a schema change made stale | 0; 1 any stale (`SqlCommands.cs:206`) |
| `sql diff --recordings` | What re-recorded tests changed | 0 always (`:135`) |
| `sql reviewed --recordings` | The gate on unread recordings | 0; 1 any unread (`:153`) |
| `sql capture start`, `stop`, `read` | An Extended Events session | 0; 1 the database does not answer (finding 1) |
| `sql report --log` | The database's share of each test | 0 |
| `http stale --recordings` | Recordings whose replay missed calls | 0; 1 any stale (`HttpCommands.cs:35`) |
| `http diff --recordings` | What re-recorded calls changed | 0 always (`:86`) |
| `http reviewed --recordings` | The gate on unread recordings | 0; 1 any unread (`:104`) |
| `why <file:line>` | Where a failure parted from green | 0; 2 nothing to compare (`WhyCommand.cs:47-51`) |
| `voice` | Lines of video scripts with no clip | 0; 1 lines without a clip; 2 a script has errors (`VoiceCommand.cs:176`) |
| `controls generate --input` | Each `.tpl.cs` to its `.gen.cs` | 0; 1 a file failed (`GenerateCommand.cs:111-115`); 2 no input (`:46`, `:59`) |
| `version` | Prints `bruinet <version>` | 0 |
| `mcp` | The MCP server over stdio | 0 when the client closes (`ServerCommands.cs:21`) |
| `mcp-host --build` | The MCP server, restarted per build | 0 (`ServerCommands.cs:46`) |
| `lsp` | The language server over stdio | 0 (`ServerCommands.cs:62`) |

Machine-readable output is `--json`, one document on stdout.
It exists on `scrape new`, `list`, `status`, `check`, `explore`, `draft`, `verify`, `resolve`, `draft-test`, on `data validate` and `phrases`, on `scenario results delete`, `tests affected` and `why`.
No other command has it.

## Findings

### 1. Exit 1 means both "found problems" and "crashed"

- The wrapper catches Playwright, timeout and eight named exception types; every other exception leaves it (`BruinetCommand.cs:86-100`).
- `RunAsync` invokes with System.CommandLine's default settings, whose handler prints the exception and returns 1 (`BruinetCommand.cs:70`).
- `SqlException` is not in the list, so every `sql` command with `--connection` exits 1 with a stack trace when the server does not answer (`SqlCommands.cs:51`, `:220`, `:239`, `:256`).
- `JsonException` is not in the list, so `sql stale` with a damaged `schema.json` exits 1, the code it uses for "recordings are stale" (`SqlCommands.cs:181`, `:206`; `SchemaSnapshot.cs:69`).
- The same holds for `tests affected` with a damaged touch map (`TouchMap.cs:81-83`) and for `sql capture stop` and `read` with a damaged state file (`ExtendedEvents.cs:30`).
- `git` missing from the path is a `Win32Exception` from `Process.Start`, so `tests affected`, `sql diff` and `http diff` exit 1 (`SelectionCommands.cs:103`, `SqlCommands.cs:166`).
- Ctrl+C exits 1 as well (`BruinetCommand.cs:81-85`).
- `voice` dereferences `ClipLibrary.Find(…)!`, which returns null without a folder, so that case is a null reference and exit 1 (`VoiceCommand.cs:40`, `ClipLibrary.cs:152-159`).
- A CI step that reads 1 as "the gate found something" cannot tell it from "the gate did not run".

### 2. Exit 2 also carries three different meanings

- "Not confirmed" is 2 (`ScrapeCommands.cs:64`, `:193`; `ScenarioCommands.cs:77`), as `cli.md:96` says, though the call was right and the tool could run.
- `why` exits 2 when the scenario has no failure or no green run, which is an answer and not a wrong call (`WhyCommand.cs:47-51`).
- `voice` exits 2 when a video script has errors, which is a problem found in the user's files (`VoiceCommand.cs:50-54`, `:176`), while `data validate` exits 1 for the same kind of thing.

### 3. One option, two meanings

| Option | Means here | And there |
| --- | --- | --- |
| `--suite` | A suite's name (`ScenarioCommands.cs:22`, `:55`; `WhyCommand.cs:18`) | A project folder (`SelectionCommands.cs:84-85`; `VoiceCommand.cs:12`) |
| `--solution` default | Nearest folder up with a solution (`CommonOptions.cs:10`) | This folder, for `mcp` and `mcp-host` (`ServerCommands.cs:12`, `:19`, `:32`, `:42`) |
| `--references` | `projects` or `packages` (`ScrapeCommands.cs:31`) | A JSON file (`SqlCommands.cs:39`) |
| `--folder` | The new project's folder (`ScrapeCommands.cs:27`) | A folder on the SQL server (`SqlCommands.cs:214`) |
| `schema` | `data schema` writes `testdata.schema.json` (`DataCommands.cs:20`) | `sql schema` writes the database snapshot (`SqlCommands.cs:44`) |

- `cli.md:79` says every command spells `--solution` the same; `mcp` does not.
- `sql stale --version name=value` reuses the word of the root's `--version` for something else (`SqlCommands.cs:177`).

### 4. One thing, several spellings

- Which test project: `--project` in `scrape` (`CommonOptions.cs:13`), the current folder or `--config` in `data` (`DataCommands.cs:29`), `--suite <folder>` in `tests` and `voice`, `--suite <name>` in `scenario` and `why`, `--recordings <folder>` in `sql` and `http`.
- A scenario's place: `why <file>:<line>` and `data snippet <file>:<line>` (`WhyCommand.cs:17`, `DataCommands.cs:17`), but `scenario results delete <file> --line N` (`ScenarioCommands.cs:53-54`).
- A browser window: `scrape explore` and `verify` take `--headless` and show a window by default (`ScrapeCommands.cs:153`, `:167`, `:358`); `scrape check` takes `--visible` and shows none by default (`ScrapeCommands.cs:136`, `:141`).
- Several values: repeat the option (`--test-id`, `ScrapeCommands.cs:30`), separate with `;` (`controls generate --input`, `GenerateCommand.cs:16`, `:27`), or separate with `,` (`sql schema --data`, `SqlCommands.cs:40`, `:55`).
- In `tests affected`, `--files a b c` takes many values after one option, while `--app` and `--always` must be repeated and their help does not say so (`SelectionCommands.cs:39`, `:42`, `:43`).
- The version: the `version` command (`BruinetCommand.cs:107-118`) and the root option `--version` that System.CommandLine adds, shown in `bruinet --help`.
- The error prefix: none (`BruinetCommand.cs:98`), `error:` (`DataValidatorApp.cs:37`), `Error:` (`GenerateCommand.cs:45`, `:58`).

### 5. Verbs

- AD-028 says commands are verbs (`ad-028-one-command-line.md:9`). `stale`, `reviewed`, `affected`, `phrases`, `references`, `snippet`, `schema`, `touch-map`, `status` and `list` are not.
- `reviewed` reads as "mark as reviewed", but it is the gate; the marking is `diff --accept` (`HttpCommands.cs:44`, `:94`; `SqlCommands.cs:85`, `:143`).
- `diff --accept` writes `reviewed.json` from a command that otherwise only reads (`HttpCommands.cs:80-84`; `SqlCommands.cs:129-133`).
- `voice` has no verb at all: `--fill`, `--ask`, `--prune` and `--index` are four acts written as flags (`VoiceCommand.cs:13-16`).
- `why` and `voice` are top-level beside the groups, though both live in the `Scenario` namespace and act on a scenario or a suite (`BruinetCommand.cs:46-47`).

### 6. Two commands spend or delete without asking

- `voice --ask` buys speech from the voice's service with no question and no `--yes` (`VoiceCommand.cs:14`, `:28`).
- `voice --prune` deletes clips with no question (`VoiceCommand.cs:15`, `:172`).
- `scenario results delete` asks, and is the model (`ScenarioCommands.cs:74-78`).
- `--ask` is also the wrong word: on this command line `--yes` means "do not ask", and `--ask` means "buy".

### 7. Output that breaks AD-028's stdout rule

- `controls generate` writes progress and warnings to stdout: "Analyzing…", "Successfully generated", "Warning:" (`GenerateCommand.cs:72`, `:82`, `:87`, `:108`).
- `controls generate` prints a stack trace for an unexpected exception (`GenerateCommand.cs:97-103`), which the wrapper's rule forbids (`BruinetCommand.cs:73`).
- `scenario results delete --json` prints nothing on stdout when there is nothing to delete (`ScenarioCommands.cs:72-73`), so a script parsing JSON gets an empty document.
- `data validate --fix` writes "Renamed…" and "Fixed…" lines to stdout before the result (`DataValidatorApp.cs:158`, `:181`, `:191`), which under `--json` is not one document.
- `voice` writes its report, the missing lines and "Wrote…" all to stdout (`VoiceCommand.cs:95`, `:151-160`).

### 8. The gates have no `--json`

- `sql stale`, `sql reviewed`, `http stale` and `http reviewed` are the commands CI runs, and none has `--json` (`SqlCommands.cs:143`, `:178`; `HttpCommands.cs:25`, `:94`).
- Their count goes to stderr and their list to stdout as bare paths (`HttpCommands.cs:29-34`, `:98-103`).
- `data list`, `describe`, `references` and `schema` have no `--json` either (`DataCommands.cs:16-20`), while `validate` and `phrases` do.
- `data` still accepts a hidden `--format json`, the old tool's spelling (`DataCommands.cs:34-35`, `:51`); AD-028 rules out an alias for `bruinet-data` (`ad-028-one-command-line.md:22`).

### 9. The docs are behind the tree

- `docs/run/cli.md` has no row for any `sql` command or for `voice` (`cli.md:35-65`); they are only in `docs/guides/sql-replay.md` and `docs/guides/narrated-recordings.md`.
- `cli.md:73` leaves out `mcp-host --runs` (`ServerCommands.cs:34`).
- AD-028's list of commands stops at `scrape`, `data`, `scenario`, `controls`, `lsp` and `mcp` (`ad-028-one-command-line.md:8`).
- `data validate`'s help says `<file>` in the group listing, though it is optional (`DataCommands.cs:14`); the same for `phrases`.
- No guide names a command that does not exist; I searched `docs`, `.docs`, `.github` and the root files for `bruinet <word> <word>`.
- `.github/PIPELINE.md` describes `.github/workflows/build-and-test.yml`, .NET 9 and "Phase 001" (`PIPELINE.md:5`, `:134`, `:337`); the workflows are `ci.yml`, `release.yml` and `security.yml`. The whole file is stale.

### 10. The scripts

| Script | Verdict | Why |
| --- | --- | --- |
| `bruinet.ps1` | Keep | Runs the tool from source and passes its exit code (`bruinet.ps1:9-10`) |
| `CreateHtml.Bat` and five more | One command, six times | They differ only in line 10, the `--input` folders |
| `select-tests.ps1` | Keep, read `--json` | It parses the text output with a regex (`select-tests.ps1:79-80`) |
| `scrape-loop.ps1` | Keep | It uses `--json` and exit codes as `cli.md` describes (`scrape-loop.ps1:227-233`) |
| `timings.ps1` | Should be a command | Reads the same `timings-*.csv` as `tests touch-map`; nothing in the repo names it |
| `check-test-guard.ps1` | Keep, fix exit | Passes when `git diff` fails (below) |
| `check-api-changes.ps1` | Keep, fix exit | Passes when `git diff` fails (below) |
| `mutate.ps1` | Keep, fix message | Any Stryker failure is reported as a low score (`mutate.ps1:90`, `:98-100`) |
| `coverage.ps1`, `test-maui-headless.ps1`, `pack.ps1` | Keep | CI runs them; each fails when a step fails |
| `update-public-api.ps1` | Keep | A developer's tool; AD-033 names it |
| `repeat-run.ps1` | Keep or drop | Only `CHANGELOG.md` names it; it never fails, by design (`repeat-run.ps1:88`) |
| `run-android-tests.ps1` | Likely dead | Only `CHANGELOG.md` names it; `docs/run/maui-android.md` gives the steps by hand |
| `start-appium.ps1` | Dead | One line (`start-appium.ps1:9`) that `maui-android.md:57` already shows, with a different driver flag |
| `samples/TestRunner.ps1` | Dead | A hard-coded path into another repository (`TestRunner.ps1:4`); nothing names it |

- `check-api-changes.ps1` sets `$ErrorActionPreference`, which does not stop on a failed `git`. With a base revision that is not fetched, `git diff` returns nothing and the script prints "no public API file changed" and exits 0 (`check-api-changes.ps1:38-42`).
- `check-test-guard.ps1` has the same shape: an empty `git diff` ends in "no tests taken away" and exit 0 (`check-test-guard.ps1:40`, `:89-91`). CI runs both against `origin/<base>` (`ci.yml:37`, `:42`).
- `samples/TestRunner.ps1` prints "TEST PASSED" and exits 0 after a failed pipe exchange, since the catch does not exit (`TestRunner.ps1:69-71`, `:79`).
- The six `.Bat` files and `scrape-loop.ps1` reach into `bin` after building (`CreateHtml.Bat:9`, `scrape-loop.ps1:270`), which AD-028 allows for scripts.
- `timings.ps1`'s example names a consumer's suite, `tests\Tomorrow.Studio.UITests` (`timings.ps1:26`).

## Renames

| Now | Proposed | Why |
| --- | --- | --- |
| `--suite <folder>` in `tests`, `voice` | `--project <folder>` | `--suite` stays the suite's name; `--project` already means the project |
| `scrape check --visible` | `--headless`, a window by default at a terminal | One word for the window, one default |
| `scenario results delete <file> --line N` | `<file>[:<line>]` | As `why` and `data snippet` |
| `why <file:line>` | `scenario why <file:line>` | A group for every command about a scenario |
| `http reviewed`, `sql reviewed` | `http check`, `sql check` | A verb, and it does not read as "mark reviewed" |
| `http diff --accept`, `sql diff --accept` | `http accept`, `sql accept` | A writing act is its own command |
| `voice`, `--fill`, `--ask`, `--prune`, `--index` | `voice list`, `fill`, `buy`, `prune`, `index` | Verbs as commands; `buy` and `prune` take `--yes` |
| `controls generate --input a;b` | `controls generate <paths>…` | Arguments, as `tests touch-map <timings>…` |
| `sql schema --references <file>` | `--references-file <file>` | `--references` is `projects` or `packages` in `scrape new` |
| `sql stale --version name=value` | `--stamp name=value` | `--version` is the root's |
| `sql capture start --folder` | `--server-folder` | `--folder` is a local project folder in `scrape new` |
| `sql schema` or `data schema` | `sql snapshot` | Two files called a schema; the SQL one is called a snapshot in its own help |
| `version` and `--version` | Keep `--version` only | Two ways to one answer |
| `data … --format json` (hidden) | Remove | The old tool's alias, ruled out by AD-028 |

## Proposals

1. **Make exit 1 mean only "found".** Set the invocation's default exception handler off and catch everything in `Action`: a message on stderr and exit 2, the stack trace only with an environment variable. Add `SqlException`, `JsonException`, `IOException` and `Win32Exception` by name with plain messages. Small, and the one that matters for CI.
2. **Give Ctrl+C its own code.** 130, as shells do, or 2; not 1.
3. **Decide what 2 covers.** My proposal: "not confirmed" stays 2; `why` with nothing to compare becomes 1 with its message on stdout; `voice` with a broken script becomes 1.
4. **Make the renames above** in one pass, before the first shipped version, and update `cli.md` and the skills in the same commit.
5. **Ask before spending or deleting in `voice`,** with `--yes`, as `scenario results delete` does.
6. **Give `mcp` and `mcp-host` the shared `--solution`** with its walk up to the solution file, or name theirs `--folder` and say so in `cli.md`.
7. **Add `--json` to the four gates** and to `data list`, `describe`, `references` and `schema`; print `[]` or an object when there is nothing, never an empty stdout.
8. **Move `controls generate`'s progress to stderr** and drop its stack trace; one error prefix for the whole tool.
9. **Bring `cli.md` and AD-028 up to the tree:** rows for `sql`, `voice` and `--runs`, and a test in `Bruinet.Cli.Tests` that fails when a command has no row in `cli.md`.
10. **Replace the six `Create….Bat` files** with `bruinet.ps1 controls generate <folder>` in the skills and guides, or with one `generate-controls.ps1 -Stack <name>`.
11. **Make `timings.ps1` a command,** `bruinet tests timings <folder>`, beside `touch-map`, which already reads the same files.
12. **Have `select-tests.ps1` read `tests affected --json`** instead of matching the text.
13. **Make the two guard scripts fail when `git` fails:** check `$LASTEXITCODE` after each `git diff` and exit 2.
14. **Delete `samples/TestRunner.ps1`, `start-appium.ps1` and `.github/PIPELINE.md`;** decide on `run-android-tests.ps1` and `repeat-run.ps1`.

## Not settled

- That an uncaught exception exits 1 is read from System.CommandLine 2.0's default handler, not run. No test in the source sets or checks it.
- `bruinet --version` was not run; whether it prints the same text as `bruinet version` is unknown.
- That the guard scripts pass on a failed `git diff` is read from the scripts, not run.
- Whether the owner still runs `timings.ps1`, `repeat-run.ps1` and `run-android-tests.ps1` by hand cannot be seen from the repository.
- `data`'s remaining exit paths were read by their `return` and `throw` lines, not line by line through `DataValidatorApp.cs:200-530`.

## For the owner

1. **The renames.** My proposal: all of them now, since nothing is shipped and the skills are the only callers to update.
2. **The groups' nouns versus AD-028's "commands are verbs".** My proposal: amend AD-028 to allow a noun that names the list a command prints (`list`, `status`, `phrases`, `stale`, `affected`), and rename only `reviewed`, which misleads.
3. **Scripts as commands.** My proposal: only `timings.ps1` becomes one; the guard, coverage, mutation and pack scripts are about Bruinet's own repository and stay scripts.
