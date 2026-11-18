# The Scraper

The Scraper records an app's pages into a UI test project and helps an agent (Copilot, Claude) make
page objects and controls from them. Html and Blazor apps today. The decisions are in
[AD-027](../../.docs/decisions/ad-027-the-scraper-records-into-the-project-and-calls-no-model.md).

## The view: eight steps

Open it in **VS Code** with **Bruinet BDD: Open Scraper** (also on a `scraper.config.md`'s title bar),
in **Visual Studio** with View, Other Windows, **Scraper**. It is a workflow: the steps on the left,
each with its state (✓ done, its number to do, ! to look at) and a count, the step in the panel on
the right with one main button, and in the header **Next:** what to do now, with **Go**.

| Step | What it is for | Main button |
| --- | --- | --- |
| 1 Project | The UI test project the page objects go into | **New project…** (also **+** by the picker) |
| 2 Connect | Can the Scraper reach the app, signed in, and may Explore go there | **Check the app**; then **Allow this host** or **Open the browser to sign in** when it says so |
| 3 Collect pages | Explore's guards and budget as a form (**Save** writes the fields you changed, **Undo changes** puts them back), and the last run | **Explore** (a dry run the first time); **Record by hand** under More |
| 4 Review | The pages and their captures (an outline per capture, highlight, Compare, Changes), what Explore skipped by reason and the requests it blocked | none: **Next** |
| 5 Controls | **To make**: the patterns that repeat, each with a tick box and a control name; **Made**: the controls with a scraped header, checked against the recordings (each part found in how many of the pattern's occurrences) | **Make *n* controls with the agent**; per control **Review** (its code, its parts, a note, **Fix with the agent**, **Accept**). **Skip** if the page objects do without |
| 6 Page objects | A row per page: a tick box, its class name (edit it to rename the class, its draft and your part), and whether it is drafted or changed since | **Draft *n* page objects** (the ticked ones), **Improve with the agent** |
| 7 Verify | Each page object's members looked up in the running app | **Verify** |
| 8 Tests | Each test recording: its steps as the scenario says them, its gaps with why, its drafts (and whether the recording changed since) | **Record a test** (the browser opens with **Assert**, **Stop** and a count); per test **Draft** (as `## Tests` says, or xUnit, scenario, both) and **Run once** (built, then `dotnet test` on the draft) |

**Code**, below the steps, lists the project's page objects and controls. While a recording or
Explore runs, a strip under the header shows its progress with **Capture**, **Pick**, **Assert** (a test recording), **Stop**,
**Continue** (after a sign-in) and **This page…**. What the workflow remembers that the files do not
(the last check and verify, accepted controls, skipped controls) is in the project's
`.scraper-state.json`, which is not committed. An agent sees the same steps with
`scrape_next`.

## Make a project scrapeable

A new project: **+** beside the Scraper's project picker. The form asks for the name, folder,
platform, start URL and test ids, and whether Bruinet is referenced as projects (in this repository)
or as packages. It writes the `.csproj`, its `scraper.config.md` and an empty `Pages/`, and adds the
project to the open solution (in a solution folder, `Scraped` unless you name another). Each problem
shows under its field before anything is written. **Add a config to an existing project instead**
writes only the config into a test project you have. An agent does the same with
`scrape_create_project`.

The picker lists the solution's projects that have a `scraper.config.md`, and those under the
solution's folder; `TestResults` (the loop's copies) and build output are left out. Two projects of
one name are shown with their folders.

By hand: add `scraper.config.md` to the UI test project the page objects go into (one project per
app, or several for a large app; a project that references another scraped project sees its
controls):

```markdown
# Scraper Config

## App

| Field | Value |
| --- | --- |
| Platform | blazor |
| StartUrl | http://localhost:5180/ |
| Environment | production |
| Namespace | MyApp.UITests.PageObjects |
| Pages | PageObjects |
| Controls | PageObjects/Controls |
| Screenshots | false |
| Privacy | false |

## Modules

| Path | Pages | Namespace |
| --- | --- | --- |
| /invoice | Modules/Invoices/Pages | MyApp.UITests.Modules.Invoices.Pages |

## Aliases

| Url |
| --- |
| https://myapp.example.be/ |

## Test ids

| Attribute |
| --- |
| data-testid |
| data-automation-id |

## Masks

| Pattern | Replacement |
| --- | --- |
| `NL\d{2}[A-Z]{4}\d{10}` | `IBAN` |

## Secrets

| Field | Placeholder |
| --- | --- |
| API key | |
| pin-code | pin |
```

- `Platform`: `html` or `blazor` (a Blazor project gets the `Bruinet.Blazor` controls suggested).
- `Modules` (optional, columns `Path`, `Pages`, `Namespace`): a page whose key starts with `Path` is drafted
  into that module's folder and namespace; the first row that matches wins, and a page no row claims goes to
  `Pages` in `Namespace`.
- `Aliases`: other hosts of the same app; a page elsewhere (a sign-in provider) is not recorded.
- `Control libraries` (optional, columns `Class prefix`, `Library`): a pattern whose classes start with the prefix
  (`v-` for Vuetify) belongs in that library; its control brief says so. The brief still writes into the project, in
  the category folder of the control's kind (`Controls/Text`, `Controls/Selection`, `Controls/Buttons`, …, as
  `Bruinet.Html` keeps them), and the control moves to the library once it builds. A draft leaves out what a
  library's classes cover (an element with such a class, or within three levels below one, not inside a web
  component's shadow root), as it does what the project's own parts cover.
