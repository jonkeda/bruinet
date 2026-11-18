# Proposal: making the Bouw7 work faster

Status: **proposal**, for discussion. Date: 2026-11-16.

| | |
| --- | --- |
| What | Where the time went while taking Bouw7 from four journeys to the whole menu and three more journeys, and what would take the most of it away |
| Builds on | `bouw7-whole-app-plan.md` (the work), `Bouw7/.testing/400-FrictionLog.md` rows 64 to 79 (what went wrong), `500-Harvest.md` (the last time this was asked) |
| The short version | Most of the time is not writing tests. It is **waiting for a browser to do the same thing again** (a new browser and a page load per rule, three times per gate), and **finding facts about a page one probe at a time**. Five changes take away most of both: one page load per chapter, read-only tests side by side, a `look` command instead of probe tests, a faster inner loop before the three-run gate, and set-up and clean-up below the screen |
| The numbers | Measured where a run printed them, in this one session, on one machine, against a shared acc. The gains are estimates until tried |

## 1. Where the time went

From the session of 2026-11-16: the map and the opens tests, the explorations, B7.CON.02, B7.PRJ.02 and
B7.CON.03, and the friction fixes between them.

| What | Measured | Why it costs that |
| --- | --- | --- |
| One example of a spec | 8 to 14 s (B7.CON.03: 10 examples in 1 m 22 s; B7.CON.02: 7 in 1 m 40 s) | Each example is a scenario of its own: a new browser, the saved sign-in, the page, then a few checks that take under a second |
| The gate for one journey | 5 to 7 min (B7.CON.03: 3 runs of 2 m 15 s) | Three runs, and each run also repeats the module's other tests (`Module=Contacts\|Spec=…`) |
| The loop for a module with three specs | about 15 min | The module's code tests run once per spec per run: 9 times |
| Every menu item opens | 4 to 5 min for 76 pages, three runs for the gate | One page after another, in one browser |
| A fact about a page | 20 to 40 s a look, plus writing the look | A throwaway probe test per question: about 20 of them this session, for the menu, the contact form, the project form, the delete dialogs, the status field, the contact's page |
| Failed runs before green | B7.CON.02: 4, B7.PRJ.02: 1, B7.CON.03: 2 | A guessed locator, a wait that was missing, the runner on the wrong page: each found only by a full run on acc |
| Exploring 31 routes | about 45 min, one route after another | A run per route, with the config changed between runs |
| Opening every route once to see it | 8 min | A fixed wait per page |
| Making and deleting a record through the screen | about 10 s a contact, a project the same; an hour entry 20 s of a 21 s test (`500-Harvest.md`) | The list is opened, filtered and waited for; the edit form; the question; the list again |
| Finding out that data changed under us | about 20 min, twice | Another agent made a contact and a project in the organization whose counts two specs rely on (friction #72) |

A journey went from nothing to green in 25 to 45 minutes. Roughly a third of that was runs on acc, a third
finding facts, and a third writing the spec, the page objects and the steps. The first two thirds are the
ones a tool can take away.

## 2. Proposals

Sizes: **S** an hour or two, **M** a day, **L** several days. "Where" is where the change is made.

### A. Fewer page loads

| # | Change | Why | Gain, estimated | Size | Where |
| --- | --- | --- | --- | --- | --- |
| A1 | **A chapter's examples share one page when they only read.** The chapter's Given and When run once; each rule's Then runs on the page as it is. A rule with a When of its own (it changes the page) still gets a fresh start, or says how it leaves the page | B7.CON.03 opens the same contact ten times to read ten things; B7.INV.01, B7.CON.01 and B7.PRJ.01 the same. The rules stay separate tests with separate results | 10 examples in about 25 s, not 82 s: three times faster for read-only chapters, which are most of depths 1 and 2 | M | Bruinet (the spec runner) |
| A2 | **Keep the browser between scenarios, clear what the app keeps.** The fixture starts a new browser per scenario because Bouw7 keeps its filters in the browser. Clearing the page's storage, or a new context in the same browser, is enough | The browser's start is part of every example's 8 to 14 s | 2 to 4 s an example | S | Bouw7 (the fixture) |
| A3 | **A page says when it is loaded.** `HeimdallTable` and `B7DataTable` know "loaded": rows, or their own no-results row. Steps wait for that, not for a column, a title or a count | Four of the seven failed runs were a read before the page was ready (#69, #70, #73); each cost a full run and a workaround in the fixture | Fewer failed runs, and no fixed waits in fixtures | M | Bouw7 (the two tables), then Bruinet's `TableControl` |

### B. Runs side by side

| # | Change | Why | Gain, estimated | Size | Where |
| --- | --- | --- | --- | --- | --- |
| B1 | **Read-only tests in parallel.** Tests as Reader, and read-only chapters as Creator, run in several browsers at once; tests that write stay one at a time. A trait says which is which (`Writes` in `TestAccounts.md` already does for Reader) | The opens tests are 76 independent page loads; `xunit.runner.json` runs everything in one line | The opens tests in about 1 min with four browsers; a module's read-only specs the same factor | M | Bouw7 (collections and the runner's settings); check acc's limits first (Q1) |
| B2 | **The gate's three runs at once** for read-only journeys: three browsers, one run each | Three runs prove the test is not flaky; they do not have to follow each other | A read-only gate in the time of one run | S, after B1 | Bruinet (`site-loop.ps1`) |
| B3 | **Explore several routes at once**, and take a list of routes in one run (`--url` more than once, `Within` per route) | 31 routes took 45 min in a row, with the config file changed between runs | About 10 min with four at once; no edits to the config | M | Bruinet (the Scraper) |

### C. Facts without probe tests

| # | Change | Why | Gain, estimated | Size | Where |
| --- | --- | --- | --- | --- | --- |
| C1 | **`bruinet scrape look`**: open an address as an account, do a few named steps (click this text, pick this, fill this), and print what is there: headings, buttons, fields with their labels and required marks, messages, dialogs, the address it ended on. Shadow roots included. It never presses what the guards forbid | About 20 probe tests were written and thrown away this session, each a build and a run, several broken by quoting. `scrape find` reads recordings, and the pages after an action (a form after Create, a delete question, a contact's page) were never recorded | A fact in 10 s with no code; roughly 10 min a journey | M | Bruinet (the Scraper's CLI and MCP) |
| C2 | **Explore records what a form says**: for each form, its fields, which are required, and what an empty submit answers, when the guards allow the submit | The first three rules of every "make a …" spec are these three facts, found by hand each time | The spec's first rules drafted from the recording | M | Bruinet (the Scraper) |
| C3 | **A page with an id in its address is one page** (`/contact#/view/{id}`, `/project/view?id=`), drafted with its tabs as tab pages | Both detail pages were written by hand (#79); every module has one, with up to 16 tabs | A draft instead of a hand-written page per detail page: 15 to 20 of them to come | M | Bruinet (the Scraper; with the open "tab states" item) |
| C4 | **The menu as a map from the Scraper** | The map took an hour of probes (#65); it is done for Bouw7, so this pays only at the next site | Nothing here | M | Bruinet, later |

### D. A faster inner loop

| # | Change | Why | Gain, estimated | Size | Where |
| --- | --- | --- | --- | --- | --- |
| D1 | **Check a spec's steps without a browser first.** Every step of every example is bound to its page and control before anything opens: "The Contacts page has no control 'the tab Financial'" in a second, not after a run | Two of B7.CON.03's failed runs and one of B7.CON.02's were binding errors; the runner already knows them when it starts, but says so per scenario, during the run | A failed run saved per journey | S | Bruinet (a `--bind-only` run, or the loop's stage 5 gate) |
| D2 | **Run one rule while writing, the gate once at the end.** The loop gets `-Rule B7.CON.03.R4` and `-Runs 1` as the way to work, and the three-run gate only when every rule has passed once | The habit this session was to rerun the whole spec after each fix | 1 to 2 min a fix | S | Bruinet (`site-loop.ps1`), and the `scenario-from-spec` skill |
| D3 | **The gate runs what the journey touches.** `Spec=<id>` alone for a spec that runs from its examples; the module's code tests once per loop, not once per spec per run | A module with three specs ran its code tests nine times | **Measured after it was built: small.** Contacts, three specs, three runs: 782 s against about 870 s, a tenth. The estimate here was 15 min to 8, and it was wrong: Contacts has only four code tests (30 s a run); the time is in the examples themselves, which A1 and A2 are about | S | Bruinet (`site-loop.ps1`) |
| D4 | **A failing gate's line in the report's table is its first error** | The build gate failed once and its line said only "Time Elapsed" (#77). The errors were in the report, under the table, and were not read; the earlier version of this row said they were not kept, which was wrong | A rerun saved now and then | S | Bruinet (`site-loop.ps1`) |
| D5 | **A fixture step can say which page it lands on**, or "I open the … page" takes what the page needs | Every detail page needs the workaround of #78 until then | A rewrite of the spec saved per detail page | M | Bruinet (the runner) |

### E. Data below the screen

| # | Change | Why | Gain, estimated | Size | Where |
| --- | --- | --- | --- | --- | --- |
| E1 | **Set-up and clean-up through Bouw7's API** (B18, designed, not built): make a contact, delete by marker, in one call each | A delete through the screen is about 10 s and four waits; depth 4 journeys for every module will each make and delete records | 10 to 20 s a writing example; clean-up that cannot be confused by a slow list (#73) or miss a record (#76) | L | Bruinet (service clients), `Bouw7.Api` |
| E2 | **An organization for the tests alone**, or no exact counts | Two specs fail today because another agent writes in the same organization (#72), and each such surprise costs a triage | No false failures; parallel agents possible | S to decide | The owner (Q2) |
| E3 | **Seed data made once, by a seed run**: an employee, a quotation, a purchase order, a project with hours | Detail and filter rules for the new modules need records to look at; making them per test through the screen is the slowest way | Read-only journeys for modules that are empty today | M, after E1 | Bouw7 |

## 3. What I would do first

In this order, because each makes the next cheaper, and the first four are small.

1. **D3, D2, D4**: the loop runs less and says more. **Done 2026-11-16** (section 6).
2. **A2**: keep the browser. Small, and every example gains.
3. **D1**: bind before the browser opens.
4. **C1**: `scrape look`. The largest saving in the writing of a journey.
5. **A1**: one page per read-only chapter. The largest saving in the running of one.
6. **B1, B2**: side by side, once Q1 is answered.
7. **A3, C3, D5**: the detail pages, before wave 1 reaches the projects' sixteen tabs.
8. **E1 to E3**: before the first module whose journeys mostly write (Purchase, Quotations).

With 1 to 5 done, my estimate for a journey like B7.CON.03 is 10 to 15 minutes from nothing to green,
against 25 to 45 now, and the gate for a read-only journey under 2 minutes against 5 to 7. The plan
counts about 40 journeys still to make, so the difference is in the order of 10 to 20 hours of runs and
looks. That is an estimate from three journeys, not a measurement.

## 4. What not to do

| Idea | Why not |
| --- | --- |
| One run instead of three for the gate | Three runs caught the slow list twice this session. B2 keeps the three and takes the time |
| Shorter timeouts on acc | acc is shared and slow by moments (friction #49, #59); the suite's 15 s is what keeps runs green |
| Mock the back end | The suite exists to test Bouw7 on acc; a mock tests the mock |
| Skip clean-up, sweep later | AGENTS.md: a test cleans up what it made. E1 makes clean-up cheap instead |
| Parallel tests that write, as the same account | They would break each other's counts and filters, as the other agent's records do today |

## 5. Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | How many browsers may be signed in on acc at once, per account and in all? Does a second sign-in end the first? | Try four as Reader on the opens tests; stop at the first sign-in that is refused |
| Q2 | An organization on acc for these tests alone, or specs without exact counts? | The organization: counts are what make "the list shows all contacts again" exact |
| Q3 | May the tests use Bouw7's API on acc for set-up and clean-up, as the account that is signed in? | Yes (the first proposal's Q4 said so); B18's design then gets built for contacts first |
| Q4 | A1 changes what a failing rule means: a later rule of a chapter reads a page an earlier rule read. Is that acceptable for read-only chapters? | Yes, with the rule that a Then never changes the page, checked by the runner |
| Q5 | Which of these go into Bruinet now, and which wait for a second site to ask for them? | A1, C1, D1 to D5 now: any site with specs gains. B3, C2 to C4 when the next site starts |

## 6. What is done

| # | Done | Measured |
| --- | --- | --- |
| D2 | `site-loop.ps1 -Rule <id>` builds and runs one rule and leaves `100-Loops.md` alone; `-Spec <id>` takes one spec. The `scenario-from-spec` skill says: one rule while writing, the gate at the end | One rule of B7.CON.03: 26 s, build included, against 1 m 22 s for the spec. A rule no test names fails, where the filter alone said nothing |
| D3 | A runnable spec's gate runs `Spec=<id>`; the module's code tests once per loop | Contacts, 3 specs, 3 runs: 782 s against about 870 s. A tenth, not the half that was estimated |
| D4 | A failing gate's line in the report's table is its first error | Not measured: it saves a rerun when a gate fails |
| A2 | The scenarios' fixture keeps the browser for the same account and starts it over: the page's storage is emptied, what the saved sign-in held is put back, the app opens on its start page (`Bouw7Session.StartOver`). Another account, or a sign-in that ran out, still gets a new browser | B7.CON.02: 65 s, was 95 to 101 s. B7.CON.03: 72 s, was 82 s alone and 116 to 121 s in the loop. All 53 specs and scenarios once: 6 m 28 s, 7.3 s each, with no filter or search carried from one scenario to the next (the only failures are the three count rules of friction #72) |
| A1 | In Bruinet: a suite that sets `ShareChapters` runs a chapter's steps once for the examples after them that only check (`ChapterRuns`, 9 unit tests); `BddDocumentOrder` runs scenarios in the order of their files (3 unit tests), which sharing needs because xUnit's own order mixes them. In Bouw7: both switched on, and the one Then step that opened a menu closes it again (`VSelect.Close`, 2 unit tests) | All 53 specs and scenarios once: 5 m 13 s, was 6 m 28 s with A2 alone. 12 of the 53 shared a chapter and took 0.1 s each; B7.CON.03 42 s, was 72 s. A wrong value planted in a shared rule failed it, and the rule after it started over and passed |
| C1 | `bruinet scrape look` in Bruinet: opens a page with the project's saved sign-in, clicks what is asked (a text, `field:<label>`, `css:<selector>`) under the project's guards, and prints the page's facts; fields are named by the label beside them. 15 unit tests; described in the Scraper's guide, the CLI page and the `spec-from-app` skill | On acc: the contact form's required fields and the two messages after an empty Create, the project form's fields and its seven statuses, the dashboard's headings: each one command of 16 to 22 s, where each was a probe test of 20 to 40 s plus the writing of it. The guards refused a click on Export. Not built: typing (a look never fills a form), and the same as an MCP tool |

| D1 | `AssertEveryFileBinds` on Bruinet's scenario test base; Bouw7's `Every_step_binds` test (`Category=Bind`); the loop runs it as a gate before any run | Every step of every spec checked in 3 s with no browser; a planted step that cannot bind was caught with its file and line |
| A3 | `HeimdallTable.IsLoaded` and `ReadColumn` (loaded and no longer changing) | The contacts fixture lost its own waiting loop; both Contacts specs green |
| D5 | Nothing built: a page object's step that returns a page already names where it lands. "I open the contact …" moved to the Contacts page | B7.CON.03's chapter reads as a person does it, and binds |
| B1 | Four test collections at once; every class in the assembly's one collection unless it names its own, which the 13 opens classes do | The 76 opens tests: 1 m 33 s to 1 m 48 s, three runs green, was 4 to 5 min. Eight at once: no faster, and one page timed out |
| B2 | `site-loop.ps1 -AtOnce`: the gate's runs as processes side by side | B7.CON.03's whole gate (bind, three runs, trace): 109 s |
| E1 | `Bouw7Api`: the app's own back end with the saved sign-in's token; lists, makes and deletes contacts and projects; deletes only what carries the marker; never writes as Reader. The specs' clean-up uses it | Its four live tests: 4 s, no browser. A delete: one call, was about 10 s on the screen |
| E3 | `Bouw7Seed` and the seed run (`Category=Seed`): a seed client, supplier and project | Made once; a second run changes nothing |
| E2 | The owner: the two organizations we have, for now; more can be made | No exact counts in a spec any more (friction #72) |

| B3 | `scrape explore` takes several `--url`: a run for each, one after another, each kept within its own path. Not side by side: one browser profile, one recording at a time | Materials and Waste in one command, 10 captures each, no change to the config between them |
| C3 | The Scraper already names an address with an id as one page (`/contact#/view/{id}`); what was missing: a page object given such an address is at its address on any record and does not open itself (`PageAddress`, 12 unit tests), and the draft passes the address. The contact's page was explored (12 captures, every tab) and drafted; the hand-written page became its person's part | Both Contacts specs green with the drafted page. Not built: a detail page's tabs as tab pages |
| C2 | Every exploration writes `facts.md`: what each captured state shows (headings, buttons, fields, columns) | Written for the Absence page. Not built: trying an empty submit |
| C4 | A page's links are one of its facts (what each says, where it goes on its own site), so an open menu is a map | The flyout's Master data panel in one `scrape look` with two clicks: its nine links. Not built: a command that walks a whole menu by itself |

What this says about the rest. A1 gained a fifth, not the factor three estimated for read-only chapters, for two
reasons the estimate missed: only 12 of 53 examples can share today (an example with a When of its own, and the first
of each chapter, cannot), and the three rules that fail on the counts of friction #72 each wait 15 to 20 s before they
give up, a minute of the five. What is left of an example's 6 s is the page itself, loaded once per example that
has a When. The next gains are elsewhere: **C1** (`scrape look`) for the writing, **D1** for failed runs, **B1** for the
opens tests, and writing chapters so that more examples only check (a When that every example of a chapter starts
with belongs in the chapter).

## 7. A journey with the tools

B7.PRJ.03 A project's page, the first journey made after everything above (2026-11-17):

| | Before (B7.CON.03, the same kind of journey) | B7.PRJ.03 |
| --- | --- | --- |
| Facts | 3 probe tests written, run and thrown away | 2 `scrape look` commands, and the API for the project's id |
| A step that does not bind | Found by a run on acc, per scenario | Found in under a second, before any run (`Category=Bind`) |
| While writing | The whole spec after each fix | One rule (`-Rule`): 15 s |
| The gate | 3 runs after each other: about 7 min at first | 3 runs at once: 87 s |
| Failed runs | 2 | 2: one of them the whole spec where one rule would have done, 4 min lost |
| From nothing to green | about 40 min | about 20 min |

One journey is one measurement. What it does show: the gate and the facts are no longer where the time goes; the two
failed runs were both about how this page behaves (a link that opens beside the list, a button in a tab), which only
the page can tell.
