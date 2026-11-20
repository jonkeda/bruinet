# The `bruinet` Command Line

`bruinet` is Bruinet's one command line (AD-028): the Scraper, test data and scenario checks,
feature-file previews, control generation, and the servers the IDE extensions and agents start.
`bruinet --help`, and `--help` after any command, shows what comes next.

## Install

Anywhere:

```powershell
dotnet tool install -g Bruinet.Cli
bruinet --help
```

For a repository's scripts and CI, as a local tool (working directory: the repository root):

```powershell
dotnet new tool-manifest
dotnet tool install Bruinet.Cli
dotnet bruinet --help
```

Inside Bruinet's own repository, run it from source (working directory: the Bruinet root):

```powershell
pwsh tools/Scripts/bruinet.ps1 --help
```

The Scraper drives Playwright's Chromium. Install it once per machine with
`bruinet scrape install-browser`.

## Commands

| Command | Does | Guide |
| --- | --- | --- |
| `bruinet scrape new <Name> --url <start>` | Makes a UI test project with a `scraper.config.md` and an empty `Pages` folder (`--platform`, `--folder`, `--test-id`, `--references`) | [Scraper](../guides/scraper.md) |
| `bruinet scrape list` | The projects with a `scraper.config.md` | |
| `bruinet scrape check [--headless]` | Opens the start URL: does the app answer, signed in, on a host Explore may go to; a window when someone is at a terminal, to sign in | |
| `bruinet scrape explore` | Explores the app by itself under the project's guards (`--dry-run`, `--max-pages`, `--max-depth`, `--max-minutes`, `--delay-ms`, `--url`) | |
| `bruinet scrape draft [--page <key>]…` | Drafts page objects from the recorded pages | |
| `bruinet scrape verify` | Looks the page objects' members up in the running app | |
| `bruinet scrape find <text>` | Finds a text, a placeholder or an attribute's value in the recorded pages, shadow roots included, with the path to each node; `--run` for one run. Exit 1 when nothing holds it | |
| `bruinet scrape look [--url <address>] [--as <account>] [--click <text>]… [--calls]` | Opens a page with the project's saved sign-in (`--as Reader`: that account's, beside it, in a browser profile of its own; a sign-in that ran out is renewed by the config's `RenewSignIn` command, once), clicks what is asked in order (a text as the page says it, `field:<label>`, `css:<selector>`), and prints what the page shows then: headings, buttons, tabs, fields, what is marked required, messages, columns, a table's rows (how many, and the first few), an open list's options, its links (a menu as a map); a dialog's own facts when one is open. `--calls` adds the calls the page's scripts made after the last click (on opening, when nothing is clicked): method, address, status, and what a writing call sent, never a header. Under the project's guards; types and saves nothing. Exit 1 when a click was refused or not found | [Scraper](../guides/scraper.md) |
| `bruinet scrape places show [--write] [--json]` | The list of the app's places (pages, tabs, dialogs, menus, buttons that only call the back end), kept in the suite's `.testing/175-Places.json`: printed as a document; `--write` writes `175-Places.md` beside it, `--json` prints the list itself | [Scraper](../guides/scraper.md#the-places-of-an-app) |
| `bruinet scrape places seed\|routes\|walk\|add` | Fills the list. `seed [--map <file>]…` adds the map's addresses, the suite's page objects' and the recordings' pages; `routes [--as]` reads the routes the app's own scripts name; `walk [--as] [--may-write] [--own <address>]… [--only] [--again] [--links-and-tabs] [--max-minutes] [--max-places] [--max-presses] [--max-depth]` opens each page not yet opened and presses what the config's `Environment` allows, a write held back unless the record is the walk's own (exit 1 when a sign-in page stopped it); `add --from-run <file>` adds the forms tests saved through their page objects | [Scraper](../guides/scraper.md#the-places-of-an-app) |
| `bruinet scrape places read\|fields\|elements\|draft\|drift\|not-shown` | `not-shown <place> <label>… [--clear]` records on a place what a drafted test did not find on the app (a button that only shows in some state), so a draft leaves it out and says so. Reads the list and the captures the walk kept, opening nothing. `read` reads each kept place again with the control signs and components the project names now; `fields` counts the fields by control, what no sign fits first; `elements` counts the app's own elements with what stands for each; `draft <place> [--as form\|row\|form-test] [--name] [--page] [--write]` drafts a class from a place in the suite's shape (`draft-templates.md` beside `scraper.config.md`); `drift [--check]` holds the drafted classes against the list as it is now |
| `bruinet scrape scraper status [--json]` | What the app's places still owe, by group (the first part of its pages' addresses), the group that owes most first: pages and dialog forms without a class, pages of one record, fields without a made control, forms not saved. From the list of places and the suite's source; opens nothing | 1 when something is owed |
| `bruinet scrape scraper replay [--json]` | Reads every kept place again and drafts again every class that was drafted from one, with the signs, holders and templates as they are now; says what differs from the list and the suite (fields that gained or lost a control, classes that would be drafted differently) and which places that owe a class can be drafted. Writes and opens nothing: run it before and after a change | 1 when something differs |
| `bruinet scrape scraper bench [--json]` | Holds the examples (`## Examples` of the config: a place and the class a person wrote for it) against what their places read as now, and says for each difference who is right | 1 when an example names what the page shows and the reading misses |
| `bruinet scrape scraper setup [--set Question=answer]…` | What a person answered about the app before the factory runs (`setup.md` in the app's own Scraper place): what may be written, what runs, what a change kept without a person may touch, when it may ask, when it commits, who accepts a spec, how many browsers walk. Prints it; `--set` writes an answer. A question without an answer takes the careful value | |
| `bruinet scrape scraper modules [--write] [--json]` | Cuts the app into modules from the list of places, without walking it: each module's groups, pages known, kinds and what it owes. `--write` writes `modules.md`, keeping the `Order` and `Do` a person wrote there | |
| `bruinet scrape scraper run [<group>…] [--module <name>] [--batch N] [--no-walk] [--no-test] [--dry-run] [--save]` | Runs loops of the factory (with `--save`: drafts no class, but a save test for each dialog on a record's page that has a drafted class and none yet): each group through walk, read, draft, build and test. What does not build or does not pass twice is put back with a reason. Each loop is written into `state.json` and `ledger.md` in the app's own Scraper place, so a run that stops goes on. Default groups: those that owe something, in the order of `modules.md`, when the setup's `Run` says so | 1 when a rule stopped it: one reason three times in the batch, two loops in a row that kept nothing, the suite does not build, the sign-in ran out |
| `bruinet scrape scraper records [--make <kind>] [--sweep] [--check]` | The records the Scraper may make for itself in the app, by the kinds its own Scraper project adds (`app.AddRecord`): prints per kind what the app holds with a run's marker now. `--make` makes one and writes it down in `records.md`; `--sweep` deletes every marked record of every kind; `--check` exits 1 when one is left | |
| `bruinet scrape scraper patterns [--max N] [--json]` | The parts the kept captures are built from that repeat and that no control stands for, most widespread first: what a control would be worth making for | |
| `bruinet scrape scraper brief <signature> [--name N] [--variant V] [--lifted]` | Writes the brief for making a control from one of those patterns: where to write it, what to read, its occurrences, an example and the hints | |
| `bruinet scrape scraper promote <control> [--to <library>]` | Moves a control's signs from the app's config (`## Control signs`) to the top of a control library's file of signs: what was learned on this app and holds for the library | |
| `bruinet scrape scraper spec <address> --id <id> --title "<title>" [--write] [--results <trx>]` | Drafts a spec (`<id>.spec.md`, status `draft`) from the drafted pages of one address: a rule and an example per page a tab shows, in the suite's own phrases, without any step the suite's other specs have. `--results` leaves out a rule whose scenario failed in that run. A spec that is no draft any more is not written over | |
| `bruinet scrape scraper rights [<group>…]` | What each account that walked the app saw and was refused, a row per page: the rights matrix a spec is written from. An account gets a column by walking as it (`places walk --as <account> --links-and-tabs`) | |
| `bruinet scrape scraper review` | Writes `review.md` in the app's Scraper project: the last reason per place that still owes a class, by reason and by the shape of its evidence; what was never tried; what waits for a person. `scraper run` writes it after every batch | |
| `bruinet scrape scraper note "<text>" [--loop N]` | Adds a line to the ledger: what a person or an agent changed or decided | |
| `bruinet scrape scraper keep [--json] [--baseline <filter>] [--mark]` | Judges the change in the working tree before it is kept: every changed file against what the setup's `Improve` answer allows (the app's data, also the suite's code, also Bruinet's), and what replay gives with the change. Changes nothing | 1 when a file is outside the scope, or a field lost its control |
| `bruinet scrape config get\|set\|add\|remove` | Reads or changes `scraper.config.md`: `set Explore MaxPages 20`, `add "Explore hosts" shop.example` | |
| `bruinet scrape install-browser` | Installs Chromium for the Scraper | |
| `bruinet data validate [file] [--fix]` | Checks every test set and scenario, or one file; what `--fix` rewrote goes to stderr | [Test Data](../guides/test-data.md) |
| `bruinet data phrases [file] [--keyword When]` | The phrases steps can use, with argument types | |
| `bruinet data references <namespace>` | The config and files that use a namespace | |
| `bruinet data snippet <file>:<line>` | A method for a step that binds to nothing | |
| `bruinet data describe <Type>` | The fields of a test-data type | |
| `bruinet data list` | The test sets and what uses them | |
| `bruinet data schema [--check]` | Writes (or checks) `testdata.schema.json` | |
| `bruinet scenario preview <file> [--out page.html]` | A feature file's preview as a page (`--suite`, `--host`) | [Visual Studio Preview](../guides/visual-studio-preview.md) |
| `bruinet scenario why <file>:<line> [--suite <name>]` | Where a scenario's newest failure first parted from its last green run: the step, the kind of change, and the files changed since that touch it; reads only (the MCP tool `diff_since_green`), and with no failure or no green run to compare it says so and exits 0 | [Visual Studio Preview](../guides/visual-studio-preview.md) |
| `bruinet scenario results delete [<file>[:<line>] \| --suite <name>]` | Deletes old results of a scenario, a file or a suite from every run; asks first | |
| `bruinet friction add --stage <stage> --what <text> --cost <text> --change <text> --where <text> [--workaround <text>] [--log <file>]` | Adds a row to the suite's friction log (`.testing/400-FrictionLog.md`, found from this folder up), numbered after its last row and dated today; prints the number | [friction-log](../../.github/skills/friction-log/SKILL.md) |
| `bruinet suite pages [--suite <folder>] [--map <file>] [--places <file>] [--write] [--check] [--json]` | A suite's page checklist, read from its source: a row per page object with its kind (list, form, view), its named fields, buttons, tabs and typed rows, the specs and tests that visit it, and its gaps; under it the pages of the list of places (`.testing/175-Places.json`, or the map when the suite has no list) without a page object. `--write` writes `.testing/180-Pages.md` and keeps each row's Owed column; `--check` exits 1 when that document is not what the source says now | |
| `bruinet spec check <spec or folder>...` | Checks every spec (`*.spec.md`, a folder searched for them) against the format ([the spec format](../spec/spec-format.md)): header, sections, rules with ids, an example per rule, plain words. Exit 1 when a spec has a finding | [runnable specs](../guides/runnable-specs.md) |
| `bruinet spec trace <spec> <tests folder>... [--runnable] [--results <file.trx or folder>...]` | Finds the tests that reach each rule of a spec, by the rule's id in a trait or a tag; `--runnable` counts the spec's own examples, run as scenarios; `--results` gives each rule a state from the runs (passing, failing, not run). Exit 1 on a rule no test reaches, or one not passing | [runnable specs](../guides/runnable-specs.md) |
| `bruinet tests timings <folder> [--top 10]` | From a run's timings (`BRUINET_TIMINGS`), writes `timings.md` beside them: the host's start, time by kind, per test, and the slowest spans | [test selection](../guides/test-selection.md) |
| `bruinet tests touch-map <timings>… [--project <folder>]` | From a full run's timings, writes the project's `touch-map.json`: which test and scenario used which page objects and controls | [test selection](../guides/test-selection.md) |
| `bruinet tests affected [--project <folder>] [--base <rev> \| --files <paths>]` | The suite's tests a change reaches, each with why, as one `--filter` (`--app`, `--map`, `--always`); the suite's libraries, test base and the app's shared code run it whole; `tools/Scripts/select-tests.ps1` runs it for the Html and Blazor suites | [test selection](../guides/test-selection.md) |
| `bruinet sql snapshot --connection-from <variable>` | Writes the database's schema snapshot (`--out`, `--data`, `--references-file`, `--default-schema`, `--provider`) | [SQL Replay](../guides/sql-replay.md) |
| `bruinet sql stale --schema <file> --recordings <folder>` | The recordings a schema change made stale, from their stamps (`--stamp name=value`); exit 1 when any | |
| `bruinet sql diff --recordings <folder> [--base <rev>]` | What each re-recorded test changed against git: commands, rows, columns, values, errors, the stamp | |
| `bruinet sql accept --recordings <folder>` | Marks every recording read as it is now (`reviewed.json`) | |
| `bruinet sql check --recordings <folder>` | The gate: recordings that went in unread; exit 1 when any | |
| `bruinet sql capture start --connection-from <variable>` | Starts an Extended Events session on the test database (`--server-folder`, `--database-like`, `--state`) | |
| `bruinet sql capture stop --connection-from <variable>` | Stops and drops the session; its files stay | |
| `bruinet sql capture read --connection-from <variable> [--out sql-log]` | Reads the session's files into the SQL log | |
| `bruinet sql report --log <folder>` | The database's share of each test, how far each object reaches, and repeated commands (`--timings`, `--schema`, `--out`) | |
| `bruinet http stale --recordings <folder>` | The HTTP recordings whose newest replay missed a call or left recorded calls unused; exit 1 when any | [HTTP Replay](../guides/http-replay.md) |
| `bruinet http diff --recordings <folder> [--base <rev>]` | What each re-recorded test's calls changed against git: calls added and gone, statuses and answers | |
| `bruinet http accept --recordings <folder>` | Marks every HTTP recording read as it is now (`reviewed.json`) | |
| `bruinet http check --recordings <folder>` | The gate: HTTP recordings that went in unread; exit 1 when any | |
| `bruinet voice list [--project <folder>]` | The lines of the project's video scripts with no clip in its speech folder, and their characters; exit 1 when any | [Narrated Recordings](../guides/narrated-recordings.md) |
| `bruinet voice fill [--project <folder>]` | Adds what the local speech cache holds to the speech folder; costs nothing | |
| `bruinet voice buy [--project <folder>] [--yes]` | As `fill`, and buys the lines found nowhere from the voice's service; asks first | |
| `bruinet voice prune [--project <folder>] [--yes]` | Deletes the clips no script says any more; asks first, and refused when a script has errors | |
| `bruinet voice index [--project <folder>]` | Writes `speech-clips.md` beside the scripts: per video, each line, where it is said, and its clip | |
| `bruinet controls generate <paths>…` | Each `.tpl.cs` in the files and folders given to its `.gen.cs` (`--members all\|actions\|state`) | [generator contract](../../.github/skills/maui-control/references/generator-contract.md) |

`bruinet --version` prints the version.

`--json` prints one JSON document: on `scrape` commands, `data validate`, `phrases`, `references`, `describe` and `list`,
`scenario why` and `results delete`, `tests affected`, `sql snapshot`, `stale` and `check`, and `http stale` and `check`.
With nothing to report it prints `[]` or an object, never nothing.

For tools, not people:

| Command | Started by |
| --- | --- |
| `bruinet lsp` | The Visual Studio and VS Code extensions: the language server, over stdio |
| `bruinet mcp [--solution <folder>]` | Copilot, Claude and other agents: the MCP server, over stdio (`.mcp.json`, `.vscode/mcp.json`) |
| `bruinet mcp-host --build <folder> [--solution <folder>] [--runs <folder>]` | The MCP server while Bruinet is developed: runs `bruinet mcp` from a snapshot of the build folder (`--runs`, default its sibling `run`) and starts it again when a new `mcp.stamp` appears there, the session kept and told its tools changed; nothing holds the build folder |

## Options Every Command Spells The Same

| Option | Meaning | Default |
| --- | --- | --- |
| `--solution <folder>` | Where projects are found, `mcp` and `mcp-host` included | The nearest folder up with a `.sln` or `.slnx`, else the current one |
| `-p`, `--project <name>` | The project: its name or its folder relative to the solution; in `tests` and `voice`, its folder | The project the current folder is in, else the only one |
| `--suite <name>` | A suite's name, for a feature file several suites link | The file's own suite |
| `--json` | The result as one JSON document on stdout | Text |
| `-y`, `--yes` | Yes to the question before it starts | Ask; with no one at a terminal, do not start |
| `--headless` | No browser window | A window when someone is at a terminal |

`data` commands find their `bdd.config.md` (or `bdd.config.md`) in the current folder, or take
`--config` and `--assembly`.

A scenario's place is `<file>:<line>`, the line of its heading. An option that takes several values is repeated:
`--data dbo.Kinds --data dbo.Places`.

Results go to stdout; progress, counts, questions and problems to stderr.

## Exit Codes

| Code | Means |
| --- | --- |
| 0 | Ran, and found nothing |
| 1 | Ran, and found something: data problems, stale or unread recordings, lines with no clip, members verify did not find, a guard that stopped explore, a draft put back |
| 2 | Could not run: a wrong call (it says where `--help` is), a missing file, a database that does not answer, no browser, or not confirmed |
| 130 | Stopped by Ctrl+C |

A gate in CI reads 1 as "found" and 2 as "broken". Anything a command throws is one line, `bruinet: <message>`, on
stderr; `BRUINET_DEBUG=1` adds the stack trace.

## In A Script

From a scraped project's folder (working directory: the UI test project):

```powershell
bruinet scrape explore --headless --yes --json > explore.json
if ($LASTEXITCODE -eq 2) { throw 'explore could not run' }
bruinet scrape draft
bruinet scrape verify --headless
```

`tools/Scripts/scrape-loop.ps1` runs explore, draft, an agent and verify this way, unattended; with `-Factory` it
runs the factory on the test site from nothing (seed, walk, modules, loops, the suite, replay) and says what each
planted fault ended as.