- `Test ids`: the attributes that are test ids, preferred first. The default list is `data-testid`,
  `data-test`, `data-test-id`, `data-qa` and `data-cy`.
- Web components: a capture keeps open shadow roots, and a suggested locator crosses them as Playwright's CSS
  does (a shadow tree's top elements are its host's children), so a control inside a web component is found.
- `Masks`: text replaced before anything is written. A page capture never records what a user typed;
  only a test recording's actions keep typed values, masked.
- `Privacy` in `## App` (default `false`): the Scraper runs on dev, test and acceptance environments, whose data is
  test data, so by default only the masks listed apply. With `true`, an e-mail address is masked too (`{e-mail}`),
  and an element whose text is masked is not drafted. Secrets (below) and sign-in tokens in reports are kept out
  either way.
- `Secrets` (optional, columns `Field`, `Placeholder`): fields whose value a test recording never
  keeps, matched by test id, `name`, `id` or label text. The value stays in the browser; the action
  records `Placeholder` (default: the field in camel case, `apiKey`) for you to map to a setting or test
  data. A password field is always a secret, with the placeholder `password`.
- `Tests` (fields `Target`, `Base`): what a test recording is drafted as, `xunit`, `scenario` or
  `both`, and the test base an xUnit draft derives from (it gives `Context` and `NavigateToPage(path)`).
  A new config gets `Target`: `scenario` when the project has a `bdd.config.md`, otherwise `xunit`.
  An xUnit draft goes to `Tests/Recorded/<Name>Tests.recorded.cs`, beside your part `<Name>Tests.cs`,
  which is written once and never changed; a scenario to `Features/Recorded/<name>.feature.md`. Each
  starts with what it was recorded from (`// <recorded session=… hash=… />`, or a `Recorded` metadata
  row), so a draft is drafted again over itself and never over a file you made.
- `Components` (optional, columns `Match`, `Kind`, `Name`): elements the Scraper cannot tell by itself, named by you. `Match` is `selector: …` or `text: …`; `Kind` is `collection`, `component` or what a test does with it (`text`, `selector`, `toggle`, `range`, `date`, `clickable`, `readable`). They win over its own reading: step 5 offers them by that name, and their brief says so.

`tests/Bruinet.Blazor.UITests` is an example: its config, its `Recordings/`, and
`PageObjects/UserFormPage.cs`, made from them.

## Record

