# Plan: page objects and a first set of tests for the whole of Bouw7

Status: **plan**, for discussion. Date: 2026-11-16.

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with.

| | |
| --- | --- |
| What | From four modules with one journey each to **every page the menu reaches**: a page object per page, and a first set of tests per page, green on acc |
| Builds on | `.my/Planning/bouw7-proposal.md` (the loop, the accounts, the guards), `Bouw7/.testing/` (structure, controls, friction, harvest) |
| Changes nothing in | The loop's stages and gates, the layout by module, the rules of `Bouw7/AGENTS.md` |
| The short version | First one test that opens **every** menu item (the whole app in a day, shallow). Then module by module through the existing loop, in four depths: list, detail, form, make. Modules that only read go first; anything that sends, pays or books waits for the owner |

## 1. Where we are

| Module | Page objects | Spec | Rules, green on acc |
| --- | --- | --- | --- |
| SignIn | 3 | B7.SIG.01 (a scenario, no spec) | 1 scenario |
| Contacts | `ContactPage`, `ContactCreatePage` | B7.CON.01 Find a contact | 8 |
| Projects | `ProjectPage`, `ProjectCreatePage` | B7.PRJ.01 Find a project | 15 with Hours |
| Hours | `HourLogPage`, `…ContactPage`, `…WeekViewPage`, `…ApprovalPage`, `HourEntryDialog` | B7.HRS.01 Log hours | (23 for the three) |
| Invoices | `InvoicesPage` and its 4 tabs; the two create pages as drafts only | B7.INV.01 See the invoices | 6 |

That is 4 of the menu's 9 modules, and about 9 of the app's 75 routes (section 2). Nothing is known yet about
Employees, Purchase, QSHE, Reports, Quotations and Schedule beyond their routes.

## 2. The map of the app

From `MenuProbe`, run on acc as Reader on 2026-11-16. **Bold** routes have a page object today.

| Menu | Item | Route |
| --- | --- | --- |
| Contacts | Overview | **`/contact#/`** |
| | Create contact | **`/contact#/create`** |
| Employees | Overview | `/employee` |
| | Create employee | `/employee#/create` |
| Invoices | Overview | **`/invoice`** |
| | Create invoice | `/invoice/create` (draft) |
| | Create collective invoice | `/invoice/create-collective` (draft) |
| | Checkout statement | `/checkout-document` |
| | Checkout statement contracted | `/progress-sheet` |
| Purchase | Purchase invoices | `/purchase-invoice` |
| | Purchase orders | `/contract-purchase-order` |
| | Subcontractor contracts | `/contract-subcontractor` |
| | Obligations | `/delivery-ticket` |
| | Contract terms | `/contract-term` |
| Projects | Overview | **`/project`** |
| | Create project | **`/project/create`** |
| | Booked hours of employees | **`/hour-log`** |
| | Booked hours of contacts | **`/hour-log#/contact`** |
| | Hours per week | **`/hour-log#/week-view`** |
| | Hour log approval | **`/hour-log#/approval`** |
| | Pay hours | `/hour-log#/pay-off` |
| QSHE | Activities | `/kam#/` |
| | Reports | `/kam#/qhse` |
| | Journals | `/kam#/journal-day` |
| | Inspections | `/kam#/inspection` |
| Reports | Financial | `/report/liquidity` |
| | Revenue | `/report/finance-report` |
| | Project costs | `/report/costs-report` |
| | Productivity | `/report/productivity` |
| | Projects | `/report/projects-report` |
| | Work in progress | `/report-project-progress` |
| Quotations | Overview | `/quotation` |
| | New quotation | `/quotation#/create` |
| | Overview (calculations) | `/calculation` |
| Schedule | Project planning | `/planning/projects` |
| | Employee planning (new) | `/planning/employee-overview` |
| | Employee planning | `/planning/employee` |
| | Absence | `/day-off` |
| | Absence per employee | `/day-off-per-employee` |
| | Capacity planning | `/report-capacity-planning` |

### The product flyout

The first button of the menu bar (it shows the product's name, "Construction") opens a flyout with
four panels. From `MenuProbe.ProductFlyout`, run on acc as all four accounts on 2026-11-16. Creator,
NormalUser and Admin see the same items, so "Creator" below is their organization, Daan & Dennis
Demolitions. The full map, with each page's title and heading: `Bouw7/.testing/170-Map.md`.

**Navigation** holds the top menu's items, with two groups the top menu does not show (Quotations:
Quotations and Calculations; Projects: Projects and Bookings). The accounts differ:

| Item | Reader | Creator |
| --- | --- | --- |
| Quotations: Calculations, Overview (`/calculation`) | yes | no |
| Schedule: Employee planning, new (`/planning/employee-overview`) | yes | no |
| Projects: Pay hours (`/hour-log#/pay-off`) | yes | no |
| Purchase: Approve purchase invoices (`/approval`) | no | yes |

