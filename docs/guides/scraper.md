# The Scraper

The Scraper records an app's pages into a UI test project and helps an agent (Copilot, Claude) make
page objects and controls from them. Html and Blazor apps today. Its factory (`bruinet scrape scraper run`) goes
further by itself: a class and a test per place of the app, kept only when the test passed. The decisions are in
[AD-027](../../.docs/decisions/ad-027-the-scraper-records-into-the-project-and-calls-no-model.md) and
[AD-045](../../.docs/decisions/ad-045-the-factory-keeps-what-a-test-proved.md).

## How it is used

From the command line (`bruinet scrape …`, [the CLI page](../run/cli.md)) and by an agent through the MCP server's
`scrape_*` tools (below). There is no view in an IDE and no recording by hand: the Scraper moves through the app
by itself (Explore, and the walk of an app's places), under the guards the project sets. A person signs in once,
in the browser Explore opens, or a test saves the sign-in; the Scraper never types a credential.

## Make a project scrapeable

A new project: `bruinet scrape new <name> --url <start>` (an agent: `scrape_create_project`). It asks for nothing
else: the folder, platform, test ids and whether Bruinet is referenced as projects or packages are options. It
writes the `.csproj`, its `scraper.config.md` and an empty `Pages/`; add the project to the solution yourself.
`bruinet scrape list` shows the projects with a `scraper.config.md` under the solution's folder; `TestResults` (the
loop's copies) and build output are left out.

### The Scraper's own project for an app

What the Scraper knows and keeps about an app can live in a project of its own beside the suite: `<App>.Scraper`,
with the `scraper.config.md`, the draft templates, the recordings and the list of places. Its config names the
suite in `## App` (`| Suite | ../<App>.UITests |`): page objects, controls and tests are still read, written and
built there, and the project is still known by the suite's name. The suite does not reference it and runs without
it. A path in the config (`SignInState`, a control library's `Signs`) is relative to the config's folder.

The project is a small program that runs the Scraper for this app, with what only the app has added:

```csharp
using Bruinet.Scraper.Host;

return await ScraperApp.RunAsync(args, app =>
{
    app.AddRecord(new ContactRecord());   // an IRecordKind: how a record is made, found and deleted
    app.AddReader(new PlanBoardReader()); // an IPlaceReader: a widget no sign can describe
});
```

It references `Bruinet.Scraper.Host`. Its arguments are those of `bruinet scrape`, from the command on
(`dotnet run --project <App>.Scraper -- places walk --only /contact`); in the project's own folder
`bruinet scrape …` hands the command to it. From another folder `bruinet scrape` still runs, without what the app
added. The app's project does not generate control templates before a build: `bruinet scrape` does.

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
- `Masks`: text replaced before anything is written. A page capture never records what a user typed.
- `Privacy` in `## App` (default `false`): the Scraper runs on dev, test and acceptance environments, whose data is
  test data, so by default only the masks listed apply. With `true`, an e-mail address is masked too (`{e-mail}`),
  and an element whose text is masked is not drafted. Sign-in tokens in reports are kept out either way.
- `Place signs` (optional, columns `What`, `Sign`): how the parts of a page show themselves beyond the roles the
  platform gives. The Scraper itself knows only the roles (`role=tab`, `role=dialog`, `role=menu`, `role=toolbar`);
  what a UI library or the app calls a tab, an open menu, a dialog, a toolbar, the title of a part and an icon is
  a row. A control library's signs file has the same section for the library's own; the project's rows are added
  to them. `What` is one of `tab`, `open menu`, `dialog`, `toolbar`, `icon` (class names, several separated by
  commas), `title` (the end of a class name: `card__title`), `icon name` (the start of a class that names an
  icon: `fa-`), `icon style` (a regular expression for what follows that start and only says how the icon is
  drawn: `solid`, `\d+x`) and `closes` (an icon that closes what is open: `fa-xmark`). Without a row for it, a
  library's tab is a button like any other and a button that shows only an icon has no name.
- `Components` (optional, columns `Match`, `Kind`, `Name`): elements the Scraper cannot tell by itself, named by you. `Match` is `selector: …` or `text: …`; `Kind` is `collection`, `component` or what a test does with it (`text`, `selector`, `toggle`, `range`, `date`, `clickable`, `readable`). They win over its own reading: step 5 offers them by that name, and their brief says so.

`tests/Bruinet.Blazor.UITests` is an example: its config, its `Recordings/`, and
`PageObjects/UserFormPage.cs`, made from them.

## Recordings

What Explore and the walk capture goes to `Recordings/<date>-<session>/` in the project: `recording.json` and one
`*.nodes.jsonl` per page. Commit them: they are what the page objects were made from. The browser is Playwright's
Chromium (`bruinet scrape install-browser`), with its own login per project, under your local application data.

## Explore and draft

**Explore** visits the app's pages and states by itself and captures each new one as if you had
gone there: links to the app's own hosts, then tabs, menus, expanders, sorting, paging and dialogs
that say they are one. It works breadth first and stops at the first limit of its budget. Its
recording is `Recordings/<date>-explore/`, and everything else (patterns, suggestions, changes)
works on it. **Draft page objects** then writes a page object per recorded page, with
no model.

- **The command line** ([`bruinet`](../run/cli.md), from the solution folder or a project's own
  folder, which picks the project):

  ```powershell
  bruinet scrape check --project MyApp.UITests
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

**A draft** is `Pages/<Name>.cs`, the whole class, made from the page as it opens: a member
per element with a stable locator, named from its test id or text. The class is named from the path and
the hash route (`/contact#/create` is `ContactCreatePage`), and passes them as its URL, so the page opens
itself and is loaded only on that route. An id of a word and a counter (`input-47`) is generated, not
stable. What a part of the project already is (a class with a `Header.Control` signature that matches),
what a control library's classes cover and a table's headers are left out
and named in a comment, as is what has no stable locator and what only a click shows. A part is any class
with a `Header.Control` signature, on a Bruinet base or on another control (`TableControl`, a library's). The file's
first line (`// <scraped page=… />`) says it is the Scraper's: drafting again writes over the file, and what it
was before is in git. A page class of the draft's name without that line is yours and is not drafted. A draft that
does not build is put back and reported. An older draft, `Pages/<Name>.scraped.cs` beside your part
`Pages/<Name>.cs`, stays one part of a `partial` class when it is drafted again, and a name your part has is left
out of it. Controls are not drafted: Draft lists the parts that repeat,
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
| `bruinet scrape places show` | Prints the list; `--write` writes the document, `--json` the list itself |
| `bruinet scrape places seed` | Adds what is known without opening the app: the addresses a map names in backticks (`--map`, default `.testing/170-Map.md`), the addresses of the suite's page objects, the pages the recordings captured |
| `bruinet scrape places routes` | Opens each page of the list and reads the scripts only it loads, as text: a single-page app's route table survives in what it serves (`path:"/view/:id",component:…`). Each route is a known page; one with a parameter has no address until a record's own is found |
| `bruinet scrape places walk` | Opens each page that is known and not yet opened (the frontier), reads it, and presses what a person can press, each once. `--own <address>` opens a record the walk owns at its own address first |
| `bruinet scrape places add --from-run <file>` | Adds what tests found when they saved forms through their page objects |
| `bruinet scrape scraper status` | Says what is left, by group: the pages that open and have no class, the dialog forms no class was drafted from, the pages of one record (each needs a record first), the fields no made control stands for and the forms no test saved. A place also says how it is reached (a tab, an add button, a row's button, a row): the walk keeps that from the element it pressed, and a place found before it did reads as a plain button until its page is walked again |
| `bruinet scrape scraper replay` | The measure of a change to a sign, a holder, a template or the reader: every kept place is read again and every class drafted from one (every file that starts with the Scraper's line for a form or a row, whatever its class is built on) is drafted again, in memory, and what differs from the list and the suite is said. Seconds for hundreds of places, and no browser. A class that differs is stale (a template changed after it was drafted) or the change at hand did it: draft it again (`places draft <place> --name <Class> --write`) or take the change back. `--write` merges into a class that is there: a class drafted from a place gets the draft's members (one of the same name is replaced, a new one added), a class without the Scraper's first line is a person's and is only added to. What only the class has stays, and is named; a drafted class that has nothing of its own is written as the draft. `--replace` writes the draft over a drafted class as it is, so a member the place no longer gives goes. `--as form-test --write` writes the test over the one the Scraper drafted, and leaves a person's alone. A drafted test checks the class's fields, a page's tabs, and (where its template has `{Checks}`) every button, section and card opener the class declares. `bruinet scrape places not-shown <place> <label>…` records what such a test did not find on the app: a draft of the place leaves it out and says so, through any later reading, until `--clear` |
| `bruinet scrape scraper bench` | The examples: under `## Examples` in the config, a place and the class a person wrote for it (columns `Place`, `Class`, and `Kind` with `row` for a list's row). A label the example names that the capture shows and the reading does not give counts against the Scraper; a label the place shows and the example lacks does not |
| `bruinet scrape scraper setup` | A person's answers about this app, asked once (the `scraper-setup` skill): `Writes` (`read-only`, `own-records`, `free`), `Run`, `Improve` (`off`, `data`, `suite`, `bruinet`: what a change kept without a person may touch), `Ask`, `Commit`, `Specs`, `Walkers`. With `Writes` `read-only` a walk follows links and tabs only and holds every write back. A question without an answer takes the careful value |
| `bruinet scrape scraper modules` | The app cut into modules without walking it, from the list as `seed` and `routes` filled it and the config's `## Modules`. In the written `modules.md` a person may change `Order` and `Do` (`yes`, `later`, `no`); the cut made again keeps both |
| `bruinet scrape scraper run` | The loop. For each group that owes something: walk its pages again, read the captures, draft a class and a test for each place that has none, build the suite, run the new tests on the app. A file that does not build, and a test that fails twice with the class it holds, are put back, each with a reason that has a code (`does-not-build`, `no-page-class`, `needs-record`, `label-not-found`, `did-not-open`, `timeout`) and where its fix belongs. After every loop the ledger says where the minutes went, per station (walk, read, draft, build, test) and per kept file. A run of tests that takes longer than 15 minutes puts the loop back with `loop-too-long` and stops the batch. It never touches a file it did not write in that loop, with one exception: when a dialog's test names a member of its page that the page's class lacks, that member is added to the class from a draft of the page, and taken out again when the suite does not build with it. A batch stops by itself when one reason comes three times (a fault of the process, not of those groups), when two loops in a row keep nothing, or when the suite no longer builds. `--no-walk` drafts from the captures there are; `--dry-run` says which groups would run. A drafted test runs twice before its class is kept: one that fails and then passes is kept and said (`passed-second-time`), one that passes and then fails is put back (`passed-then-failed`) |
| `bruinet scrape scraper review` | `review.md`: why each place that still owes a class was not kept, the last reason per place, gathered by reason and by what the evidence has in common. Written after every batch too. The brief a fix starts from |
| `bruinet scrape scraper note` | A line in the ledger, for what a loop cannot know: what was changed between two batches, and why |
| `bruinet scrape scraper keep` | The check between an agent's change and keeping it: which changed files the setup's `Improve` answer does not allow, and what replay gives with the change (fields that gained or lost a control, classes that would be drafted differently). It also holds what the places owe against the mark the last batch left (pages without a class, dialog forms not drafted, record pages, fields with a made control): a count that got worse refuses the change, and so does a test of `--baseline <filter>` that fails. `--mark` writes the counts of now as the new mark when the change may be kept. It changes nothing else: a change it refuses is the agent's or a person's to take back. `scraper run --baseline <filter>` is the other guard: the tests of that filter run before a loop writes and again at its end, and a loop after which one of them no longer passes is put back whole (`broke-a-passing-test`) |

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
keeps scripts, styles and fonts itself: for the length of a run, and on disk between runs
(`%TEMP%/bruinet-kept-assets`, shared with the UI suites' browser) when the site gives an asset a version, which is
asked for first. `BRUINET_KEPT_ASSETS=off` keeps none on disk.

What a walk as one account opened, the walk as another account opens again: the list says per page who saw it and
who the app refused. A record's own page that opens by pressing its row is reached by pressing the first row of each
table once.

### The draft templates

How a place becomes a class and a test is the suite's own: `draft-templates.md` beside `scraper.config.md`. Each
`##` section is one template, a table or the first code block under the heading. The Scraper picks the template by
what the place is and how it opens; a template that is not there falls back to the plainer one above it.

| Section | For |
| --- | --- |
| `Field members` | A table: the member a field gets, by its control, its kind and what holds it |
| `Reserved names` | A table with a `Name` column: what the suite's own bases have already. A member of that name gets `Field` after it (`TitleField`), so the class builds |
| `Button member`, `Tab member`, `Opener member` | A member per button, per tab, and per card whose own button opens a dialog |
| `Section member` | A member per section of a form that folds, named by its header (the place sign `section` says what such a header looks like). Without it a section is listed as not drafted |
| `Tabs part`, `Tabs part tab`, `Tabs part member` | The tabs every page of one address shows (the pages of the list with the same address before `#` or `?`, when two or more have two or more tabs in common) are one class, drafted once (`places draft <page> --as tabs-part`, and by the loop with the first such page): `Tabs part` is that class, `Tabs part tab` a tab in it, `Tabs part member` a page's member of it (`{Part}` is the class). A page keeps a `Tab member` only for a tab the others do not show. Without all three every page has its own tab members |
| `Opener member by icon` | For a card with several buttons that each open a dialog: a member per button, with `{Icon}` the button's icon as the walk named it. Such a card's dialogs are named after the card and the icon (`TimelineCalendarDialog`). Without this template the card gets one member, as a card with one button does |
| `Dialog`, `Form page`, `Record page` | The class of a dialog, of a page, and of the page of one record (its `{Address}` has `{id}` where the record's id is) |
| `Row`, `Column member` | A list's row |
| `Form page test`, `Record page test` | The test of a page, and of a record's page (opened on a record the suite gives) |
| `Form test` | The test of a dialog a button named in words opens: `{OpenerMember}` is that button on the page's class |
| `Card form test` | The test of a dialog a card's own button opens |
| `Record form test`, `Record card form test` | The same two on the page of one record |
| `Spec head`, `Spec opening <address>`, `Spec page steps`, `Spec button step`, `Spec page rule` | How the suite writes a drafted spec (`bruinet scrape scraper spec`): the head with its fields and intent (`{Id}`, `{Title}`), the chapter that opens the pages of an address (a title line, prose, then its steps), the steps that reach a page (`{Tab}`, `{PageName}`), the step that checks a button (`{Label}`), and a page's rule (`{Tab}`, `{Buttons}`). A page's template carries the page's name for a step with `{PageName}` (its class's words: `ProjectViewComplaintsPage` is "Project View Complaints"). Without these blocks no spec is drafted |
| `{Lane}` in a test's template | A number from 1 to the setup's `Walkers`, the same for a test class every time: a suite that runs a group of tests one after the other names the group with it (`[Collection("Forms {Module} {Lane}")]`), so a module's drafted tests run that many at once |
| `Record save test` | The save test of a dialog on a record's page (`bruinet scrape scraper run --save`, `places draft --as save-test`): the suite's way to make a record of its own, save the form on it and take the record away. `{SaveMember}` is the form's button that saves (by the walk's save words), `{Place}` the place's key. Drafted only for a class the Scraper drafted, and not where the setup's `Writes` is `read-only`. Without the template no save test is drafted |
| `List step` | The wait for a page's list, put where a test's template says `{ListStep}` when the dialog's page shows a list: a page whose list is still loading may not answer a press yet |
| `Row form test`, `Tab step` | The test of a dialog that a button in a list's row opens (a button that shows only an icon and is no card's own): no member of the page stands for it, so the test presses the button of the first row that has `{Icon}`, after a `Tab step` per tab the list is under (`{TabSteps}`). Such a dialog is named after its page and its tab (`EquipmentBookingsFormDialog`) |

A template is text with places in braces: `{Self}`, `{Page}`, `{Namespace}`, `{PagesNamespace}`, `{Module}`,
`{Header}`, `{Summary}`, `{Members}`, `{Address}`, `{Form}`, `{Opener}`, `{OpenerMember}`, `{Checks}`, `{Icon}`,
`{TabSteps}`, `{ListStep}`, and in a member `{Name}` and `{Label}`.

## Generate

An agent makes the controls and improves the drafts through the MCP server's tools: a control from its brief
(`scrape_get_control_brief`), written with `scrape_write_file`, until `scrape_build` passes. Review the result in
your usual diff.

Register the MCP server as in [Copilot And Other Agents](visual-studio-preview.md#copilot-and-other-agents).
Its Scraper tools:

| Tool | Gives or does |
| --- | --- |
| `scrape_list_projects` | the projects with a `scraper.config.md` |
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
| `scrape_list_events` | a recording session's page captures, in order |

Every list takes `max` (50 when left out) and says how many rows it left out as `more`. A tool that asks first acts
on a second call with `confirm: true`.

## When the app changes

Explore or walk the page again, then update its page object. `scrape_list_pages` marks a page whose page object
was made from an older capture; `scrape_diff_page` lists what was added, removed and changed since.
`bruinet scrape places drift` does the same for the classes drafted from places.

## Facts for a spec

Beside each place's fields the list keeps what is known of the values they take, a line per fact with how sure it
is: `Name: max length 255 (stated, maxlength)`. A field's own line, and so the hash a drafted class is held
against, does not change when a fact is learned.

| Level | Means | Comes from |
| --- | --- | --- |
| `stated` | The page says so itself | The field's attributes in a capture: `maxlength`, `minlength`, `min`, `max`, `step`, `pattern`, `inputmode`, `autocomplete`. A capture made before these were kept states nothing: walk the page again |
| `seen` | The app did it when asked | What a test wrote down (`places add --from-run`). Saved empty: `Waste: message when empty Waste is required (seen, saved empty)`. Typed and not saved: `Postcode: max length 20 (seen, typed 300 characters)`, `E-mail: message when wrong not a valid email address (seen, typed x)` |
| `read` | A script says so and nobody tried | Nothing reads scripts for rules yet |

The walk fills no form (AD-043), so typing is a test's work: it types through the page object what does not fit,
reads what the field kept and what the form said, and writes a line `{"place": …, "facts": […]}` to the file the
save tests write to. Such a line adds facts and leaves the place's state as it was. A text control has `Overfill`
for it: it fills a field that may keep less and confirms only that it holds something, where `SetText` confirms the
value took. What was seen counts for more than what is stated: the field table shows the seen limit when there are
both, and a field's message for a wrong value stands in its Message cell after `wrong value:`.

A save test goes one step on where the setup lets the Scraper write: after the empty save it saves with 300
characters in every text field the browser holds no limit for. A save that is taken is a fact (`Remark: takes 300
(seen, saved 300 characters)`: the longest is that or more); one that is refused gives what the form said
(`message when too long`), and the test saves again with a value that fits.

`places fields --spec "<place>"` prints the place's fields as the sections of a screen's spec: the field table
(Field, Kind, Required, Min, Max, Message), "More about a field" (a pattern, what a wrong or too long value was
answered with) and the open questions. A cell there is a fact, a dash where a limit does not apply, or the word
"open" with its question below; a fact only read from a script is a question with its value as the proposal. What
was seen is written once more as "Examples", a row a reader can try again (the empty save, a single letter, a long
save), and a place with columns gets "The list": its columns in their order, with what a capture does not hold
(what a column shows, sorts by and filters on) as open.

A template's test may name the form's button that saves as `{Saver}`: the member of the drafted form the walk's
save words find. A line that names it is left out for a form that shows none, or whose class a person wrote.

`bruinet scrape places fields --facts` lists them; `places fields --table "<place>"` prints a place's fields as a
spec's field table (Field, Kind, Required, Min, Max, Message, From), where a stated or seen limit is a cell, one
only read is a question and what nobody knows is "?". No value a field holds and no choice a select offers is kept:
a capture and the list hold no value of a record (AD-027, AD-043). `bruinet scrape scraper rights` gives the rights
matrix: what each account that walked saw and was refused.

## The loop

`tools/Scripts/scrape-loop.ps1` runs everything end to end, unattended, and writes one report. Run
it after changing the Scraper (from the Bruinet root):

```powershell
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Profile test-tenant -Repeat 3
pwsh tools/Scripts/scrape-loop.ps1 -Target blazor-sample
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Agent copilot
pwsh tools/Scripts/scrape-loop.ps1 -Target sites -Agent none
pwsh tools/Scripts/scrape-loop.ps1 -Target test-site -Factory
```

`-Factory` runs the factory instead of explore, draft and verify, on the test site, from nothing: a new test
project with the draft templates for a plain site (`tools/Scripts/factory/draft-templates.md`: Bruinet's own
controls, a field found by its label, a button by its text) and a browser fixture (`SiteContext.cs`). Its stages:

| Stage | Does | Fails when |
| --- | --- | --- |
| Places | `places seed` (the config's start address, and a map of the pages no link leads to) and `places walk --headless` | the walk fails, or finds fewer places than the site has pages |
| Guards | reads the test site's counters | any counter moved |
| Modules | `scraper modules --write` and the setup's answers for the run | either fails |
| Run | `scraper run --no-walk`: loops that draft, build and test | no loop ran, or no file was kept. A batch that stops by a rule is the factory doing its work, and is read from `state.json` |
| Suite | `dotnet test` on the project as the factory left it | it does not build, or a kept test fails |
| Replay | `scraper replay` | a class would be drafted differently |
| Faults | says what each planted fault ended as | a fault has no outcome at all |

The planted faults are pages of the test site (`pages/faults/`) and one class written by hand into the new
project, each a weakness the factory met on a real app:

| Fault | Ends as |
| --- | --- |
| Two buttons of one name | Kept: the class gets two members (`Details`, `Details2`). Both find the first button: a locator by text cannot tell them apart |
| A tab with a count ("Open (37)", another number on every load) | Kept: the tab is named and found without its count, and a tab drawn as a button is not also a button |
| A slow page, whose form comes two seconds after the page | A reason, `nothing-to-check`: the walk read the page before its form was there |
| A page that needs a record (`record.html?id=7`) | Owed as the page of one record: the plain templates do not say how such a page is opened |
| A wrong hand-written class | Left alone: a person's class is not the factory's to change |

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
