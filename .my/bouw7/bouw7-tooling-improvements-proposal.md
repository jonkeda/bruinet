# Proposal: what the walk of Bouw7 taught, as improvements to the tooling

Status: proposed (2026-11-18), nothing of it built. Asked by the owner: "create a doc with improvement proposals from
everything you learned: scraper, component recognizer, control recognizer, etc."

| | |
| --- | --- |
| What | 38 proposals in eight groups, from one day of building the places walk and running it on acc |
| Where the evidence is | `Bouw7/.testing/400-FrictionLog.md` rows 120 to 133, `175-Places.json`, `160-Controls.md`, `180-Pages.md`, and the design `bouw7-crawler-design.md` ("Built") |
| The short version | The walk now finds the places. The next cost is turning 63 pages and 75 forms into page objects, and almost none of what the walk read is kept in a form a recognizer can use. Most proposals below close that gap |
| The numbers | Counted on 2026-11-18. Sizes are estimates |

Sizes: **S** is hours, **M** is a day or two, **L** is more.

## The first five, if only five are picked

| # | Proposal | Why first |
| --- | --- | --- |
| 2.1 | The walk keeps a snapshot of each place | Every recognizer below needs it; without it each page is opened again by hand |
| 3.1 | A field's kind read from its element, not from its role | 690 of the list's fields say only "text"; no control can be chosen from that |
| 1.1 | Two elements of one name told apart by where they are | The only open fault of the walk: a dialog's own button is taken for the page's |
| 4.1 | A form's page object drafted from its snapshot | 75 forms, each written by hand today |
| 7.1 | One writer per checkout | Another session committed and moved this work while it ran, twice |

## 1. The Scraper's walk

