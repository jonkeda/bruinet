# Finding every page of the app: a crawler design

Status: built (2026-11-18), all nine steps; what it found and what is left is in "Built" at the end. Asked by the owner: "do we know all pages of the app are
in 180-Pages? and how do we know", then "design a crawler design if we don't have a good crawler already".

The owner's changes to the first version (2026-11-18), all taken in below:

1. "'The guards forbid': we need to loosen this. If a site is acceptance then a lot more is allowed."
2. "Most of the time in modern apps functionality is behind taps and clicks on buttons. So these need to be followed."
3. "Checking javascript/typescript is a good idea."
4. "Writing is allowed on acc environments. But if you can, instead of trying to fill it yourself, first build a
   page object for a form and then click save. And see where it gets you."

## The question

Which places does the app have, and which of them has the suite no page object for? `180-Pages.md` answers only the
second half, and only against the menu: its "addresses without a page" come from `170-Map.md`, 76 addresses the menu
leads to.

## What we have

| Tool | What it does | Why it does not answer the question |
| --- | --- | --- |
| `MenuProbe` (the suite) | Reads the menu's flyout per account, opens each item; wrote `170-Map.md` | The menu only: no record's own page, no tab, no dialog |
| `bruinet scrape explore` | Goes through the app breadth first under the guards, captures each state a safe click shows, replays clicks to reach a state again | Below |
| `bruinet scrape look` | Opens one page and says what it shows | One page at a time, by hand |
| `bruinet suite pages` | The suite's page objects against a list of addresses | Needs that list |

So there is a crawler, and its hard parts are done: the saved sign-in and its renewal, the request guard, reaching a
state by replaying clicks, shadow roots. It was built to record pages for drafting page objects, not to list the app:

- **A budget per run, and nothing kept between runs.** 40 captures, 2 steps deep, 10 minutes. The 48 runs on Bouw7
  each wrote their own `explore.md`. Added up by hand they hold 59 distinct pages, fewer than the menu's 76.
- **Its guards are those of a careful visitor.** A row's buttons, its pen and its menu are never pressed. On an
  acceptance environment that is the wrong default: most of the app is behind them.
- **It spends on states, not places.** Every sorted table is a capture.
- **Tabs are states of a page,** not places with a name.
- **One account per run.** Reader's organization shows 74 menu items where ours shows 59.
- **A long name is read as an id.** A segment of 24 or more letters counts as a token, so
  `/property-asset-management` and `/report-capacity-planning` both come out as `/{id}`. A bug.
- **A run that ends in an error writes no report** (friction 66).

Verdict: not a new crawler. A walk aimed at places, on Explore's engine, that keeps one list, with guards that fit
the environment.

## What a place is

A place is what a page object would be written for.

| Kind | Its key | Example |
| --- | --- | --- |
| Page | The address with ids replaced | `/contact#/view/{id}` |
| Tab | Its page's key and the tab's name, a count left out | `/kam#/` › Activities |
| Dialog or menu | Its page's key and what opened it | `/material-per-unit#/` › + Material |
| Action | Its place and the button, for a button that opens nothing and calls the back end | `/hour-log#/approval` › Approve |

