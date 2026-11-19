# The loops factory: small groups, all the way through, a little better each time

Status: proposed (2026-11-18). Nothing of the factory itself is built; the tools its stations use are (see "What is
there"). Asked by the owner: "design a loops factory, where you scrape and create a few page objects and tests, and
then check and improve the process, and then loop again"; then "the idea is to do small groups for everything and then
improve each time a little bit", and "the loops should loop until you are done".

| | |
| --- | --- |
| What | A way of working: one small group of places goes through every station (walk, recognize, draft, test, run), then the process gets one small improvement, then the next group. It repeats until the queue is empty |
| Why | The other way was tried on 2026-11-18: nine steps built in one go, then every page walked at once. It took hours with nothing to look at, and each fault was found late, on acc |
| Size of a group | One page with its dialogs and tabs, or two or three small pages of one module: at most six classes |
| Done | Every page of the list of places has a class and a test that opens it, or a line that says why not |

## The words

| Word | Meaning |
| --- | --- |
| Place | A page, a tab, a dialog, a menu, or a button that only calls the back end, as `.testing/175-Places.json` holds it |
| Group | The places one loop takes: a page and what opens on it |
| Station | One step of a loop. Each has one tool, one result and one check |
| Loop | A group through all stations, then the review |
| Queue | The groups still to do, in order |
| Ledger | `.testing/190-Factory.md`: a row per loop, with what it made, what it cost and what it changed |

## One loop

A loop takes a group from the queue and passes it through eight stations. A station that fails stops the loop at that
station; the group stays in the queue with the reason.

| # | Station | Does | With | Check before the next |
| --- | --- | --- | --- | --- |
| 1 | Pick | Takes the next group from the queue | the ledger and the list | The group has an address that opens |
| 2 | Walk | Opens the group's pages on acc and presses their buttons, each once; keeps a snapshot of each place | `bruinet scrape places walk --only <address> --again` | The walk opened every page of the group |
| 3 | Recognize | Says which control each field is, which have none, which component a dialog or menu is | `places read`, `places fields`, `places elements` | No field of the group reads "no sign fits" without a reason written down |
| 4 | Controls | Adds what station 3 found missing: a sign, a control, a component. Only what this group needs | the control skills, `control-signs.md`, `## Control signs` | The suite builds, the control's own test passes |
| 5 | Draft | Drafts each place's class: a form, a list's row, a page | `places draft` | Each draft builds |
| 6 | Finish | Renames and prunes the drafts, adds the openers on the page, writes what no draft can (a tab, a button's meaning) | by hand, through page objects only | `suite pages --check` passes; no forbidden call |
| 7 | Test | A read-only test per class: the page opens, the form shows the fields its class declares. A save test only where a way to delete is known | `places draft --as form-test`, `PlacesSaveTests` | The group's tests pass three times in a row on acc |
| 8 | Review | Looks back on this loop only: what took longest, what was done by hand twice, what failed. Makes **one** improvement, to a tool, a template, a sign table or this design | the ledger, the friction log | The improvement has a test or a measure, and the ledger says what it was |

Stations 2 and 7 use acc, one at a time. Nothing else does.

## The order of the queue

Small and plain first, so the tools get better before the hard pages come.

| Order | Groups | About | Why here |
| --- | --- | --- | --- |
| 1 | Master data lists with one dialog (day off, absence, branches) | 6 | The forms are small and the suite already has examples to hold the drafts against |
| 2 | Form pages with an address (`/branch#/create`, `/employee#/create`, `/purchase-invoice#/create`) | 5 | They open without a record |
| 3 | Settings pages (`/my-account#/…`, the five connections) | 20 | Many pages of one build: the pattern tool earns its keep here |
| 4 | QHSE tabs and a project's own tabs | 25 | They need a record to be there |
| 5 | Record pages without an address (11) | 11 | They need a record the tests made; station 7's save tests come first |
| 6 | Reports, schedule, dashboard | 10 | Charts and a timeline: controls that are still to decide |

The first groups have page objects already in part. That is on purpose: the first loops hold the drafts against
classes a person wrote, which says how good a draft is before any draft is trusted.

## Until it is done

The factory runs loop after loop without waiting for a word between them. It stops by itself in five cases.

| It stops when | Then |
| --- | --- |
| The queue is empty | Done: the last ledger row says so |
| The same station fails for two groups in a row | The process is at fault, not the group: it stops and reports |
| acc does not answer (a sign-in that cannot be renewed, pages over 30 s) | It stops; nothing is retried in a loop |
| A loop would need something the owner decides (saving a setting, sending, a chart's meaning) | That group is parked with the question; the factory goes on with the next |
| Three loops in a row made no improvement and each took longer than the one before | It stops and asks for a look |

A parked group is not a failure. The ledger lists the parked groups and their questions together at the end.

## What a loop leaves behind

| Where | What |
| --- | --- |
| The suite | The group's classes (`*.scraped.cs` and their hand-written part), their tests |
| `.testing/175-Places.json` | The group's places with a snapshot, controls per field, a component where one fits |
| `.testing/180-Pages.md` | The checklist, written again |
| `.testing/190-Factory.md` | One row: the group, minutes per station, classes and tests made, drafted lines kept against lines written by hand, the improvement |
| `.testing/400-FrictionLog.md` | A row per thing that had to be worked around |
| A commit per loop | Only when the owner has said so; until then the work stays uncommitted |

## The measures

Four numbers per loop, so "a little better each time" can be seen.

| Measure | How |
| --- | --- |
| Minutes per class | The loop's time over the classes it made |
| Drafted share | Lines of a finished class that the draft wrote, over all its lines |
| Fields without a control | From `places fields`, for the group, before station 4 and after |
| First-run passes | The share of the group's tests that passed the first time on acc |

The review's one improvement aims at the worst of the four.

## The improvements waiting

The 38 proposals of `bouw7-tooling-improvements-proposal.md` are the stock the reviews draw from. They are not
built up front any more: a proposal is built in the loop that first needs it.

| Built already (2026-11-18) | Waiting, to be picked by a review |
| --- | --- |
| A snapshot per place (2.1); controls by sign tables (3.1, 3.2); fields without a control (3.3); required from the page (3.4); buttons named by what the page says (3.5); the app's own elements (2.4); a component per dialog or menu (2.3); the same form opened two ways (2.5); drafts of a form, a row and a form test (4.1 to 4.3); the drift check (4.4); a dialog's buttons apart from the page's (1.1); what an unopened page waited for (1.5); the fields a save sends (1.8) | Own records per kind (1.2); several accounts in one walk (1.3); a dry run (1.4); the list as a file per page (1.6, 6.1); route guards (1.7); record texts kept out by rule (6.2); dialogs and tabs in the checklist (6.3); what changed since the last walk (6.4); how each record is deleted (5.1); the sweep (5.2); a value per kind of field (5.3); a page's cost (7.2); the environment's state (7.3); environment failures apart (7.4); the test site's pages for acc's faults (8.2) |

Two of the built ones are not proven: 1.1 passes its test with and without the fix, and 2.3 has no component named
in the config yet. The first loops prove or correct them.

## Safety

The standing rules hold in every loop, unchanged: only the acc hosts; no secret in a tracked file, a log or a report;
nothing written as Reader; every record a test makes carries the run marker and is deleted by the test; no send, mail
or payment; tests go through page objects and controls only; one UI test process at a time; no commit or push
without the owner's word.

## What is there, and what the factory still needs

| | |
| --- | --- |
| There | Every tool of stations 2, 3, 5 and 7, as `bruinet scrape places …`; the suite's `draft-templates.md` and sign tables; 59 of 418 places with a snapshot from a walk that was stopped |
| To build before loop 1 | The ledger (`190-Factory.md`) and the queue in it; nothing else |
| Not planned | A script that runs the loops unattended. The stations are run by the agent, which also does stations 4, 6 and 8, where judgment is needed |

## For the owner to decide

1. **The size of a group.** Proposed: at most six classes. Smaller is slower but safer.
2. **Commits.** Proposed: one commit per loop, on the owner's word once, for all loops after it. Until then nothing
   is committed.
3. **The order.** Proposed above. Another order is fine as long as the plain groups come first.
4. **Save tests in the loop.** Proposed: only where a way to delete is known; a form without one gets its read-only
   test and a line in the ledger.
5. **How long without a word.** Proposed: the factory reports in one line after every loop and goes on; it stops
   only in the five cases above.