So the app has **41** navigation items, 40 for Reader and 38 for Creator.

**My products** has one item, Accounting, a link to Exact Online (`start.exactonline.nl`): a
production host outside acc. Never opened; the session refuses it (`AllowedHosts`).

**Master data**, 14 items for Reader, 9 for Creator (marked C):

| Item | Route | | Item | Route |
| --- | --- | --- | --- | --- |
| Materials (C) | `/material-per-unit` | | Types of hour | `/hour-type` |
| Waste (C) | `/garbage-per-unit` | | Attributes | `/custom-attribute` |
| Equipment (C) | `/equipment` | | File types | `/project-file-type` |
| Objects (C) | `/property-asset-management` | | Departments (C) | `/department` |
| Cost codes (C) | `/security-code/security-code-chapter` | | Branches (C) | `/branch` |
| Mileage | `/mileage-registration` | | VAT rates | `/vat-tariff` |
| Categories (C) | `/project-category` | | Plan item color (C) | `/plan-item-color` |

**Settings**, 15 items and 5 connections for Reader, 12 items and no connections for Creator (C):

| Item | Route | | Item | Route |
| --- | --- | --- | --- | --- |
| My account (C) | `/my-account/#` | | Invoices (C) | `/my-account/#invoice` |
| My subscription | `/my-account/#my-subscription` | | Purchase (C) | `/my-account/#purchase` |
| Tariffs (C) | `/my-account/#tariffs` | | Approval (C) | `/my-account/#approval` |
| Quotations (C) | `/my-account/#quotation` | | Emails (C) | `/my-account/#mail-settings` |
| Schedule (C) | `/my-account/#planning` | | Notifications (C) | `/my-account/#notifications` |
| Projects (C) | `/my-account/#project` | | Background task scheduler | `/my-account/#background-tasks` |
| QSHE (C) | `/my-account/#qhse` | | Rights | `/right` |
| Quality assurance (C) | `/my-account/#quality-assurance` | | | |

Connections (Reader only): Basecone `/basecone`, IBIS `/ibis`, Mareon `/mareon`, Twinfield
`/twinfield`, Whitevision `/whitevision/settings`.

In all: **75 routes** on acc (41 navigation, 14 master data, 20 settings and connections), of
which Reader sees 74 and the other three accounts 59. What an account sees follows its
**organization**, not its role: Admin sees no more than Creator, and has no Rights page either.

Also not mapped yet: the **dashboard** (`/dashboard`), **profile settings**
(`/profile-settings`), what is behind the avatar (Explore never clicks it),
and every **detail page** (`/project/view?id=…`, a contact, an invoice, …) with its tabs. Opening
each of Reader's 74 routes once showed that all of them open, and what the app shows instead of a
page (404, 403, and a 500 on `/right` for Creator: `Bouw7/.testing/issues/001`).

## 3. What a page gets

"Test pages" here are page objects: `<Page>.scraped.cs` from the Scraper and `<Page>.cs`, the
person's part, verified on acc (`scrape verify`). A page's first set of tests comes in five depths.
Each depth is useful without the next, so the whole app gets depth 0 before any module gets depth 4.

| Depth | Name | What is checked | As | Writes |
| --- | --- | --- | --- | --- |
| 0 | **Opens** | The menu item opens its page: the route, the title, no error page | Reader | no |
| 1 | **List** | Title and buttons, columns, the count, an empty list (Creator) and a list with rows (Reader), each filter narrows | Creator, Reader | no |
| 2 | **Detail** | The first row opens its detail page; its tabs are there and each opens | Reader | no |
| 3 | **Form** | The create form opens, shows its fields, says which are required, and closes without saving | Creator | no |
| 4 | **Make** | One record made with the run marker, seen in the list, changed, deleted (`CleanUpRegistry`) | Creator | yes |

Depth 0 is **one data-driven test over the map of section 2**, not a test per page: `[Smoke]`,
`[Live]`, a row per menu item, `[Module]` from the row. Depths 1 to 4 are specs, one journey each,
run from the spec as B7.CON.01 is (AD-041), with a C# test only where a phrase does not bind.

**Not in the first set**, anywhere: send (mail leaves acc), pay, export, the link with Exact,
booking or finalising (an invoice that gets a number cannot be deleted), approving and paying hours,
and whether a report's numbers are right. Each is named in its spec's "Not in this journey".

## 4. Modules, ids and journeys

Folders are named as the English menu (`150-Structure.md`, rule 4). Hours stays a module of its
own though the menu lists it under Projects (decision 003). New ids:

