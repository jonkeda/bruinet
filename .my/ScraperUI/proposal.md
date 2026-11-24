# Proposal: a person clicks, the Scraper records, the chat steers

Status: **proposal**, for discussion. Date: 2026-11-24.

| | |
| --- | --- |
| What | A way to scrape a production site: `bruinet scrape follow` starts Chrome, a person clicks through the app, and every page, tab and dialog they reach is recorded as a place. The person steers it from the model's chat; the model never clicks |
| Why now | On `Environment production` the walk presses only what is known to show more (AD-043), so most of what is behind buttons is never found. Letting it press more on production is not on the table |
| The UI | None of its own: a normal Chrome window and the chat. No toolbar, no picker, no Scraper window |
| What it reverses | AD-027 rules out "a command or tool that waits for a person to browse, click or pick in the Scraper's browser", and recording by hand was removed on 2026-11-19. This brings a part of it back, for one case. Section 6 says what changes in the decision and what stays ruled out |
| Builds on | The walk and the list of places (`Places/Walker.cs`, `175-Places.json`), `scrape look`, the request guard (`Explore/RequestGuard.cs`), the saved sign-in |

## 1. The problem

| # | What holds | What follows |
| --- | --- | --- |
| 1 | Some sites to scrape exist only as production | There is no acc to press freely on |
| 2 | A model that clicks on production is too dangerous | A danger word list is a guess at what a button does; one wrong guess sends a mail or deletes a record that is someone's |
| 3 | A walk that does not click finds the menu and the links, no more | Forms behind Create, a record's tabs, dialogs and the pages after a save stay unknown |
| 4 | A person knows what a button does on their own site | The person presses; the tool only watches |

The judgement "is this press safe here" moves from `ClickJudge` to the person, for the presses `ClickJudge`
does not allow. Everything after the press (reading the page, naming the place, drafting) stays as it is.

## 2. What it looks like

```powershell
# from the app's Scraper project
bruinet scrape follow --as Admin
```

Chrome opens on the start address with the project's saved sign-in. The person uses the app as they
would. In the chat:

> **Person:** start following Bouw7 as Admin
> **Model:** Chrome is open on the dashboard. The list has 76 places; 31 pages have buttons nobody pressed
> yet. The module that owes most is Projects: start at `/project`?
> **Person:** *(clicks Projects, a project, the tab Planning, the button New phase)*
> **Person:** what did you get?
> **Model:** 4 new places: the project's page, its tab Planning, the dialog New phase (6 fields, 2
> required). On this page Export and Archive were not pressed.
> **Person:** Archive mails the client, skip it. *(clicks Export)*
> **Model:** Noted on the place: Archive is not to be pressed, "mails the client". Export opened a
> download, recorded as a button that only calls the back end.
> **Person:** stop
> **Model:** 14 new places, 3 dialogs, 1 note. `scraper status` now owes 9 classes for Projects.

The model's part is the navigator: it knows what the list of places still lacks and says where to go
next. The person's part is the hands.

## 3. How it works

| Part | What it does | From what exists |
| --- | --- | --- |
| **Start** | Opens the installed Chrome, with a window, on the browser profile the saved sign-in is in. A sign-in page is the person's to fill in, as now | `scrape check`, `look --as` |
| **A step** | A script in the page reports each click before it happens: the pressed element's role, label and locator. Never a key, never a typed value. After the click, the page is read once it is still | New: the click listener. The reading is the walk's |
| **A place** | What the page shows after a step is held against the last reading. A new address, a tab's content or an open dialog is a place, written into `175-Places.json` with the press that led to it, exactly as the walk writes one | `Walker`, `PlacesReading`, `NodeSignature` |
| **Calls** | The calls the page made after the step: method, address, status. On production never what a call sent | `look --calls`, `IPageCalls` |
| **The session** | One process holds the browser; the MCP server starts it and talks to it. It ends when the person says stop, closes Chrome, or after a time without a step | `ScraperService.Sessions` |

Everything downstream reads the list of places and does not know who pressed: `places read`, `fields`,
`draft`, `scraper status`, `scraper run --no-walk`.

### What the chat can do

Four MCP tools, each also a command (`bruinet scrape follow status` and so on):

| Tool | What it does | Can it change the site |
| --- | --- | --- |
| `scrape_follow_start` | Starts the session (`--as`, `--url`) | No: opens the start address |
| `scrape_follow_status` | The steps since the last ask, the new places, and on the page that is open: what was not pressed yet | No: reads |
| `scrape_follow_note` | Writes a person's remark on the current place: a name, "not to be pressed, because", "only shows for a draft invoice" | No: writes the list |
| `scrape_follow_stop` | Ends the session and prints the summary | No |