Step 3, More, **Record by hand** (or an agent's `scrape_start`) opens a Chromium window. Work in it: each page the app moves to is captured once it has
settled (a hash route and `pushState` count as moves). **Capture** takes the page again (a dialog
open, a tab chosen); **Pick** lets you click an element to find it in the capture; **Stop** closes
the window. The window keeps its own login per project, under your local application data.

Recordings go to `Recordings/<date>-<session>/` in the project: `recording.json` and one
`*.nodes.jsonl` per page. Commit them: they are what the page objects were made from. The browser
is Playwright's Chromium (`pwsh <an Html test project's output>\playwright.ps1 install chromium`).

## Explore and draft

**Explore** visits the app's pages and states by itself and captures each new one as if you had
gone there: links to the app's own hosts, then tabs, menus, expanders, sorting, paging and dialogs
that say they are one. It works breadth first and stops at the first limit of its budget. Its
recording is `Recordings/<date>-explore/`, and everything else (patterns, suggestions, changes)
works on it as on yours. **Draft page objects** then writes a page object per recorded page, with
no model.

- **The view:** step 3 shows the guards and budget as a form and the summary of the run they make,
  with **Explore** (a dry run the first time: links only, it lists what it would click), **Stop**
  and **Continue** in the running strip. Step 4 lists what was captured, skipped and blocked, with
  **Never click**, **Always click** or **Allow this request**. Steps 6 and 7 draft and verify.
- **The command line** ([`bruinet`](../run/cli.md), from the solution folder or a project's own
  folder, which picks the project):

  ```powershell
  bruinet scrape status
  bruinet scrape explore --project MyApp.UITests [--dry-run] [--max-pages N] [--max-depth N] [--max-minutes N] [--headless] [--yes] [--json]
  bruinet scrape draft --project MyApp.UITests [--page /orders/{id}]
  bruinet scrape verify --project MyApp.UITests [--headless]
  bruinet scrape find "First select a project" --project MyApp.UITests [--run 2026-11-14-explore]
  ```

  `explore` shows the guards and asks "Go on?" unless `--yes`; with no one at a terminal it does
  not start without `--yes`. It exits with 0 when it ran within its guards, 1 when a guard stopped
  it (a sign-in, a refused host), 2 for an error.
- **An agent:** `scrape_explore` (links only and headless unless asked otherwise), `scrape_draft_page_objects`,
  `scrape_check_page_objects` and `scrape_update_config`. Each asks you first.

**Several starts, and a page of one record.** `--url` may be given several times: a run for each, one after
another, each kept within its own start's path unless `--within` says otherwise, so a module is explored route by
route with one command. An address with a record's id in it is one page (`/contact#/view/{id}`): explore it from one
record's address, and its draft knows it is open on any record (it cannot open itself: a test goes there from the page
that leads to it). Each exploration also writes `facts.md` beside its captures: what every captured state shows.

**A look at one page.** A fact about a page (which fields a form marks as required, what an empty submit answers,
what a list offers) does not need a recording or a probe test:

```powershell
bruinet scrape look --url /contact#/create
bruinet scrape look --url /contact#/create --click "Create"
bruinet scrape look --url /project/create --click "field:Status"
```

