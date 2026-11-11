# AD-028: One Command Line

Bruinet has one command, `bruinet` (`tools/Bruinet.Cli`, packed as the dotnet tool `Bruinet.Cli`).
Everything Bruinet runs from a command line is a command of it:

| Decision | What holds |
| --- | --- |
| **One executable** | `bruinet scrape …`, `bruinet data …` (test data and scenario checks), `bruinet scenario …` (preview, why a scenario fails, deleting results), `bruinet tests …` (timings, touch map, the tests a change reaches), `bruinet sql …` and `bruinet http …` (replay's recordings and their gates), `bruinet voice …` (a video's speech clips), `bruinet controls generate`, and the servers other programs start: `bruinet lsp` (the IDE extensions), `bruinet mcp` and `bruinet mcp-host` (agents). `docs/run/cli.md` has a row per command, and a test keeps it so. The language server, the data validator and the control generator are folders of the one project (`Lsp/`, `Data/`, `Generate/`), tested by one test project. |
| **Groups are nouns, commands verbs** | `bruinet <group> <command> [argument] [options]`. A group on its own, and `--help` anywhere, shows what comes next. |
| **The same words everywhere** | `--solution` (default: the nearest folder up with a `.sln`/`.slnx`), `--project` (default: the project the current folder is in), `--json` (one JSON document on stdout), `--yes`, `--headless`. Results go to stdout; progress, questions and problems to stderr. |
| **Exit codes mean the same** | 0 ran and found nothing; 1 ran and found something (data problems, stale or unread recordings, members verify did not find, a guard that stopped explore); 2 it could not run (a wrong call, a missing project, no browser, not confirmed); 130 stopped by Ctrl+C. |
| **No one to ask, no question** | With stdin redirected, a command that would ask does not start without `--yes`, and no browser window opens. |
| **Thin** | A command parses, calls the service the IDEs and MCP call (`ScraperService`, `BddLanguageServer`, `DataValidatorApp`, the generator), and prints. Parsing is System.CommandLine. |

**Why:** there were four entry points (`bruinet-data`, the language server with no arguments,
`--mcp`, `--preview` and `scrape`, and `Bruinet.Generator`), each parsing its own way, two of them
reached by a path into `bin/Debug`. One name is one thing to install and to find with `--help`.
Merging them adds no project (AD-026, R2: one process, one project). Evidence:
[the design](https://github.com/exact-mobile-apps/test-tooling/blob/0c519980/.my/cli/design.md).

**Ruled out:**
- a second tool beside `bruinet` (`bruinet-data`), or an alias for it;
- a `Bruinet.Cli` that starts the language server as another process;
- `bruinet` running UI tests (`dotnet test` with a filter does) or wrapping `dotnet new`;
- the Presenter as a command: it is an app; the scenario runner (`--pipe`, AD-025) stays a process a host starts.

**Broken when:**
- a Bruinet executable with a command line of its own is added beside `bruinet`;
- a guide tells a person to run a Bruinet command by a path into `bin` (scripts that build it first may);
- a command asks a question with no one at a terminal, or prints anything but its result on stdout under `--json`.
