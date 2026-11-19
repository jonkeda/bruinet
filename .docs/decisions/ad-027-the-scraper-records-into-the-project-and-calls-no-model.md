# AD-027: The Scraper Records Into The Project And Calls No Model

The Scraper turns an app's pages into page objects and controls. It is a service with no UI, no model of its own
and no step a person does in a browser:

| Decision | What holds |
| --- | --- |
| **One engine behind the command line and MCP** | `Bruinet.Scraper` captures, detects patterns, suggests controls and locators, diffs, writes and builds. `bruinet scrape …` serves it to a person, a loop and CI; the MCP server's `scrape_*` tools serve it to agents. **Amended (2026-11-19):** the Scraper view in the IDEs and its `bruinet/scraper/*` requests are removed: nobody used the view, and what it showed is a file a person reads. |
| **Rules draft; the agent improves; the Scraper checks** | The Scraper calls no model. **Draft** writes a page object per recorded page from its suggestions by rules: the page as it opens, stable locators only, into `<Name>.scraped.cs`, a `partial` whose other part is the person's and never touched; a draft that does not build is put back. Controls are not drafted: what is one part is a judgement, the agent's. An agent reaches the Scraper through MCP tools; everything the tools do is deterministic and testable without tokens. Generated code counts once `scrape_build`, the project's own build with its analyzers, passes, and **Verify** finds every member's locator once in the running app. |
| **Explore clicks for the person, inside guards the project sets** | Explore is a recording session that moves by itself, breadth first within a budget. Four guards, each a setting in `scraper.config.md` with the careful value as default: which clicks (`ClickPolicy`, danger-word categories, never and always click, never visit), which requests (`BlockMethods`, `AllowRequests`), what the request guard cannot see (`WebsocketApps`), and where (`ExploreHosts`, `localhost` and `127.0.0.1` by default). A sign-in page stops it until the person signs in; it never types. |
| **An agent from its own command line** | Unattended, the agent is its own CLI (Copilot CLI, Claude Code) given only the Bruinet MCP server's tools, as `tools/Scripts/scrape-loop.ps1` runs it. Bruinet's command line (`scrape explore`, `draft`, `verify`) stays model-free. |
| **Recordings are files in the UI test project** | A project is scraped when it has a `scraper.config.md`. Its recordings are `Recordings/<session>/` in the project: `recording.json`, an ordered list of page captures, and one `*.nodes.jsonl` per captured page (one node per line, fields in a fixed order), committed with the code. The index is built in memory; the browser login is under the user's profile, never in the repository. **Amended (2026-11-19):** a session is made by Explore or the walk only; recording by hand, with its toolbar and picker, is removed. |
| **The code is the registry** | A generated file starts with `// <scraped page=… from=… hash=… />` or `control=…`. What exists, what it was made from, and whether the page changed since are read from the source, not from a store. |
| **Nothing typed, nothing unmasked** | A capture never reads what a user typed (`value`). The config's masks are applied before a snapshot is written or shown. **Amended (2026-11-19):** the amendment for recording tests (R7) is withdrawn with the feature: a recording holds no action and no typed value at all. |
| **A platform is a source** | `IScrapeSource` and the snapshot model (`ScrapeSnapshot`, `ScrapeNode`) are in the Scraper (`Bruinet.Scraper.Snapshots`), with no platform type. Html (Playwright, with Blazor's controls preferred for a Blazor project) is the first source; it is in the engine until a second one needs a project of its own (AD-026, R3). |

**Why:** the WPF Scraper (a window with its own browser, code preview, log viewer and approval UI,
calling the Copilot SDK itself, with a per-user SQLite corpus) duplicated what an IDE and its
agent already do, and nothing it generated could be reproduced by a teammate. Evidence (at
c4f5a864): [the plan, as built](https://github.com/exact-mobile-apps/test-tooling/blob/c4f5a864/.my/ScraperEx/plan.md),
[the capture spike](https://github.com/exact-mobile-apps/test-tooling/blob/c4f5a864/.my/ScraperEx/spike0/Program.cs).
Exploring and drafting by rules took the person's browsing and the agent's first pass out of the
loop, so it runs unattended after every change; a click that changes data is harmless on one tenant
and not on another, so the guards are the project's settings, not Bruinet's. Evidence (at
22685915): [the auto plan, as built](https://github.com/exact-mobile-apps/test-tooling/blob/22685915/.my/ScraperEx/auto-plan.md).

**Ruled out:**
- a Scraper window or app of its own, and any Scraper view in an IDE;
- a command or tool that waits for a person to browse, click or pick in the Scraper's browser (a sign-in is the
  one thing a person does there);
- drafting a test from what a person did in a browser;
- a model call, a prompt sent, or a model SDK in Bruinet's Scraper;
- a corpus database, or recordings outside the project;
- a registry of generated code beside the code;
- recording input values in a capture; writing a snapshot before its masks are applied;
- `scrape_write_file` outside the project, or into `Recordings/`, `bin/` or `obj/`.
- Explore typing a credential or into a field; looser guards than the careful ones by default;
  a host Explore runs against without being named (other than `localhost` and `127.0.0.1`);
- drafting over a page object a person made, or into a person's part of one; leaving a draft that
  does not build.

**Broken when:**
- a Scraper feature needs an IDE, a person in a browser, or a model to be tested;
- `Bruinet.Scraper` references a model SDK, a database or a UI framework;
- a teammate cannot see from the repository what a page object was made from;
- a `*.nodes.jsonl` holds a typed value or an unmasked match of a mask;
- a `recording.json` holds anything but page captures.
- Explore with a project's default config moves any of the test site's counters
  (`scrape-loop.ps1 -Target test-site`);
- drafting twice changes a drafted file, or a draft survives that does not build or verify.