| Module | Id | First journeys (`.01` is depth 1, then as the pages allow) |
| --- | --- | --- |
| Contacts | B7.CON | **.02 Make a contact** (depths 3, 4), .03 A contact's page (depth 2) |
| Projects | B7.PRJ | **.02 Make a project**, .03 A project's page and its tabs |
| Hours | B7.HRS | .02 Hours of contacts, .03 Hours per week, .04 The approval list (read only), .05 Pay hours (read only) |
| Invoices | B7.INV | .02 The other tabs, .03 The invoice form (depth 3; a concept at most), .04 Checkout statements (two pages) |
| Employees | B7.EMP | .01 Find an employee, .02 An employee's page, .03 The employee form (depth 3 only, see Q2) |
| Quotations | B7.QUO | .01 Find a quotation, .02 Make a quotation (never sent), .03 Calculations |
| Purchase | B7.PUR | .01 to .05, one per list (invoices, orders, subcontractor contracts, obligations, contract terms), depth 1; depth 3 where a form exists |
| Schedule | B7.SCH | .01 Absence, .02 Absence per employee, .03 Project planning, .04 Employee planning (old and new), .05 Capacity planning |
| QSHE | B7.QSH | .01 Activities, .02 Reports, .03 Journals, .04 Inspections |
| Reports | B7.REP | .01 to .06, one per report: opens, its filters, a table or chart is drawn |
| Master data | B7.MST | .01 The lists at a glance (one rule per list: title, columns, has rows), then a journey per list that a form of another module reads from: types of hour, categories, cost codes, materials |
| Settings | B7.SET | .01 Each settings page opens and shows its fields, read only (Reader). Nothing is saved: a setting changes the whole organization. Rights (`/right`): read only. Connections: depth 0 only |
| Dashboard | B7.DSH | .01 What NormalUser lands on |
| Across modules | in the module whose result is checked | A contact → its project → hours on it → the project to be invoiced on Invoices (depth 4, last) |

About 42 journeys. At B7.INV.01's size (6 to 8 rules) that is 250 to 300 rules; the number is a
guess until each module is explored.

## 5. Order

| Wave | Scope | Why here | Done when |
| --- | --- | --- | --- |
| **0 The whole menu** | The map as a file, `scraper.config.md` rows for every route, the depth 0 test, one Explore per unexplored module | Shows at once which pages open, which controls are new, and what each module will cost | 74 of Reader's 74 routes open, green 3×, and Creator's `/approval`; an exploration per module |
| **1 Deepen the four** | CON.02, PRJ.02, then the rest of Contacts, Projects, Hours, Invoices | The pages and controls exist. CON.02 is the write journey the harvest asked for as its fair second measure | Each journey's every rule passing, 3× |
| **2 Lists** | Master data, Employees, Quotations, Purchase | Expected to be Heimdall or `b7-data-table` lists: mostly reuse. Master data first: small lists, and the other modules' forms pick from them | The same |
| **3 Boards** | Schedule, QSHE | New kinds of page (planning boards, calendars, journals): new controls first | The same |
| **4 Reports** | Reports, Dashboard, Settings (read only) | Charts and totals; read only; what to assert needs the owner (Q5) | The same |
| **5 Across** | One journey through Contacts, Projects, Hours and Invoices | Needs waves 1 and 2 | Green 3×, and acc left as it was found |