What was seen: the walk opened 124 pages and made 984 presses in two passes as Creator and one as Reader. Its faults
were found one at a time, each by a run that had to be stopped (friction 126 to 132).

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 1.1 | Tell two elements of one name apart by their place in the page (the dialog they are in, their parent's name), not by their order | The yes of a department's "Delete?" was never pressed: the dialog's Delete came before the page's and was taken for it (friction 131) | The walk deletes its own department on acc | M |
| 1.2 | A record of the walk's own for every kind of record: a save test leaves it, `walk --own` opens it, the test's next run removes it | 11 pages the scripts name have no address; each needs a record to open. Today `--own` takes one address by hand | Pages without an address: 11 to 0 | M, then per form |
| 1.3 | Walk as every account of `TestAccounts.md` in one call, and say per place who sees it | Admin and NormalUser have not walked; Reader's walk was started by hand | The list has a column per account | S |
| 1.4 | A dry run before a long walk: open five pages, press what would be pressed, print it, write nothing | Three walks were stopped after ten minutes for noise: the column chooser's every line, an editor's bold and italic, each tick box (friction 128) | No walk stopped for noise | S |
| 1.5 | A page that does not load in time is tried once more at the end, with what it was waiting for written down | `/right` needs 29 to 35 s on acc; the walk said only "did not open" (friction 133) | A "did not open" names the request that was still open | S |
| 1.6 | The walk's state on disk after each page as a small file of its own, the list written at the end | The list (15 000 lines) is written whole after every page, and another program holding it ended two walks (friction 132) | A walk survives a held file; the list changes once per walk | S |
| 1.7 | The scripts' routes read for their guards too: which right a route asks for | The routes gave 73 addresses but not who may open them; that came from two walks of an hour | Refusals known before a walk for the routes that say so | M, uncertain: depends on how the app's scripts name rights |
| 1.8 | What a press called, kept as the place's contract: the call's address and the fields of its body, without values | 8 buttons only call the back end and 4 writes were held; the list keeps the address and nothing more | A held write shows which fields a form sends | M |

## 2. The component recognizer

What was seen: the patterns tool (`scrape_detect_patterns`) works on recorded pages. The walk reads every page and
keeps only names, so nothing it saw can be given to the patterns tool. And what the list shows as repeated is plain to
a reader: 95 menus of which 64 are one column chooser, 63 dialogs that hold a form, the same row menu on
list after list.

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 2.1 | The walk keeps a snapshot per place (page, dialog, menu, tab), as a recording does, beside the list | The walk read 416 places and kept none of their structure. Writing a page object means opening the page again | Patterns can be asked over all 416 places | M |
| 2.2 | Patterns over places, grouped by kind: the same dialog frame, the same menu, the same table, the same filter row | `160-Controls.md` was written from 6 pages; the 63 new pages have not been held against it | A table: pattern, on how many places, the component that covers it or "none" | M |
| 2.3 | A place that an existing component covers is said so in the list ("a `HeimdallTable`", "the column chooser") | Each list page's column chooser is a place of its own with 20 lines; it is one component | Menus in the list: 95 to 31, the 64 column choosers one line each | M |
| 2.4 | Web components by their tag: every `b7-*` and `a-*` element counted over all places, with whether a control or component has that tag | The app's own elements (`b7-data-table`, `b7-alert`, `a-date-picker`) are the surest sign of a component; the list in `160-Controls.md` was put together by eye | The count is a command's output, not a reading | S, after 2.1 |
| 2.5 | A dialog is named after its heading, not after the button that opened it | "`/day-off › + Day off`" is the same form as "`/day-off › fa-pen`"; they are two places today | Dialogs in the list fall where two buttons open one form | S |

## 3. The control recognizer

What was seen: the walk names a field by its label and its role. Of the fields in the list, 690 are "text", 104
"number", 84 "combobox", 26 "textarea", 22 "checkbox". A date field, an autocomplete and a plain text field are all
"text". The forms written last week needed a probe per form (`FormProbe`) to find the kinds.

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 3.1 | A field's kind from its element and what is around it: the wrapper's class (`v-select`, `v-autocomplete`, `v-text-field`), what it opens (a date picker, a list), its input type | "text" for 690 fields | Fields with only "text": under 300, the rest named by control | M |
| 3.2 | A table of signs per control library, kept beside the library: "an element like this is a `VSelect`" | `scrape_suggest_controls` knows Bruinet's own controls; what marks a Vuetify 2 control is in the heads of whoever wrote `Bruinet.Web.Vuetify2` | A control added to a library says how it is recognized, with a test | M |
| 3.3 | Fields that no control fits, listed over all places with a count and one example | Known today by having tripped over them: a colour palette button, three bare checkboxes, Equipment's unit lines, the file input, the date picker with presets | One list of controls to make, ordered by how many fields wait for each | S, after 3.1 |
| 3.4 | Required, disabled and read-only kept per field as facts of their own, and the message a field shows when it is left empty | The list says "text, required" for 9 fields; the save tests found "Name is required" by saving empty | An empty save's complaints are known per form before a test is written | S |
| 3.5 | A button with only an icon named by what it does: its tooltip, its `aria-label`, then its icon | Places are called "fa-pen", "fa-line-columns", "mdi-chevron-up"; a class that says how an icon is drawn was once taken for its name (friction 128) | No place in the list named after an icon where the page says more | S |

## 4. The page object drafter

What was seen: 63 addresses and 75 forms have no page object. Last week's forms were written by hand and by script
from a probe. The forms proposal's draft tool (A4, A5 there) was not built.

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 4.1 | A form's page object drafted from its snapshot: a member per field, named after its label, typed by 3.1's kind, sections as members | Each form took a probe run and a hand-written class | A drafted form builds; the fields `MasterDataFormsTests` checks are all there | M |
| 4.2 | A list page's typed row drafted from its table's columns | 14 pages have a table without a typed row (`180-Pages.md`) | "No typed row": 14 to 0 | S |
| 4.3 | The read-only check drafted with the page: every field the page object declares is on the page | `MasterDataFormsTests` does this for 13 forms, each written by hand | A new page arrives with its check | S |
| 4.4 | A drift check: the page object held against a fresh snapshot, the differences as a list | acc was deployed at 10:10 that morning; nothing said whether a page had changed | `suite pages --check` fails when a field went or came | M |

## 5. Forms and saving

What was seen: two forms are saved by a test, 75 are not. A form joins `PlacesSaveTests` when it has a page object
and a way to delete what it makes; the department is deleted through its own form, the category through the API.

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 5.1 | How each kind of record is deleted, as a table per suite: by its form, by its row's menu, by the API, or not at all | Found per form by trying. Where there is none, a form cannot be saved by a test | Every form of the list says how its record goes, or that it cannot | S to find, M to build |
| 5.2 | A sweep before and after a run: every record whose name starts with the run marker, of every kind in 5.1's table | B7.PRJ.02.R6 failed once with a project of the day's marker in the list; whether an earlier run had left it is not known | acc holds no marked record after a run, asserted | M |
| 5.3 | A value per kind of field for filling in a form, so a save test is the form's page object and one line | The two save tests each choose their values by hand | A form joins `PlacesSaveTests` with its page object and its row in 5.1 | S, after 4.1 |
| 5.4 | Decide what a setting is to the tests | Settings are switches for the whole organization; decision 005 forbids saving one, "writing is allowed on acc" may lift it. Open since the design | An owner's yes or no, per setting where the old value can be put back | Owner |

## 6. The list of places and the checklist

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 6.1 | The list as one file per page, with its dialogs, menus and tabs in it | One file of 15 000 lines: it cannot be reviewed, and every walk changes all of it | A walk of one page changes one file | S |
| 6.2 | What a record said is kept out of the list by rule: a table's cell texts, a record's name in a heading | A record's text became a place's name twice; one entry was removed by hand | No name of a contact, project or person in the list | S |
| 6.3 | `suite pages` counts dialogs and tabs too: which have a page object or a member | The checklist holds pages against addresses; 96 dialogs and 65 tabs are not held against anything | A dialog without a page object is a row | S |
| 6.4 | A walk's result as a difference: places that came, went and changed since the last walk | The list says what is; after a deploy nothing says what is new | A deploy's walk prints a short list | S, after 6.1 |

## 7. Speed and the environment

What was seen: the largest cost of the week was not a tool fault but one fact read wrongly for days. A browser whose
requests are routed keeps no cache, so every page fetched 17 MB of scripts again; it was read as "acc flaps"
(friction 120 and 125).

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 7.1 | One writer per checkout: a session that walks or builds for hours works in its own worktree | Another session committed this work under its own message, moved its design document, and built at the same time, which left a broken reference file (friction 129) | No commit holds another session's unfinished work | Habit, and a line in `AGENTS.md` |
| 7.2 | A run says what a page cost: bytes fetched, bytes answered from what is kept, the slowest request | The cause was found by timing requests by hand after hours | `test-timings.md` has a line per page; a page over 20 s names its slowest request | S |
| 7.3 | The environment's own state at the start of a run: its deploy time, one known page's load time | acc was deployed at 10:10; the line fell from megabytes to 150 KB a second in the afternoon. Neither was known to a run | A failed run says first whether acc was slow or new | S |
| 7.4 | A failure for the environment's reasons is marked so, not counted as a failed test | `/right` failed two tests of 310 for acc's reasons | A whole run's count has three numbers: passed, failed, environment | M |
| 7.5 | Ask the owner of acc for compressed scripts | acc serves its scripts unpacked: the vendors script alone is 8.8 MB | A page's scripts: 17 MB to about 4 | Theirs |

## 8. The work itself

| # | Proposal | What was seen | Measure | Size |
| --- | --- | --- | --- | --- |
| 8.1 | A long build is cut into steps that each end in a state a person can look at, with a word to the owner at each | "You have been busy for 4 hours?" was a fair question: nine steps were built in one go | No stretch over an hour without something to show | Habit |
| 8.2 | A walk's faults are found on the test site first: a page there for each thing acc tripped over (a "leave site?" question, a page that sends itself on, two buttons of one name, a row that opens by pressing) | Each was found on acc, by a run of ten minutes to an hour, and only then given a test | The next fault of this kind fails a test in seconds | M |
| 8.3 | Scripts with quotes or backslashes are written to a file, never typed into a shell | About ten failed edits (friction 129) | None | Habit |

## What this does not propose

- **A recognizer that writes page objects without a person.** The drafts of group 4 are a start to rename and prune,
  as the scrape skill already says of `scrape_suggest_controls`.
- **Reading charts.** What a test reads from a chart is still the owner's decision (`160-Controls.md`).
- **Sending, mailing and paying on acc.** They stay off (the design, decision 3).

## For the owner to decide

1. Which of the first five to build, and in which order. The proposal: 2.1, 3.1, 4.1 in that order, as one piece of
   work, because each needs the one before; 1.1 beside them; 7.1 now.
2. 5.4: may a test save a setting on acc when it can put the old value back?
3. 7.5: who asks acc's owner about compression.