It opens the address with the project's profile and saved sign-in, clicks what `--click` names, in order, and prints
the page's facts: its headings, buttons, tabs, fields (named by the label beside them where they have no name of
their own), what is marked as required, messages, a table's columns and rows (how many, and the first three by their cells), an open list's options, and its links (what each says and where it goes: an open menu
is the app's map). When a dialog is open
the facts are the dialog's. A click is a text as the page says it, `field:` and a field's label, or `css:` and a
selector. The project's guards hold: its hosts, its request guard, and for every click what Explore itself may not
click (`Never click`, the danger words). Nothing is typed, so a form is never filled; what a filled form does is a
test's to find out.

**The calls a page makes.** `--calls` adds a line per call the page's scripts made (fetch and XHR) after the last
click, or on opening when nothing is clicked: `Call: GET https://api…/organization/department/904 -> 200`, and for a
call that writes what it sent. It is how a suite learns where a record lives, so a test can delete beside the screen
what it made on it, without guessing addresses. No header is shown, so no token.

**As another account, and a sign-in that ran out.** `--as Reader` looks with that account's saved sign-in: the file
named so beside the project's own `SignInState`, in a browser profile of its own, so one account's session never
answers for another's. When a look meets the sign-in page and `## Explore` has a `RenewSignIn` row, the Scraper runs
that command in the project's folder (`{account}` in it is the account looked as), then looks once more. The command
is the UI suite's own: a test that signs in and saves the sign-in
(`dotnet test --no-build --filter Category=SignIn -e SUITE_SIGN_IN_AS={account}`). The Scraper types no credential
itself. Without the row, the look stops and says the sign-in ran out.

**Signing in is yours.** Explore uses the project's browser profile. When it reaches a sign-in page
(the config's `LoginUrl`, a page with a password field it was sent to, or another host), it stops
and asks you to sign in in the window. It never types. A project whose UI suite already signs in can
start from that sign-in instead: `SignInState` under `## Explore` names the file the suite saves
(`SaveStorageState`, such as `../.auth/Reader.json`, git-ignored), and its cookies and storage are loaded
into the profile before the first page. Still nothing is typed.

**The guards** are settings in `scraper.config.md`. The defaults are the careful ones; loosen what
you know is harmless for your app and tenant:

```markdown
## Explore

| Field | Value |
| --- | --- |
| MaxPages | 50 |
| MaxDepth | 4 |
| MaxMinutes | 10 |
| DelayMs | 500 |
| LoginUrl | /login |
| Within | |
| ClickPolicy | safe |
| BlockMethods | POST, PUT, PATCH, DELETE |
| WebsocketApps | links |

## Danger words

| Category | On |
| --- | --- |
| delete | false |

## Explore hosts

| Host |
| --- |
| localhost |
| test.myapp.example |

## Never click

| Match |
| --- |
| text: Empty cache |
| selector: [data-testid=danger-zone] button |

## Always click

| Match |
| --- |
| selector: [data-testid=filter-toggle] |

## Never visit

| Url |
| --- |
| /admin |

## Allow requests

| Method | Url |
| --- | --- |
| POST | /graphql |
```

| Setting | What it decides |
| --- | --- |
| `ClickPolicy` | `links`; `safe` (the default: links, tabs, menus, expanders, sorting, paging, dialogs); `not-unsafe` (also buttons it cannot place); `all` (submits too: a throwaway environment only, asked twice, and only on a host named in `Explore hosts`) |
| `Never click`, `Always click` | elements by `text:` or `selector:`; a selector is Playwright's CSS, so it reaches into a web component's open shadow root. An element in `Always click` is clicked even when nothing in it says it can be (a custom element with a click handler); `Never click` wins over it. A table is sorted once per page, not once per column |
| `Danger words` | categories of words that make an element unsafe, all on by default, in English, Dutch, German and French: `sign-out`, `delete`, `update`, `send`, `pay`, `approve`, `bulk`. Turn one off by name |
| `BlockMethods` | request methods aborted while exploring; `GET`, `HEAD` and `OPTIONS` always go through. `Allow requests` lets single endpoints through |
| `WebsocketApps` | a page that opens a websocket (Blazor Server, SignalR) clicks without a request the guard can see: `links` (the default) explores its links only, `warn` explores it anyway, `ignore` does not say so. A report shows a websocket's scheme and host only, and a blocked request's query with its values masked, since apps put sign-in tokens there |
| `Within` | a path, or `/#/route`, the links Explore visits stay under: `/components/tabs` keeps it on that page and its states. Empty: the whole app. A value that is not a path, a route or an address is refused (Git Bash turns `/contact` into `C:/Program Files/Git/contact`: set `MSYS_NO_PATHCONV=1`). `scrape explore --within /contact` sets it for one run; the summary says when the start page is outside it |
| `Explore hosts` | where Explore may run: `localhost` and `127.0.0.1`, and every other host by name |

After every click it checks it is still on the app's host, still signed in, with no dialog it did
not open and no download; otherwise it stops and says after which click. The report,
`explore.md` in the session, lists each capture and the click that led to it, each element skipped
and why, and each request blocked.

**A draft** is `Pages/<Name>.scraped.cs`, a `partial` class made from the page as it opens: a member
per element with a stable locator, named from its test id or text. The class is named from the path and
the hash route (`/contact#/create` is `ContactCreatePage`), and passes them as its URL, so the page opens
itself and is loaded only on that route. An id of a word and a counter (`input-47`) is generated, not
stable. What a part of the project already is (a class with a `Header.Control` signature that matches),
what a control library's classes cover, a table's headers, and a name your part already has are left out
and named in a comment, as is what has no stable locator and what only a click shows. A part is any class
with a `Header.Control` signature, on a Bruinet base or on another control (`TableControl`, a library's). Put your own members in the other part,
`Pages/<Name>.cs`. Drafting again replaces only the `.scraped.cs`. A page with a page object you
made by hand is not drafted. A draft that does not build is put back and reported, and so is one
that drops a member your part uses. Controls are not drafted: Draft lists the parts that repeat,
and **Improve with the agent** makes them. **Verify** opens each page in the running app and finds
every member's locator: once, several times, or not at all.

## The places of an app

Explore records pages to draft page objects from. It does not answer "which pages does the app have": its budget is
per run, and nothing adds its runs up. `bruinet scrape places` keeps one list of an app's **places**, in the suite's
`.testing/175-Places.json`, with a document a person reads beside it (`175-Places.md`). A place is what a page object
would be written for (a page, a tab, a dialog, a menu, a part a press shows), or a button that only calls the back end
(an action). The list holds how a place is built (labels, buttons, columns, calls) and never what a record says.

| Call | Does |
| --- | --- |
| `bruinet scrape places` | Prints the list; `--write` writes the document, `--json` the list itself |
| `bruinet scrape places seed` | Adds what is known without opening the app: the addresses a map names in backticks (`--map`, default `.testing/170-Map.md`), the addresses of the suite's page objects, the pages the recordings captured |
| `bruinet scrape places routes` | Opens each page of the list and reads the scripts only it loads, as text: a single-page app's route table survives in what it serves (`path:"/view/:id",component:…`). Each route is a known page; one with a parameter has no address until a record's own is found |
| `bruinet scrape places walk` | Opens each page that is known and not yet opened (the frontier), reads it, and presses what a person can press, each once |
| `bruinet scrape places add --from-run <file>` | Adds what tests found when they saved forms through their page objects |

The list is saved after every page, so a walk that stops loses one page and the next walk goes on. A page that did not
open comes once more at the end of the run, and is refused after three runs.

**What the walk presses** follows `Environment` in `## App`:

| | `production` (the default) | `acceptance`, `local` |
| --- | --- | --- |
| Links | Never pressed: where they go is put on the frontier | The same |
| Tabs, menus, expanders, what opens a dialog | Pressed | Pressed |
| Any other button, a row's buttons | Not pressed | Pressed, on the first row of a table only |
| What a danger word that is on forbids (send, pay, sign out) | Not pressed | Not pressed |
| A form's save | Not pressed | Not pressed: a test saves it through its page object |

It does not sort, page or close, and presses at most `--max-presses` on a page (25) and `--max-depth` deep into what a
press opened (2: a dialog's own buttons). What is the same on every page is named in `## Walk once` (a selector per
row: the menu bar) and pressed on the first page only; a `nav`, `header` or `footer` is that by itself. `## Walk
presses` names what the walk presses although `Never click` names it, because the walk holds writes back where Explore
does not.

**A write is held back** on every record that is not the walk's own: a PUT, PATCH or DELETE; a POST whose address the
page did not ask while it opened (a GraphQL call by what it sends: a query goes, a mutation is held); a GET whose
address says, after its first part, that it deletes or sends. The press is made, what it would do is written down as
the place's calls (`DELETE /api/order/{id} (held)`), and nothing changes. `--own <address>` names a record a test made
(its name carries the run marker): on its pages writes go through. `--as <account>` walks as another account, every
write held back unless `--may-write`. `--links-and-tabs` presses nothing else.

**A form** is read and marked `form, not saved`. A test then saves it through its page object: empty first (what the
form says is required), then filled in with the run marker in every name, and writes a line of JSON per form (`place`,
`required`, `landed`, `record`, `note`). `places add --from-run` marks the form saved, makes where the save landed a
page, and prints the records' addresses for `walk --own`.

`bruinet suite pages` holds the suite's page objects against the list when the suite has one: every page of it
without a page object is listed.

While requests are guarded the browser keeps no cache, so each page would fetch the app's scripts again. The Scraper
keeps scripts, styles and fonts itself for the length of a run and answers them from there.

## Generate

The view's agent buttons (step 5's **Make *n* controls** and **Fix with the agent**, step 6's **Improve with the agent**, a node's **Make a control**) send a prompt
into Copilot Chat, in the agent and with the model chosen in the header (**Agent and model**). The
**Bruinet scraper** agent (`.github/agents/bruinet-scraper.agent.md`) has the Bruinet tools and the
rules; **Agent** and **Ask** get them from the `/bruinet-scrape-pages` prompt. A control is made from
its brief (`scrape_get_control_brief`), at most five per prompt. When the IDE cannot send it, the prompt goes to
the clipboard and the view says so. When the answer is complete, step 5 or 6 checks again by itself.
Visual Studio sends it into the open chat session with the chosen model, in the chat window's own
mode. Review the result in your usual diff.

Your choice is kept per project in `.scraper-state.json`; `## Agent` in the config gives the default:

```
## Agent

| Field | Value |
| --- | --- |
| Agent | bruinet-scraper |
| Model | gpt-4.1 |
```

Register the MCP server as in [Copilot And Other Agents](visual-studio-preview.md#copilot-and-other-agents).
Its Scraper tools:

| Tool | Gives or does |
| --- | --- |
| `scrape_list_projects` | the projects with a `scraper.config.md` |
| `scrape_next` | where a project is in the workflow: each step's state and what to do next |
| `scrape_start`, `scrape_capture_page`, `scrape_stop` | a recording (one at a time per server) |
| `scrape_list_pages` | pages by key (path and hash route, ids as `{id}`), each page's newest capture and how many it has, the page object made for each and whether the page changed since; `page` gives one page's captures |
| `scrape_get_page` | a capture as an outline with node numbers; a node's subtree for a large page |
| `scrape_find_similar`, `scrape_find_text` | where a kind of node, or a text, occurs |
| `scrape_detect_patterns` | repeated parts (controls to make) and forms, tables, lists, navigation, dialogs, frames |
| `scrape_get_control_brief` | everything known about a pattern to make a control from it: where to write it and what to read, its occurrences, an example outline, its parts with how often each is found, and hints (what the elements are, a base, members) |
| `scrape_list_page_objects`, `scrape_match_controls` | the project's page objects and controls, and which occur on a page |
| `scrape_suggest_controls` | per element: a name, the control that fits, and a locator (test id, accessible name or text, stable id, stable class; a position last, marked unstable) |
| `scrape_get_header` | the first line of a generated file: what it was made from |
| `scrape_write_file`, `scrape_build` | write a file into the project (syntax checked first), then build it |
| `scrape_diff_page` | what changed on a page since its page object was made |
| `scrape_explore` | Explore, links only unless `linksOnly: false`; asks first |
| `scrape_draft_page_objects`, `scrape_check_page_objects` | Draft and Verify, for all pages or one |
| `scrape_update_config` | one row or field of `scraper.config.md` changed; asks first |
| `scrape_create_project` | a new scraped project (its `.csproj`, config and `Pages/`), not yet in the solution; asks first |
| `scrape_rename_page_object` | a page object renamed in the files that declare it; asks first |
| `scrape_list_events`, `scrape_resolve_events`, `scrape_gap_brief` | a recorded test's events, each resolved to a page object's member, and a brief per gap |
| `scrape_draft_test`, `scrape_run_drafts` | a recorded test drafted as an xUnit test or a scenario, and each draft run once |

Every list takes `max` (50 when left out) and says how many rows it left out as `more`. A tool that asks first acts
on a second call with `confirm: true`.

## When the app changes

Steps 3, 6 and 7 get a **!** and the header says what to do: explore or record the page again, then
update its page object. In step 4 a page whose page object was made from an older capture is marked;
**Changes** (or `scrape_diff_page`) lists what was added, removed and changed since, and **Update page
object** asks the agent to change only that.

## The loop

`tools/Scripts/scrape-loop.ps1` runs everything end to end, unattended, and writes one report. Run
it after changing the Scraper (from the Bruinet root):

```powershell
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Profile test-tenant -Repeat 3
pwsh tools/Scripts/scrape-loop.ps1 -Target blazor-sample
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Agent copilot
pwsh tools/Scripts/scrape-loop.ps1 -Target sites -Agent none
```

The targets: `test-site` and `blazor-sample` run locally. `plain`, `react`, `vue2` and `blazor` are
one public page each (the-internet, DemoQA, Element UI, MudBlazor), from the projects in
[Scraper.Sites](../../Scraper.Sites/README.md), and `sites` runs the four with one summary. A public
site that does not answer is reported as site down, not a failure. It has no counters, so its
Guards stage checks every clicked element's label against the danger words, outside Explore's
judge.

| Stage | Does | Fails when |
| --- | --- | --- |
| App | starts the target on a free port: the Scraper's test site (`tests/Bruinet.Scraper.TestSite`, with a page of dangerous controls that count each hit) or the Blazor sample | it does not answer |
| Explore | `scrape explore --headless --yes` with the profile's guards | a guard stopped it unexpectedly, or it found fewer pages than the target has |
| Guards | reads the test site's counters | `careful`: any counter moved. `test-tenant`: anything but delete and update moved, or those were not reached |
| Draft | `scrape draft` | a draft does not build |
| Agent | Copilot CLI or Claude Code with only the Bruinet MCP server's tools, to make controls and better names; Copilot CLI by default; skipped for `-Agent none` | `scrape_build` fails, or it ran past `-AgentMinutes` |
| Verify | `scrape verify` | a member is not found, or found more than once. A member of the project's own control (`new(this, "Elements")`) is not read: its control makes its locator |

Each run works on a new project under `TestResults/scrape-loop/<date>-<target>/`, never on the
repository, and writes `loop-report.md` and `loop-report.json` there even when a stage fails.
`-WebsocketApps` overrides the Blazor sample's `warn`. `-TurnOff <category>` turns a danger word off
while the guard check still expects the profile, to prove the loop catches a broken guard. The
agent stage costs the CLI's credits; `-Agent none` runs the loop without it.