Per place the list holds: the key, the kind, how it was reached (from which place, by which press), an address that
opens it, its heading and tab title, its size (fields, buttons, a table's columns), the calls its buttons make, which
accounts saw it, when it was last seen, and its state in the walk (below). No record's data.

## The design

### 1. Guards by the kind of environment

`scraper.config.md` gets one field, `Environment`: `production`, `acceptance` or `local`. It sets the defaults; the
tables below it still override, per project.

| | production | acceptance |
| --- | --- | --- |
| Links, tabs | Followed | Followed |
| Buttons that open something | Only what is judged safe | All |
| A row's buttons, pen, menu | Never | Pressed, on one row per list |
| Buttons that write | Never | Pressed; the write is let through on a record the walk owns, held back on any other (section 3) |
| Save on a form | Never | By a page object, not by the walk (section 4) |
| Send, mail, pay | Never | Never: a mail or a payment leaves acc |
| Sign out | Never | Never: it ends the walk |
| Other hosts | Never | Never |

For Bouw7 this replaces the "Never click" rows on a list's row buttons. What stays off on acc is what reaches
outside it, and the account that may not write (Reader).

### 2. The walk follows buttons

At each place the walk presses **everything a person can press**, each once: links, tabs, buttons, icons, a row's
pen and menu, the items of a menu it opened. What it learns is what happened:

| After the press | It becomes |
| --- | --- |
| Another address | A page, put on the frontier |
| A dialog, a drawer or a menu over the page | A place of that kind; its own buttons are pressed in turn |
| A new tab's content | A tab |
| Nothing new to see, a call to the back end | An action, with the call's method and address |
| Nothing at all | A note on the button |

What keeps it from running forever:

- **One of a kind.** A list's first row stands for all rows; a key already known is not opened again. 500 rows are
  one visit.
- **No sorting, filtering or paging:** the same place in another order.
- **A budget per place** (time and presses), not per run.
- **A state is left by reloading,** never by its own Cancel or Close, as Explore does now.

### 3. Buttons that write

Pressing "Approve" or "Delete" on somebody's record would change data the tests did not make. So a press has two
modes, by whose record it is:

- **A record that is not ours** (no run marker): the press is made with writing calls held back by the request guard
  (PUT, PATCH, DELETE, and a POST the walk has not seen answer a read). What the button would do is written down as
  an action: "Approve calls PUT /hour-log/{id}/approve". The page may then show an error; the place is reloaded.
- **A record the walk owns** (its name starts with the run marker): every press goes through. This is where a
  record's whole life is walked: its tabs, its status buttons, its delete.

The walk comes to own records through section 4.

### 4. Forms: a page object first, then Save

The walk does not fill in a form. When it meets one it reads it (labels, kinds, what is required, its buttons, the
way `FormProbe` does now), writes it down as a place in the state **form, not saved**, and goes on. That state is a
task for the loop:

1. A page object is written for the form (`FormFields`, typed rows), if the suite has none.
2. One generic test per form, `PlacesSaveTests`: it presses Save on the empty form and writes down what the form
   says (what is required: this alone is harmless and tells a lot); then fills the form through its page object,
   every name with the run marker, and presses Save.
3. Where that lands goes into the list: the address after the save, the messages, the new record. The record is
   handed to the walk as one it owns, and the walk goes on from there with every press allowed.
4. At the end the record is deleted through the app where it can be, through the API where not, and the list says
   which. Leftovers are found by the marker, as now.

So the list drives the page objects, and the page objects open the next part of the list. A form stays visible as
"not saved" until someone has done this; it is never silently skipped.

The states of a place: `known` (on the frontier), `seen`, `form, not saved`, `saved`, `refused` (with why).

### 5. One list that grows: `.testing/175-Places.md`

Written by `bruinet scrape places`, with a JSON file beside it that holds the same and the frontier. A run takes from
the frontier and puts back what it found. A run that stops (acc stops answering for minutes at a time, friction 120)
loses one place, not the walk. `bruinet scrape places add --from-run <step log>` adds what a test visited.

Seeds: the map's addresses, the addresses of the suite's page objects, the 48 old runs' reports, the routes of
section 6.

### 6. The app's own code

Two ways, the first much the better:

- **The source.** If the app's TypeScript can be read (a checkout beside this repository), `bruinet scrape places
  --routes <folder>` reads its route tables: every route, its component, the right it asks. That is the one source
  that lists what no press leads to, and it tells which places need a right or a switch we do not have.
- **The scripts acc serves.** Without the source: the same from the JavaScript each page loads. Minified, so names of
  components are lost, but route paths are strings and survive. Whether Bouw7's scripts give them up readably is not
  tried yet: an hour, before anything else is built.

A route that the walk never reached goes on the frontier with "from the code", and stays there, visible, until it is
reached or explained.

### 7. Per account

A walk per account into the same list, each place marked with who saw it. Reader may not write (the suite's rule for
that account), so as Reader every press is in the held-back mode of section 3, and no form is saved.

### 8. Against the suite

`bruinet suite pages --places <file>` in place of `--map`: a place without a page object goes on "still to make",
with its kind and state; a page object whose address is no place is flagged.

### 9. Repairs to Explore it needs

- A long name is not an id: a segment with hyphens between words is a name.
- A report also when a run ends in an error.

## Step 0, tried (2026-11-18): the scripts acc serves

The owner chose the scripts over the source. Done by hand, as Creator's saved sign-in, reading only: the 53 addresses
of the map were fetched, and the 60 scripts they load saved and read as text.

**It works, for the part of the app that is a single-page app.** Each module's page loads its own script
(`/vue-dist/<module>/app.js`), and its route table is in it, readable: the paths as they are, and the component's
name in the name of the part it loads (`frontend_vue_pages_contact_components_Deduplicate_vue`).

| | |
| --- | --- |
| Module scripts | 39 |
| With a route table | 16, holding 71 routes |
| Of those routes, an address of the map | 38 |
| Of those routes, an address in a page object | 32 |
| In neither | 29 |

The 29, of which the suite reaches eight another way (QHSE's five as tabs, the department's update page, the
quotation's reminders, the object's create page), so some 21 are new:

- **A record's own page and its update form:** branch (create, update, view), contact (update), employee (update),
  purchase invoice (create, update, view), purchase order, subcontractor and delivery ticket (view), object (update,
  view), quotation (view, update, create for a project, attachments).
- **Pages nobody knew:** `/contact#/import`, `/contact#/deduplicate`, `/material-per-unit#/import`,
  `/garbage-per-unit#/import`, `/hour-log#/pay-off`, `/my-account#/integrations`.

What it does not give:

- **23 module scripts have no route table:** one page each, on an address of the server (the project and its tabs,
  the invoice form, the reports, the planning). Their tabs and dialogs are components, not routes. The scripts name
  75 components and some hundred server addresses, but a page's address and a call's address look alike in the text
  (`/project/timeline`, `/project/set-internal-note`): they are a list to walk, not a list of pages.
- **Nine addresses refused a plain fetch** (403: the five connections, VAT rates, types of hour, attributes, file
  types, mileage; Rights gave 500), though Creator opens most of them in the browser. Their scripts were not read;
  the command must fetch through the browser.
- **Parts loaded later** (a dialog's own script) were not fetched; their names are known, their content not.
- What Reader's organization loads beyond ours.

So: the scripts are a good second source for routes, and a list of leads for the rest. They do not replace the walk.

## How we would know

Never for certain. What can be said after it is built:

| Claim | Check |
| --- | --- |
| The walk is finished | The frontier is empty, for both organizations |
| No form was skipped | No place is in the state "form, not saved" |
| It is stable | A second walk adds no place |
| Nothing is hidden from presses | Every route in the code is a place, or has a line saying why not |
| It matches what tests do | Every address a test's step log visited is a place |

What stays unknown, and is written in the document as such: what only shows for a right none of our accounts has, or
for a feature switched off on acc. With the source (section 6) even those are named, though not seen.

## Safety

- Acc hosts only. Production's guards are the default for a project that does not say `acceptance`.
- Nothing sent, mailed or paid; no sign-out.
- A write lands only on a record with the run marker, made by a test through a page object, and is cleaned up.
- Nothing written as Reader.
- The list holds no secret and no record's data, so it can be tracked. Calls are written as method and address, ids
  replaced, never a header or a body.

## Building it, in order

| Step | What | Size |
| --- | --- | --- |
| 0 | **Done.** The routes from the scripts acc serves, by hand: 71 routes, some 21 new places | 1 hour |
| 1 | The two repairs to Explore | Small |
| 2 | The list and its frontier, filled from the old runs' reports, the map and step 0 (no acc needed) | Small |
| 3 | `Environment` and the acceptance guards | Small |
| 4 | The walk: links, tabs, and every button, with writes held back | Medium to large |
| 5 | `suite pages --places` | Small |
| 6 | Forms: the state, `PlacesSaveTests`, owned records, the walk going on from them | Medium, then per form |
| 7 | Reader's walk | Small |
| 8 | `--routes` as a command | Small to medium |

Steps 0 to 2 alone give a better answer than today's. Step 6 is the long one: Bouw7 has some 30 forms, and each
needs its page object before its Save can be pressed.

## For the owner to decide

1. **The source.** Decided (2026-11-18): "No, use acc's scripts".
2. **Saving a setting.** Settings are switches for the whole organization, and decision 005 forbids saving one.
   "Writing is allowed on acc" may lift that. Proposal: save a setting only where the test can put the old value
   back, and say so per setting.
3. **Send and mail stay off?** Proposal: yes, also on acc, unless acc's mail is known to go nowhere.
4. **Where it lives.** Proposal: in Bruinet's Scraper, for any suite; the list in the suite's `.testing`.

## Built (2026-11-18)

All steps of "Building it, in order" are built, in Bruinet's Scraper (`bruinet scrape places show|seed|routes|walk|add`,
decision AD-043 there) and in the suite (`PlacesSaveTests`, `Support/Places`). The list is `.testing/175-Places.json`,
its document `175-Places.md`.

| | |
| --- | --- |
| Places in the list | 416: 135 pages, 96 dialogs, 95 menus, 65 tabs, 17 parts of a page, 8 buttons that only call the back end |
| Pages with an address | 124, every one opened by the walk; none left to open |
| Pages the scripts name without an address | 11: a record's own page (`/quotation#/update/{id}`, `/branch#/view/{id}` and so on) that no row or link led to |
| Routes read from acc's scripts | 73, of which 23 were in no map, page object or recording |
| As Creator | 102 pages seen, 14 refused |
| As Reader (links and tabs only) | 104 pages seen, 18 refused: Approval and a project's own tabs |
| Forms found | 77: 63 dialogs and 12 pages not saved, 2 saved by a test (department, category) |
| Writes held back | 4 counted: 3 in Creator's first pass, 1 in the second; none in Reader's |
| Addresses without a page object | 63 of 118 (`180-Pages.md`, "In the list of places, without a page") |

The answer to the question this started with: 180-Pages held 70 pages; the app has at least 124 addresses and 11 more
pages by name.

What is not done:

- **75 forms are not saved.** Each needs a page object and a way to delete what it makes before it joins
  `PlacesSaveTests`.
- **A write on the walk's own record is not shown on acc.** The walk did not press the yes of the department's
  "Delete?" (friction 131: two buttons of one name told apart by their order). It is shown on the tool's test site.
- **11 pages have no address.** They need a record of their kind that the walk owns, or a row that leads there.
- **`/project/view` is refused to both accounts** when opened without a project; its tabs were opened through a
  project's link.
- **Admin and NormalUser have not walked.**
- Decisions 2 and 3 of "For the owner to decide" are still open: nothing saved a setting, and send and mail are off.