There is no tool that clicks, types or presses a key, and none is to be added. That is the proposal.

One open point is whether the model may **open an address** (Q1): it would let "go to the next page that
owes something" work without the person finding it in the menu, and an address from the list of places is
one the walk already opened. An address is still a request on production.

## 4. Guards

| Guard | Rule | Why |
| --- | --- | --- |
| **Who presses** | Only the person. The session has no route from a tool to the page's input | Point 2 |
| **Writes** | Held back at the request guard by default, as on the walk: the person can press Delete and see the question without the call landing. `--let-writes` at the start lets them through; it is a flag the person types, not a tool, and it cannot change during a session | A person exploring presses things to see what they are. A page that only shows after a real save needs the flag |
| **Hosts** | Only the config's `ExploreHosts`. A step that leaves them is not recorded | As Explore |
| **What a person typed** | Never read: no key events, no `value` | AD-027, unchanged |
| **Other people's data** | On `Environment production` a kept capture holds the page's structure (labels, buttons, columns, field names) and no text of a row or a record. Masks apply before anything is written | Production rows are real customers. The list of places already holds no record's text (AD-043); here the captures follow it |
| **What the model sees** | `scrape_follow_status` returns what the list holds: structure, not rows | The chat is a place data leaves the machine from |

## 5. Steps to build it

Sizes: **S** an hour or two, **M** a day, **L** several days.

| # | Step | Done when | Size |
| --- | --- | --- | --- |
| F1 | `scrape follow` records pages: Chrome with a window, a place per new address a person reaches, the press that led to it | On the test site a scripted person (Playwright clicking for them, in the test only) reaches three pages; the list holds them with their presses. A typed value is in no file | M |
| F2 | Tabs and dialogs: a step that changes the page without a new address | The test site's dialog and tab places are equal to what the walk writes for the same presses | M |
| F3 | Writes held by default, `--let-writes`; structure-only captures on production | The test site's counters do not move on Delete without the flag; a capture of the table page holds no cell text | S |
| F4 | The four MCP tools and `status`' "not pressed yet" | An agent given only these tools cannot move a counter of the test site | M |
| F5 | The skill: `scrape-app` gets a production path (follow, then `scraper run --no-walk`) | One module of a real production site through to drafted classes | S |
| Later | Marking in the page what was not pressed yet; several people's sessions merged | Only if F5 shows the chat's list is not enough | |

F1 to F3 are useful without the chat: a person runs the command, clicks, closes Chrome.

## 6. What changes in the decisions

AD-027 would be amended, not replaced:

| Now | Becomes |
| --- | --- |
| Ruled out: a command or tool that waits for a person to browse, click or pick in the Scraper's browser | Allowed: `scrape follow`, where a person presses and the Scraper records places. Still ruled out: a toolbar, a picker, any window of the Scraper's own |
| A session is made by Explore or the walk only | Also by follow; what it writes is the same list of places |
| Broken when a Scraper feature needs a person in a browser to be tested | Unchanged: follow is tested with a scripted person on the test site |

Stays ruled out: drafting a test from what a person did; a recording that holds an action's typed value;
a model call inside the Scraper (the chat calls the Scraper, not the other way round).

AD-043 gets one line: on `production`, what the walk may not press, a person may, through follow.

The reasons hand recording was removed still hold where the walk can press: nobody used it, and a walk
repeats itself after every change while a person does not. Follow is for the sites where the walk may not.
The cost is real: **what a person found is not found again by itself.** When the site changes, `places
drift` says which followed places changed, and a person follows those again.

## 7. Questions

| # | Question | Proposed |
| --- | --- | --- |
| Q1 | May the model open an address during a session? | Yes, but only one already in the list of places, and only after the person says so in that turn. No address it made up |
| Q2 | Writes held by default, also for the person? | Yes. It makes exploring safe, and `--let-writes` is there for the page after a save |
| Q3 | Do production captures go into the repository? | Structure only, yes: without them replay and drift do not work. If the labels themselves are sensitive for a site, that site's `Recordings/` is ignored and replay is local |
| Q4 | Installed Chrome, or the Scraper's Chromium? | Installed Chrome (`channel: chrome`), in the Scraper's own profile, not the person's daily one: no extensions, no other tabs, and the saved sign-in is already there |
| Q5 | Is the removed recorder's code worth reading first? | A look at the commit before 2026-11-19 for the click listener; the toolbar and picker stay gone |
