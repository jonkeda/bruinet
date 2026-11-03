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
| `bruinet scrape status` | The eight steps of a project and what to do next | |
| `bruinet scrape check [--visible]` | Opens the start URL: does the app answer, signed in, on a host Explore may go to | |
| `bruinet scrape explore` | Explores the app by itself under the project's guards (`--dry-run`, `--max-pages`, `--max-depth`, `--max-minutes`, `--delay-ms`, `--url`) | |
| `bruinet scrape record` | Records by hand in a browser window; `c` and Enter captures, Enter stops (`--by-hand`, `--session`, `--url`) | |
| `bruinet scrape draft [--page <key>]…` | Drafts page objects from the recorded pages | |
| `bruinet scrape verify` | Looks the page objects' members up in the running app | |
| `bruinet scrape resolve <session>` | Resolves a test recording's actions to members, verbs and catalog phrases, and lists its gaps; exit 1 with gaps | |
| `bruinet scrape draft-test <session>` | Drafts a test recording as an xUnit test, a scenario or both (`--target`, `--name`); exit 1 when gaps are marked | |
| `bruinet scrape config get\|set\|add\|remove` | Reads or changes `scraper.config.md`: `set Explore MaxPages 20`, `add "Explore hosts" shop.example` | |
| `bruinet scrape install-browser` | Installs Chromium for the Scraper | |
| `bruinet data validate [file] [--fix]` | Checks every test set and scenario, or one file | [Test Data](../guides/test-data.md) |
| `bruinet data phrases [file] [--keyword When]` | The phrases steps can use, with argument types | |
| `bruinet data references <namespace>` | The config and files that use a namespace | |
| `bruinet data snippet <file>:<line>` | A method for a step that binds to nothing | |
| `bruinet data describe <Type>` | The fields of a test-data type | |
| `bruinet data list` | The test sets and what uses them | |
| `bruinet data schema [--check]` | Writes (or checks) `testdata.schema.json` | |
| `bruinet scenario preview <file> [--out page.html]` | A feature file's preview as a page (`--suite`, `--host`) | [Visual Studio Preview](../guides/visual-studio-preview.md) |
| `bruinet scenario results delete [file] [--line N \| --suite <name>]` | Deletes old results of a scenario, a file or a suite from every run | |
| `bruinet tests touch-map <timings>… [--suite <folder>]` | From a full run's timings (`BRUINET_TIMINGS`), writes the suite's `touch-map.json`: which test and scenario used which page objects and controls | [test selection](../guides/test-selection.md) |
| `bruinet tests affected [--suite <folder>] [--base <rev> \| --files <paths>]` | The suite's tests a change reaches, each with why, as one `--filter` (`--app`, `--map`, `--always`, `--json`); the suite's libraries, test base and the app's shared code run it whole; `tools/Scripts/select-tests.ps1` runs it for the Html and Blazor suites | [test selection](../guides/test-selection.md) |
| `bruinet http stale --recordings <folder>` | The HTTP recordings whose newest replay missed a call or left recorded calls unused; exit 1 when any | [HTTP Replay](../guides/http-replay.md) |
| `bruinet http diff --recordings <folder> [--base <rev>] [--accept]` | What each re-recorded test's calls changed against git: calls added and gone, statuses and answers; `--accept` marks them read | |
| `bruinet http reviewed --recordings <folder>` | The gate: HTTP recordings that went in unread; exit 1 when any | |
| `bruinet why <file>:<line> [--suite <name>] [--json]` | Where a scenario's newest failure first parted from its last green run: the step, the kind of change, and the files changed since that touch it; exit 2 when there is no failure or green run to compare | [Visual Studio Preview](../guides/visual-studio-preview.md) |
| `bruinet controls generate --input <paths>` | Each `.tpl.cs` to its `.gen.cs` (`--members all\|actions\|state`) | [generator contract](../../.github/skills/maui-control/references/generator-contract.md) |
| `bruinet version` | The version | |

For tools, not people:

| Command | Started by |
| --- | --- |
| `bruinet lsp` | The Visual Studio and VS Code extensions: the language server, over stdio |
| `bruinet mcp [--solution <folder>]` | Copilot, Claude and other agents: the MCP server, over stdio (`.mcp.json`, `.vscode/mcp.json`) |
| `bruinet mcp-host --build <folder> [--solution <folder>]` | The MCP server while Bruinet is developed: runs `bruinet mcp` from a snapshot of the build folder and starts it again when a new `mcp.stamp` appears there, the session kept and told its tools changed; nothing holds the build folder |

## Options Every Command Spells The Same

| Option | Meaning | Default |
| --- | --- | --- |
| `--solution <folder>` | Where projects are found | The nearest folder up with a `.sln` or `.slnx`, else the current one |
| `-p`, `--project <name>` | The project: its name or its folder relative to the solution | The project the current folder is in, else the only one |
| `--json` | The result as one JSON document on stdout | Text |
| `-y`, `--yes` | Yes to the question before it starts | Ask; with no one at a terminal, do not start |
| `--headless` | No browser window | A window when someone is at a terminal |

`data` commands find their `uat.config.md` (or `bdd.config.md`) in the current folder, or take
`--config` and `--assembly`.

Results go to stdout; progress, questions and problems to stderr.

## Exit Codes

| Code | Means |
| --- | --- |
| 0 | Done, nothing wrong |
| 1 | Ran, and found problems: data problems, members verify did not find, a guard that stopped explore, a draft put back |
| 2 | Could not run: a wrong call (it says where `--help` is), no such project, no browser, or not confirmed |

## In A Script

From a scraped project's folder (working directory: the UI test project):

```powershell
bruinet scrape explore --headless --yes --json > explore.json
if ($LASTEXITCODE -eq 2) { throw 'explore could not run' }
bruinet scrape draft
bruinet scrape verify --headless
```

`tools/Scripts/scrape-loop.ps1` runs explore, draft, an agent and verify this way, unattended.