Waves 2 to 4 can change order once wave 0 shows what each module is made of. A module is not
started while the one before it has open friction (the proposal's stage 9).

## 6. How a module goes

The existing loop, unchanged: `pwsh test-tooling/tools/Scripts/site-loop.ps1 -Suite Bouw7 -Module <M> -Runs 3`,
stages 0 to 9, state in `100-Loops.md`. What this plan adds:

1. **A row per journey, not per module**, in `100-Loops.md` (a module now has several specs).
2. **Explore per module** with `--within` its routes, as Creator (decision 004); and once as
   Reader's saved sign-in for the detail pages, read only, because Creator's organization is empty
   for most modules.
3. **Specs per module folder**, `Bouw7/specs/<Module>/<id>.spec.md`, as rule 8 of
   `150-Structure.md` already says; the four flat specs move with the first new one.
4. **The 👤 points stay**: a new kind of control, and each spec's `Status`. With some 35 specs the
   owner accepts per module, not per spec (Q1).

## 7. Data and guards

| Rule | How |
| --- | --- |
| Exact counts and empty lists | Only in Creator's organization, which only the tests change |
| "Has rows" and detail pages | Reader, on the demo organization; shape only, never names or counts |
| Everything made | Starts with the run marker, registered with `CleanUpRegistry`, removed at the end; leftovers of older runs removed by marker |
| A record that cannot be deleted | Not made. The journey stops at depth 3, and the spec says why |
| Seed data in Creator's organization | Detail and filter rules need a few records there (an employee, a quotation, a purchase order). Made once by a seed test with a fixed name, not the run marker, and listed in `300-Environment.md` (Q3) |
| The Exact Online link (My products) and the Connections pages | Never opened beyond depth 0; Exact is not acc and is refused |
| Rights | One journey per module ends with a rule as NormalUser or Reader ("does not see + …") where the roles differ; found in step 1 |

## 8. Tooling this will ask for

Guesses, to confirm in wave 0; each becomes a friction row when it bites.

| Need | Where | For |
| --- | --- | --- |
| Tab states in the Scraper (open since phase 1) | Bruinet | Every detail page |
| A date picker and a date range | `Bruinet.Web.Vuetify2` | Hours, Schedule, Reports |
| `VRadioGroup`, a file upload, `CkEditor` | `Bruinet.Web.Vuetify2`, suite `Controls/Text` | Forms |
| `B7DialogManager` finished | suite `Controls/Display` | Confirm and delete dialogs |
| A planning board (rows, bars on days) | suite `Components/Collection` | Schedule |
| A chart read as "drawn, with N series" | to decide | Reports, Dashboard |
| Service clients (B18, designed) | Bruinet, `Bouw7.Api` | Seed and clean-up without 20 s of UI per record |
| The menu's map as data | suite `Support/` | Depth 0, and a check that the menu has not changed |

## 9. Steps

Small, each with its check first.

- [x] 1. `MenuProbe` as Reader, Creator, NormalUser and Admin, extended to the product flyout's four panels → `Bouw7/.testing/170-Map.md`. *Done 2026-11-16: 76 routes (the dashboard included), who sees each, its title and heading.*
- [x] 2. Module ids and routes of section 4 into `150-Structure.md`; `## Modules` rows in `scraper.config.md`. *Done: `bruinet scrape status` parses.*
- [x] 3. The depth 0 test. It became `<Module>OpensTests` in each module (13 classes over the rows of `Components/Navigation/AppMap.cs`), not one class in `Support/`: `Support/` holds no tests of Bouw7, and a class has one `[Module]`. *Done: red for a planted wrong route, then 76 of 76 green 3×.*
- [ ] 4. Explore the six new modules, the dashboard, and the detail pages. *Check: every route of the map has a capture; guards held.*
- [ ] 5. Patterns over the new explorations → `160-Controls.md`: made, to make, not a control. 👤 accepts the new kinds.
- [ ] 6. This plan's waves into `200-Tasklist.md` as phase 7, a line per journey; section 5's order corrected by what step 5 found.
- [ ] 7. Wave 1, starting with **B7.CON.02 Make a contact**, measured against the harvest's table.
- [ ] 8. Waves 2 to 5, a module at a time, each ending with its friction fixed.
- [ ] 9. A coverage page per run: map × depth × state (`lirenet trace --results`), in the morning report.

**Done** when every menu item opens (depth 0), every journey of section 4 that the owner kept has
every rule reached by a passing test 3 runs in a row, and what is left out is written in a spec's
"Not in this journey" with its reason.

## 10. Questions for the owner

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Accept specs per module or per spec? | Per module, as delegated before |
| Q2 | May a test **make an employee**? It may send an invitation or use a licence | No: depth 3 only, until answered. **Answered 2026-11-17: yes, a test may write and delete after (decision 005)** |
| Q3 | May Creator's organization hold fixed seed data? | Yes, a handful, listed in `300-Environment.md` |
| Q4 | Purchase invoices, quotations, checkout statements: can a concept be deleted, and does saving one mail or book anything? | Depth 3 until each is checked by hand once. **Answered 2026-11-17: yes, a test may write and delete after (decision 005)** |
| Q5 | Reports: is "opens, filters work, something is drawn" enough, or must numbers be checked against seed data? | The first, for the first set |
| Q6 | Which modules matter most? The order of waves 2 to 4 is ours | Employees and Quotations before Purchase |
| Q7 | Settings: is "opens and shows its fields" enough, with nothing ever saved? And may a test add a row to a Master data list (a category, a type of hour) in Creator's organization? | Yes to both; master data rows with the run marker, deleted after. **Answered 2026-11-17: yes, a test may write and delete after (decision 005)** |
| Q9 | Daan & Dennis Demolitions, where the tests write, lacks 16 items Reader's organization has (Calculations, Pay hours, new Employee planning, Types of hour, VAT rates, Mileage, Attributes, File types, Rights, My subscription, the scheduler, the connections). Admin there sees the same as Creator. Can that organization get them? | Ask for the modules and rights on acc; until then those pages get depths 0 to 2 as Reader only |
| Q8 | The two Employee planning pages: is the old one going away? | Depth 0 for the old, journeys for the new |
