# Proposal: Bouw7 as Bruinet's first real customer site

Status: **proposal**, for discussion. Date: 2026-11-14 (fifth version, same day).

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with.

| | |
| --- | --- |
| Where it lives | The container repository [jonkeda/BruinetB7](https://github.com/jonkeda/BruinetB7) (private), checked out at `E:\repos\HtmlConstruction`, with the suite in its child folder `Bouw7/` |
| Bruinet | The submodule `test-tooling/` from the public mirror [jonkeda/bruinet](https://github.com/jonkeda/bruinet), branch **`move`**, referenced as source projects (`Bouw7/.testing/decisions/001`) |
| What goes into Bruinet | Only generic mechanisms. Vuetify 2 and Bouw7 code stay in BruinetB7 |
| Changes in this version | `.testing/` holds numbered documents (`100-Loops.md`, …) and a `decisions/` folder of numbered decisions (section 4.3). The accounts file is `Bouw7/TestAccounts.md`, with its secrets in `TestAccounts.local.md` (section 8.1). The Bruinet submodule comes from the `jonkeda/bruinet` mirror |

## 1. In one paragraph

We use the Bouw7 acceptance environment (`https://login-acc.bouw7.nl/`, app at
`start-acc.bouw7.nl`) for three things at once. First, a real UI test suite for the Bouw7 web app
**as it is today**. Second, a **learning site** that tells us, with evidence, what Bruinet has to
change so the next site like it costs a fraction of the effort. Third, later, an end-to-end suite
where the **converted MAUI phone app** creates something and the web back office must show it.
Agents do as much of the work as possible, driven by an orchestrator: specs, exploring, controls,
page objects, scenarios, runs and triage. People set the guards, accept specs and controls, and
judge the results.

## 2. Goals

| # | Goal | Done when |
| --- | --- | --- |
| G1 | **Test Bouw7 web** with Bruinet | Smoke tests for hours, projects and contacts, plus a few flows, green on acc, run locally from the command line |
| G2 | **Learn from it** | A friction log (section 9) with every place the work was slow, manual or wrong, each turned into a Bruinet backlog item or a "won't fix" |
| G3 | **Page objects and control objects, scraped** | `Bruinet.Web.Vuetify2` (generic), Bouw7's own controls, and page objects for every page a test touches: drafted by the Scraper, finished by the agent, verified against acc |
| G4 | **AI-driven** | The orchestrator (section 6) takes a module from spec to green tests; a person is needed only at the checkpoints marked 👤 |
| G5 | **Phone → back office E2E** (later) | Once the converted MAUI app is ready: one journey created on the phone and checked in the web app, in one Markdown scenario |

Not goals: load tests, production, the native Android or iOS apps, or replacing Bouw7's own unit
tests.

## 3. What we already have (the "half of the technology")

On Bruinet's `move` branch:

| Need | In Bruinet today | Gap for Bouw7 |
| --- | --- | --- |
| Drive a web app | `Bruinet.Html` + `Bruinet.Html.Playwright`, page objects, ControlObjects, `TabContainerControl` | Tabs whose content is a page of its own (B17) |
| Scrape pages | Scraper: Record, **Explore** (guarded, breadth first), patterns, Draft, Verify; IDE view and MCP tools (AD-027) | Trialled on Bouw7 in October. The view feedback is fixed but not retried in VS. The agent stage never ran with Claude. Pages are keyed by URL, so tab switches go unnoticed (B17) |
| Make controls | Scraper patterns + agent through MCP; `html-control`, `convert-control` skills | The IDEs do not send the prompt (clipboard / unsent chat). 7 Bouw7 drafts in `Scraper.Sites/Bruinet.Html.Bouw7` |
| Readable scenarios | `*.feature.md`, UAT runtime, phrases from attributes | No Bouw7 phrases yet |
| Several apps in one scenario | **Mixed head**, `Apps` table with a `User` column, `{user}` subjects (AD-015), Todo's `Mixed.Uat` | A user is a name only: it has no account, password or organization (section 8.1) |
| Accounts | `LocalAccountStore` in `Bruinet.Contracts` reads `<PREFIX>_<NAME>_USERNAME/_PASSWORD/_ORGANIZATION` from a git-ignored `.env.local`; Construction has named accounts `admin`, `user`, `reader`, `create` | Lives in the contract-test package, not where UI tests and BDD look; no link to scenario subjects; no sign-in cache (section 8.1) |
| Phone app (later) | MAUI heads (FlaUI, Appium, Headless, DevFlow); Construction's test host with auth, fixtures and page objects | Construction's suite runs against WireMock; acc runs use DevFlow/CDP sign-in |
| Test data | `Bruinet.TestData` typed sets | No create/clean-up against a shared acc tenant (B7, B18) |
| Agents | CLI, MCP server, skills, `scrape-loop.ps1` (explore → draft → agent → verify) | No orchestrator above one stage; no spec, scenario or triage stages |
| Specs | Frainet: spec format, `check`, `trace` (section 7) | Used only on Todo's TOD.02 |

What the October trial showed about the site (from the recorded patterns): Vue 2 with **Vuetify 2**
(`VBtn`, `VInputSelect`, `VCard`, `VDataFooter`), Bouw7's own **web components**
(`b7-navbar-button`, "hydrated" classes, so likely Stencil, maybe shadow DOM), data rows of their
own (`HeimdallDataRow`), a frame layout (`#frame-content`, `#frame-logo`), many **tabbed pages**,
and sign-in on a separate host.

**Confirmed in phase 0** (2026-11-14, `Bouw7/.testing/300-Environment.md`): Vue 2 with Vuetify 2;
the `b7-*` components, the top navigation included, live in **shadow roots**; test ids are
**`data-cy`**, not `data-testid`; pages have Vuetify tabs; no frames on the start page; no Vuetify 3
seen. The sign-in has a third step, an organization picker. acc opens on Invoices, not a dashboard.

## 4. Layout

### 4.1 What goes where

| Kind of code | Where | Why |
| --- | --- | --- |
| Generic mechanisms: detached popups, shadow DOM, tab pages, waits, accounts, Scraper sources, orchestrator, generic skills | Bruinet repo | Every site needs them, whatever its UI kit |
| **Vuetify 2 controls and helpers** (`VBtn`, `VSelect`, `VDataTable`, `VTabs`, menus, dialogs, snackbars, waits for Vuetify's transitions) | `Bouw7/Bruinet.Web.Vuetify2/` | Not part of Bruinet. Generic for any Vuetify 2 site, so it has **no Bouw7 reference** and can move out of `Bouw7/` when a second project needs it |
| Vuetify 3, when it is needed | `Bruinet.Web.Vuetify3/`, the same shape | Vuetify 3's DOM differs (`v-field`, overlays); one project per major version, never mixed |
| Bouw7's own controls (`b7-*` web components, Heimdall data rows, frame layout) | `Bouw7.Web.UITests/Controls/` and `Components/`, or a module's own (decision 003) | Bouw7 only. A `Bouw7.Web.Controls` project only once something outside the test project needs them |
| Page objects, tests, scenarios, specs | `Bouw7/…` | Bouw7 only |

### 4.2 The tree

```text
BruinetB7/                                 container repository (github.com/jonkeda/BruinetB7)
  README.md                                what the container holds, how to clone and build
  AGENTS.md                                rules for agents in every child
  .gitignore                               Recordings/, TestAccounts.local.md, .testing/runs/, TestResults/, .scraper-state.json
  .my/                                     planning, such as this proposal
  .gitmodules                              test-tooling from github.com/jonkeda/bruinet, branch move
  test-tooling/                            Bruinet (submodule, Q12)
  Bouw7/                                   the Bouw7 suite (child)
    Bouw7.sln
    AGENTS.md                              Bouw7's rules: acc only, Dutch UI, accounts, data marker
    eng/bruinet/Bruinet.references.props   project references to test-tooling/src, as in Construction
    TestAccounts.md                        the accounts: names, roles, organizations, writes (section 8.1)
    TestAccounts.local.md                  their usernames and passwords; git-ignored
    .testing/                              the documents about the testing (section 4.3)
    specs/                                 Frainet specs per journey: B7.HRS.01.spec.md, …
    Bruinet.Web.Vuetify2/                  generic Vuetify 2 ControlObjects and helpers
      .claude/skills/vuetify2-control/     the skill that grows it (section 6.1)
    Bruinet.Web.Vuetify2.Tests/            its unit tests against a static Vuetify 2 page
    Bouw7.Web.UITests/                     grouped by module (Bouw7/.testing/150-Structure.md)
      Controls/  Components/               Bouw7 parts about no module's subject
      Modules/<Module>/                    Controls/ Components/ Pages/ Tests/ Features/ per module
      Support/                             accounts, sign-in, probes
    Bouw7.Api/                             after B18's design: set-up, clean-up and back-end checks
    Bouw7.E2E.Uat/                         later: mixed-head UAT with apps.md (Phone, Office)
```

The 7 draft controls in `Scraper.Sites/Bruinet.Html.Bouw7` leave the Bruinet repo. The `V*` ones
(`VBtn`, `VInputSelect`, `VInputAppendInner`, `VSelectSlot`) go to `Bruinet.Web.Vuetify2`, the rest
(`B7DialogManager`, `B7NavbarButton`, `TooltipHintWrapper`) to `Bouw7.Web.UITests/Controls/`.
`Scraper.Sites/Bruinet.Html.Bouw7` is then removed.

### 4.3 `Bouw7/.testing/`: the testing documents

One place for everything about the testing that is not code, readable by people and by the
agents. Tracked in git, except `runs/`.

**Numbered documents.** Each document has a number in hundreds and a name, so the folder reads in
order and a new document fits between two others (`150-…` between `100-` and `200-`):

| File | What it holds | Kept by |
| --- | --- | --- |
| `000-Readme.md` | What is tested, on which environment, how to run it locally, and an index of this folder | person |
| `100-Loops.md` | The orchestrator's state per module: stage, state, what it waits for (section 6.2) | orchestrator; a person ticks 👤 items |
| `200-Tasklist.md` | The work, as checkboxes per phase and module (example below) | person and agents |
| `300-Environment.md` | acc's hosts and URLs, what may and may not be touched, the test data rules and the run marker | person |
| `400-FrictionLog.md` | What was slow, manual or wrong, and the Bruinet change it asks for (section 9) | agents and person |

**Folders**, each with numbered files of their own:

| Folder | What it holds | Kept by |
| --- | --- | --- |
| `decisions/` | One file per Bouw7-only decision: `001-Modules-first.md`, `002-Accounts-per-module.md`, … Each says what was decided, why, and what was ruled out, in the shape of Bruinet's decision records | person; an agent may propose one |
| `issues/` | One file per app bug found: `001-<short title>.md`, with the failing scenario and the evidence | agent (`run-triage`), checked by a person |
| `runs/` | The orchestrator's reports per run (`<date>/report.md`). **Git-ignored** | orchestrator |

The accounts are not here: `TestAccounts.md` sits in the root of `Bouw7/`, next to the code that
reads it (section 8.1).

**Other documents?** Not now. Specs stay in `Bouw7/specs/` (Frainet reads them there), and
Bruinet's guides stay in Bruinet. A glossary of Bouw7's Dutch terms (`500-Glossary.md`) is the
first candidate if the agents keep getting words wrong; the friction log will tell.

`200-Tasklist.md` uses one checkbox per task, grouped by phase, with the owner and a link to the
evidence when done:

```markdown
## Phase 0 Setup

- [x] BruinetB7 reachable and checked out at E:\repos\HtmlConstruction
- [x] Bruinet as a submodule from jonkeda/bruinet, branch move
- [ ] `Bouw7/` solution with references to Bruinet
- [ ] `TestAccounts.md` and `TestAccounts.local.md` for NormalUser, Admin, Reader, Creator
- [ ] A web test opens the dashboard signed in as Reader

## Phase 2 Controls

- [ ] Hours: explore
- [ ] Hours: controls accepted 👤
```

The orchestrator does not tick boxes in `200-Tasklist.md`; it keeps `100-Loops.md`. The task list
is the plan for people, and `100-Loops.md` is the loop's state.

## 5. The AI-driven loop, per module

Everything deterministic stays in Bruinet and Frainet (model-free, per AD-027). The agent does the
judgement, one stage at a time. Each stage has an exact gate that something other than the agent
checks.

| # | Stage | Who | Gate (checked by the orchestrator, not the agent) |
| --- | --- | --- | --- |
| 0 | Guards | 👤 | `scraper.config.md`: acc hosts only, Never visit, Dutch and English danger words, `ClickPolicy safe`, masks |
| 1 | Spec | agent (`spec-from-app`) | `frainet check` passes; 👤 sets the spec to `accepted` |
| 2 | Explore | Scraper | Pages and tab states captured for the journey; guards held |
| 3 | Controls | agent (`vuetify2-control`, `html-control`) | Build + control unit tests; 👤 accepts each new control kind (once, then reused) |
| 4 | Page objects | Scraper drafts, agent (`scrape-pages`) names and groups | Build + `scrape verify`: every member found once on acc |
| 5 | Scenarios | agent (`scenario-from-spec`) | Every step binds; `frainet trace` reaches every rule |
| 6 | Run | `dotnet test` / UAT runner | Green 3 runs in a row, or each failure classified |
| 7 | Triage | agent (`run-triage`) | Test bug: fixed and back to 6. Site changed: back to 2. App bug: stays a failure (AD-009), written up for 👤 |
| 8 | Learn | agent (`friction-log`) | A friction-log row per workaround |
| 9 | Fix friction | agent, Bruinet or the suite | Each friction item of the module fixed with a test before the next module starts, or closed as won't-fix by 👤 |

Rules for the loop:

- **acc only.** A run aborts on any host not in the config.
- **Explore never types and never signs in.** The person signs in once in the Scraper's profile,
  as `Reader`.
- **Tests may change data, Explore may not.** Tests create only data with the run's marker
  (section 8.3), and only as an account whose `Writes` is `yes`.
- **The build, Verify, Frainet and green runs are the judges**, not the agent's claim.
- **Re-scrape on drift.** Each scraped file carries its page hash; a Verify that fails sends the
  module back to stage 2, and the agent's re-draft is a diff a person can review.

## 6. Skills and the orchestrator

### 6.1 Skills

Each skill does one stage, takes its input from files and leaves its output in files, so a run
can stop and resume anywhere. Generic skills live in Bruinet (`.github/skills/`, mirrored in
`.claude/skills/`, as today); a library's skill lives with the library.

| Skill | New or existing | Home | Input → output |
| --- | --- | --- | --- |
| `site-setup` | new | Bruinet | URL, platform, accounts → a child folder: solution, references, `scraper.config.md` with careful guards, sign-in fixture, `AGENTS.md`, `TestAccounts.md`, and `.testing/` with its numbered documents and `decisions/`. The skill behind B11 |
| `spec-from-app` | new | Bruinet (with Frainet) | Journey name, recordings, the running app → `specs/<id>.spec.md` in Frainet's format, `draft`, with open questions and proposals for the owner |
| `scrape-pages` | existing, extended | Bruinet | Recordings → page objects. Extended: match patterns to the referenced control libraries first (B2), and draft tab pages (B17) |
| `vuetify2-control` | new | `Bruinet.Web.Vuetify2` | A pattern with Vuetify 2 classes → a control in the library, with a unit test. Holds Vuetify 2's DOM knowledge: detached menus, `v-select`, `v-data-table`, `v-tabs`, `v-dialog`, transitions. A `vuetify3-control` beside `Bruinet.Web.Vuetify3` follows the same shape (Q9) |
| `html-control` | existing | Bruinet | A site-specific pattern → a control in `Bouw7.Web.UITests/Controls/`, or in its module's `Controls/` |
| `scenario-from-spec` | new | Bruinet (extends `bruinet-write-scenario`) | Accepted spec → `*.feature.md`: one scenario per example, tagged `@<rule id>`, subjects from `TestAccounts.md`, phrases bound to page objects |
| `run-triage` | new | Bruinet | Test results and artifacts (screenshots, step logs, traces) → a classification per failure, a fix for test bugs, an issue file for app bugs |
| `friction-log` | new | Bruinet | What the stage had to work around → a row in `.testing/400-FrictionLog.md`; at harvest time, rows grouped into Bruinet backlog items |

### 6.2 The orchestrator

**A script, not a model.** The orchestrator decides nothing a check can decide. It reads the
module's state, picks the next stage, starts the agent with only that stage's skill and tools,
runs the stage's gate itself, and writes the result back. This keeps the factory's rules in one
place and every decision reproducible, in line with AD-027 and Frainet's idea that a loop runs
dark only when its feedback is an exact check.

```text
bruinet loop run --suite Bouw7 [--module hours] [--until scenarios] [--agent claude]

  for each module in .testing/100-Loops.md that is not done or waiting:
    stage = first stage whose gate does not pass
    if stage needs 👤 → mark "waiting: <what>" and go to the next module
    run the agent:  claude -p "<stage prompt>" with the stage's skill, the Bruinet MCP tools,
                    a time limit and a token budget
    run the gate:   build / scrape verify / frainet check|trace / dotnet test
    pass  → next stage
    fail  → retry up to N times with the gate's output as feedback, then "stuck: <why>"
  write .testing/runs/<date>/report.md: per module the stage, gate results, retries, cost, 👤 waits
```

`Bouw7/.testing/100-Loops.md` is the state, readable and editable by a person:

| Module | Spec | Stage | State | Waiting for |
| --- | --- | --- | --- | --- |
| Hours | B7.HRS.01 | 3 Controls | waiting | 👤 accept `VDataTable` |
| Projects | B7.PRJ.01 | 1 Spec | waiting | 👤 accept spec (2 open questions) |
| Contacts | B7.CON.01 | 6 Run | stuck | Verify: `ContactDetail.Tabs` not found (B17) |

Decisions in the orchestrator's design:

| Decision | Choice | Why |
| --- | --- | --- |
| Driver | A `bruinet loop` command (first as `tools/Scripts/site-loop.ps1`, growing out of `scrape-loop.ps1`) | Deterministic and testable without tokens |
| Agent | Claude Code headless (`claude -p`), Copilot CLI as an option | `scrape-loop.ps1` already supports both |
| One stage per agent run | Yes, fresh context each time | Small prompts, clear gates, resumable, cheap retries |
| 👤 checkpoints | A line in `.testing/100-Loops.md` and the report. A person ticks it, or edits the spec's `Status` | Local first (Q7). Later each module can be a branch and the checkpoint a PR review |
| Budgets | Per stage: attempts, minutes, tokens; per run: total cost | An unattended loop must stop on its own |
| Where it runs | The developer's machine, at night or on demand | Q7: local first; CI later |
| Done | A module is done when its accepted spec's every rule is reached by a passing test (Frainet, F1) | One exact definition of done |

## 7. Frainet: improvement ideas

Frainet today: a spec format (intent, rules with ids, examples per rule, choices, not in this
journey, open questions), `frainet check` (25 kinds of finding, no model) and `frainet trace`
(rule ids found in `[Trait("Rule", …)]` and `@<rule>` tags, read as text). It has one spec, Todo's
TOD.02. Bouw7 is a good second product: real, not ours, with several modules and two clients.

| # | Idea | Why, for Bouw7 and in general |
| --- | --- | --- |
| F1 | **Trace with results.** Read Bruinet's result JSON and the test results, and give each rule a state: not reached, reached but not run, failing, passing | "Named by a test" is not "proven". The orchestrator's definition of done needs passing tests |
| F2 | **Spec examples ⇄ scenarios.** Check that each example in a spec has a scenario tagged with its rule whose steps match the example, and report drift both ways | The examples are already Given/When/Then in the screen's words; they drift from the feature files today without anyone noticing |
| F3 | **Reverse specs from a running app** (`spec-from-app`): an agent drafts a spec from recordings and the app's behaviour, writing every unclear point as an open question with a proposal | Bouw7 has no specs; the owner should judge a draft, not write from nothing |
| F4 | **"Who" in a spec.** Rules can name a role ("a normal user", "an administrator"), mapped to accounts in the suite, never to credentials. Trace checks that each role-bound rule runs as that role | Rights differ per role; today a spec cannot say so exactly |
| F5 | **"Where" checked.** The `Where` field (web, phone) becomes exact, and trace checks each rule is proven on every place it names | The E2E step and the converted app later: a rule may hold on the web and not yet on the phone |
| F6 | **Cross-app journeys.** A rule may span two places with a time limit ("made on the phone, seen in the office within 60 seconds") | Bouw7's phone → back office journeys |
| F7 | **A model-based review, kept apart.** An optional `frainet review` skill finds ambiguity, missing edge cases and contradictions, and writes them as open questions; the exact `check` stays model-free | The plan already asks this; Bouw7 will show what the exact checks miss |
| F8 | **Products and ids.** Several products in one repo, an id scheme per product (`B7.HRS.01.R3`), a product index with the status of each journey | Bouw7 has many modules; Todo had one list |
| F9 | **A coverage report per product**: rules × state (F1) × place (F5), as Markdown for the morning report and JSON for tools | One view for the owner and for the orchestrator |
| F10 | **A dotnet tool and editor support.** Ship `frainet` as a tool so BruinetB7 uses it without Bruinet's source; in Bruinet's language server, hover a rule id to see its tests and state, go from a rule to its scenarios | Frainet must be usable outside the Bruinet repository |

## 8. The hard parts

### 8.1 Accounts: a common way in Bruinet

**What exists:** `LocalAccountStore` (in `Bruinet.Contracts`) reads named accounts from a
git-ignored `.env.local`; Construction's live contract tests name an account per test; the `Apps`
table has a `User` column that is only a name. There is no single place where a scenario's
subject, a test's account, its credentials, its organization and its rights meet.

**Proposal (B14):** one account model in Bruinet, used by UI tests, UAT/BDD, contract tests and the
Scraper alike.

1. **`TestAccounts.md` in the suite's root, tracked, no secrets.** A Markdown table like `apps.md`:

   | Name | Role | Organization | Writes | Environments | Notes |
   | --- | --- | --- | --- | --- | --- |
   | NormalUser | user | Daan & Dennis Demolitions | yes | acc | rights checks with Admin |
   | Admin | admin | Daan & Dennis Demolitions | yes | acc | full rights |
   | Reader | user | (demo organization) | no | acc | read-only; Explore signs in as this |
   | Creator | user | (demo organization) | yes | acc | creates data in live create tests |

   The **Name** is what a scenario and a test say. More accounts are added as rows (Q6: "possibly
   more").

2. **Secrets from one git-ignored file, by name.** `TestAccounts.local.md`, next to it, has the same
   names with their usernames and passwords (and an organization, to override the table):

   ```markdown
   | Name | Username | Password | Organization |
   | --- | --- | --- | --- |
   | NormalUser | … | … | |
   ```

   No environment variables and no user secrets: one file, so it is always clear where a value
   comes from. Its path is a setting, so several suites can share one file. Changing a password or
   swapping the person behind `NormalUser` is one edit in that file; no test or scenario changes.
   On a CI machine later, the pipeline writes the file from its secret store before the run.
   Construction's `.env.local` moves to this format; until then `LocalAccountStore` can read it.

3. **BDD names the account.** A subject that is an account name resolves through `TestAccounts.md`:

   ```gherkin
   Given NormalUser is signed in
   When NormalUser opens the Hours page
   Then NormalUser should not see "Approve"
   ```

   In a mixed suite the `Apps` table's `User` column names an account (`Phone | NormalUser`), so
   AD-015's rules (the subject picks the app) hold unchanged.

4. **C# tests name it too:** `[Account("Admin")]`. A test or scenario whose account is not in
   `TestAccounts.md`, or has no username or password in `TestAccounts.local.md`, **fails** with the
   reason ("Account NormalUser has no password in TestAccounts.local.md"). A missing account is a broken
   set-up, and a skip would hide it.

5. **Sign-in once per account per run.** The suite gives one `ISignIn` per head (web: the login
   page object, then Playwright's storage state; MAUI: the app's own flow). Bruinet caches the
   signed-in state per account and switches only when a test needs another account.

6. **Rights are enforced where Bruinet can see them.** An account with `Writes = no` runs with the
   request guard the Scraper already has (`BlockMethods`): a POST, PUT or DELETE fails the test
   with "Reader may not write".

7. **`bruinet accounts check`** lists each account (names, never values), which credentials are
   missing, and whether each can sign in. It runs first in the orchestrator, and stops the run when
   an account a module needs is missing.

It lives in **`Bruinet.Core`** (Q11), next to `TestAppSet`, recorded as a decision (AD).
`LocalAccountStore` moves there from `Bruinet.Contracts`.

For Bouw7, the existing Construction accounts map one to one: `user` → NormalUser, `admin` →
Admin, `reader` → Reader, `create` → Creator. Q6: `admin` and `user` belong to their own
organization, and tests may create data there.

### 8.2 Tabbed pages (B17)

Bouw7 uses tabbed pages a lot: a project has tabs for details, hours, materials and so on. Before,
the tools did not always notice a tab switch. The Scraper keys a page by its URL, so a tab that
changes no URL is no new page, and a route change without a page load was sometimes missed.
Meanwhile each tab's content is as big as a page.

Proposal:

- **A tab's content is a page object of its own** (or a component), scoped to its tab panel. The
  host page has a `Tabs` control that opens one and returns it:
  `ProjectPage.Tabs.Open<ProjectHoursTab>()`. It builds on `TabContainerControl`, and has MAUI's
  shape (AD-013), so the converted app's `TabbedPage` or Shell tabs read the same in scenarios.
- **Switching is a state change that is waited for** (AD-004): the tab is selected
  (`aria-selected`, Vuetify's `v-tab--active`), the panel is shown, and its content has loaded.
  "Open" returns only then.
- **The Scraper knows tab states.** A capture's key is the page plus its active tab
  (`/projects/42#tab=hours`). Explore clicks every tab (they are safe), Record by hand notices a
  switch and SPA navigation (`pushState`, `hashchange`, a changed `aria-selected`), and Draft writes
  one page object per tab panel and the host page with its `Tabs`.
- **Scenarios** can say `Given NormalUser is on the Hours tab of the Project page`, or name a tab
  page directly.
- `Bruinet.Web.Vuetify2` gets `VTabs` on top of this; the generic part is in Bruinet.

### 8.3 Test data on a shared tenant

acc holds realistic contacts and projects, and other people use it.

- Data is created only by accounts with `Writes = yes`. `Reader` never writes (enforced, 8.1).
- Read checks look at the shape of the data, not at exact counts or names, because the demo data
  changes.
- Every run gets a marker (`{run}`, e.g. `BRN-1114-0932`) that every created name starts with.
- Set-up and clean-up go through the UI until B18 is designed and built (section 8.5), then
  through the API where possible. Clean-up also
  removes leftovers of older runs by marker.
- Recordings hold realistic data: masks on, and `Recordings/` stays out of git.

### 8.4 Phone → back office (later)

When the converted MAUI app is ready (Q5), the E2E suite reuses everything above:

```gherkin
@mixed
Scenario: An hour entry made on the phone shows in the back office
  Given Creator uses the Phone app
  When Creator adds an hour entry of 2 hours on project "{run} Test project"
  Then Creator should see an hour entry of 2 hours on "{run} Test project" in the Office app within 60 seconds
```

- `apps.md` with **Phone** (MAUI head; Windows first for speed, then Android) and **Office** (web
  head), both used by `Creator`, so the record lands in the organization the Office checks.
- Construction's test host gets a switch `Backend = Mock | Acc`, so the same page objects serve the
  WireMock suite and this one.
- Sync is eventual: "within N seconds" polls the Office page, refreshing between tries (B6).
- A check through `Bouw7.Api` tells a sync failure from a web UI failure.

### 8.5 Service clients for test data (B18): design first

Tests need to set up, clean up and check data below the UI: through Bouw7's API now, and through
other services later (OpenAPI, SOAP, and others). Before anything is built, a design in the Bruinet
repository (`.my/service-clients/design.md`) answers how it fits Bruinet. What it must answer:

| Question | Options to weigh |
| --- | --- |
| How does it relate to what exists? | The **service head** (the app's own services, in the test process), `Bruinet.Mocking.Http` and WireMock (fake services), `Bruinet.Contracts` (DTO contracts and recorded exchanges). B18 is the real client next to them; it may share the contracts' DTOs and exchange logging |
| What is protocol-neutral, in `Bruinet.Core`? | A service and its endpoints per environment, authentication from an account (8.1), every call in the step log and artifacts with masks, a clean-up registry (each create registers its undo, run in reverse at the end), and the run marker |
| What is per protocol, in a package each (AD-026)? | **OpenAPI**: a typed client generated from the spec (NSwag or Kiota) at build time. **SOAP**: a client from the WSDL (`dotnet-svcutil`). Later **GraphQL** (Strawberry Shake) and **OData**. Each adapter only plugs the generated client into the core |
| Generated or dynamic? | Generated typed clients (checked by the build, easy for agents to use) or calls by operation id read from the spec at run time (no code generation). Proposal: generated |
| How do scenarios use it? | Not as generic HTTP steps ("POST /projects"). The suite writes domain phrases on top (`Given a project "{run} Bouw" exists`), so scenarios stay readable and survive an API change |
| Tokens | Got from the same sign-in as the UI (8.1, B5), or a client-credentials flow per service. Which ones does Bouw7 support? |
| Generation in the agent loop | Can an agent add a domain phrase from the spec on its own, with the build and a round trip against acc as the gate? |

For Bouw7: which format the Heimdall API spec is in (the iOS repo generates a client from it),
and whether anything Bouw7 uses speaks SOAP.

## 9. Learning site: the friction log

G2 is only as good as what we write down. Every stage adds rows to `Bouw7/.testing/400-FrictionLog.md`
(skill `friction-log`):

| Field | Example |
| --- | --- |
| Stage | 3 Controls |
| What happened | Agent needed 4 tries for `VInputSelect`: the menu is attached to `body`, not to the input |
| Cost | 25 min, 3 agent runs, 1 human intervention |
| Workaround | Hand-written `Options` locator on the page scope |
| Change | Bruinet: ControlObjects may own a detached popup (B3). `Bruinet.Web.Vuetify2`: `VSelect` uses it |

Measured per module, so improvement is visible in the orchestrator's reports:

| Metric | Why |
| --- | --- |
| Time from "new module" to first green smoke test | The headline number we want to bring down |
| Human interventions per stage | How AI-driven it really is |
| Controls reused vs newly made | Whether `Bruinet.Web.Vuetify2` pays off |
| Verify hit rate of the first draft | Quality of the Scraper's rules |
| Agent retries and tokens per stage | Where the loop is weak |
| Flaky reruns per 100 runs | Whether waits, tabs and sign-in are right |

## 10. Bruinet changes we expect (hypotheses to confirm or drop)

Each is confirmed only when the friction log shows it.

| # | Change | Why we expect it |
| --- | --- | --- |
| B1 | **Agent stages run unattended with Claude Code** | Only run with Copilot so far; the IDEs do not send the prompt |
| B2 | **Reuse control libraries from outside Bruinet**: the Scraper and the agent match patterns to the project's referenced control libraries (here `Bruinet.Web.Vuetify2`) before making new controls | Vuetify controls are made once and reused, without being part of Bruinet |
| B3 | **Detached popups** (menus, selects, dialogs rendered under `body`) owned by the control that opens them | Vuetify and most component kits do this |
| B4 | **Web components and shadow DOM** in the Scraper's capture and in locators | `b7-*` components |
| B5 | **Sign-in per head** with a cached state per account (part of B14) | Every real site has a login; today each suite invents it |
| B6 | **"Within N seconds" phrases** and cross-app eventual waits | Phone → back office sync |
| B7 | **Run marker and clean-up hooks** in `Bruinet.TestData` | acc is shared |
| B8 | **Scenarios from specs** (`scenario-from-spec`) | Stage 5 |
| B9 | **Failure triage** (`run-triage`) over Bruinet's artifacts | Stage 7 |
| B10 | **Re-scrape on drift**: Verify failure → re-draft as a reviewable diff | Page objects stay alive without a person |
| B11 | **`bruinet new site`** (`site-setup`): a child folder in a container repository with careful guards, accounts and sign-in | The "next site in a day" goal |
| B12 | ~~**Dutch danger words**~~ already in Bruinet (English, Dutch, German, French) | Found in phase 1b |
| B13 | **The orchestrator**: `bruinet loop` with stages, gates, budgets and a morning report (section 6.2) | G4 |
| B14 | **Common accounts** in `Bruinet.Core`: `TestAccounts.md` and a git-ignored `TestAccounts.local.md`, account subjects in BDD, `[Account]`, fail when missing, write guard, `bruinet accounts check` (section 8.1) | Every suite with a login needs it; today it is in `Bruinet.Contracts` and Construction only |
| B15 | **Frainet in the loop**: F1, F2, F4, F5 first | The definition of done and spec ⇄ scenario drift |
| B16 | **Parity runs**: one scenario against two heads, with a side-by-side report | Converted app vs web; later old vs new where both exist |
| B17 | **Tab pages**: a tab's content as its own page object, tab switches as waited-for state changes, and tab states in the Scraper (section 8.2) | Bouw7 uses tabs a lot; switches went unnoticed |
| B18 | **Service clients for test data**, design first (section 8.5): a protocol-neutral core and adapters for OpenAPI, SOAP and later others, with the account's token and domain phrases on top | Q4: worth making generic. Bouw7's Heimdall API spec exists in the iOS repo |

## 11. Phases

| Phase | Scope | Exit gate |
| --- | --- | --- |
| **0 Setup** ✅ | Add `Bouw7/` and `Bouw7/.testing/` with its numbered documents to BruinetB7; references to Bruinet's `move` branch; guards; `TestAccounts.md` and `TestAccounts.local.md` mapped to Construction's accounts; a first sign-in fixture; confirm the site's tech | A web test opens the dashboard signed in as `Reader`, locally |
| **1 Accounts and tabs** ✅ | B14 and B17 in Bruinet, as small as Bouw7 needs | `Given NormalUser is signed in` works; a project's tabs open as page objects |
| **1b Friction log** ✅ | Shadow DOM in the Scraper's paths, `## Modules`, `Features` in `uat.config.md`, pages that open themselves, Bouw7's `scraper.config.md` | Friction items 4, 5, 6, 8, 9, 11 closed |
| **2 Controls** | Explore hours, projects and contacts; `Bruinet.Web.Vuetify2` and the suite's `Controls/` and module folders; move the 7 drafts and remove `Scraper.Sites/Bruinet.Html.Bouw7`; then **fix the phase's friction** | Libraries build, each control unit-tested, 👤 accepted; no open friction item of the phase |
| **3 Specs and web tests** | One spec per module (`spec-from-app`), page objects, scenarios; then **fix the phase's friction** | Green on acc 3 runs in a row; `frainet trace` reaches every rule; no open friction item of the phase |
| **4 Orchestrator** | Skills of section 6.1, `site-loop.ps1`, `.testing/100-Loops.md`, morning report; Frainet F1; the B18 design; then **fix the phase's friction** | One module from spec to green with only the 👤 checkpoints; no open friction item of the phase |
| **5 Harvest** | Friction log → Bruinet items; build the top ones; then **fix the phase's friction** | A new module costs measurably less than the first (section 9); no open friction item of the phase |
| **6 Phone E2E** | When the converted MAUI app is ready: Construction on acc, mixed-head UAT, one journey both ways; then **fix the phase's friction** | Green on the Windows head, then on Android; no open friction item of the phase |

**Every phase ends with a friction step.** The friction items a phase logged are fixed before the next phase
starts, in Bruinet or in the suite, each with its test; an item that is not fixed is closed by the owner as
won't-fix, with why (👤). Phase 1b was the first such step. The friction log (`Bouw7/.testing/400-FrictionLog.md`)
says per item which phase found it and which fixed it.

## 12. Risks

| Risk | Mitigation |
| --- | --- |
| A test or Explore changes or deletes real-looking data on acc | Guards, `safe` Explore, write guard per account, run markers, clean-up by marker |
| Personal data in recordings or logs | Masks, `## Secrets`, `Recordings/` not committed |
| acc is slow, down, or reset | Report "site down" as such; retry only sign-in |
| Bouw7 releases change pages under us | Drift check (B10); page hash in every scraped file |
| The agent produces plausible but wrong code | The orchestrator's gates judge, never the agent; 👤 at specs and controls |
| An unattended loop runs up cost | Budgets per stage and per run; the report shows the cost |
| Account lockout or MFA on acc | One sign-in per account per run; `Reader` for everything read-only |
| Credentials leak | Only in `.env.local`, user secrets and CI secrets; tracked files hold names only |

## 13. Questions

Answered:

| # | Question | Answer |
| --- | --- | --- |
| Q1 | Which phone app? | The **converted MAUI app**, in a later phase. First the web app as it is |
| Q2 | Which modules first? | Hours, projects, contacts |
| Q3 | Accounts? | Yes: the named accounts in Construction's `.env.local` |
| Q4 | Use Bouw7's API on acc? | Yes, and make it generic in Bruinet (B18) |
| Q5 | Native app on acc? | Not used: the phone app will be the MAUI one, not yet |
| Q6 | `admin` / `user` organization, may tests write there? | Both, possibly more accounts; yes, tests may create data |
| Q7 | Where does it run? | Depends. Local first |
| Q8 | Ownership? | Let's see |
| Q9 | Where does Vuetify 3 show up? | Not yet, but it exists, so be ready: one project and skill per major version |
| Q10 | Is BruinetB7 reachable? | Yes. `E:\repos\HtmlConstruction` is now its working copy (branch `main`), signed in as `jonkeda` through a credential helper for this repository only, as for Kofflard |
| Q11 | Accounts in `Bruinet.Core`? | Yes |
| Q12 | `test-tooling/` in the working copy: submodule or ignored? | **Submodule** at `test-tooling/` from the public mirror `jonkeda/bruinet`, tracking the `move` branch (as Kofflard has `external/test-tooling`). The suite still uses Bruinet as packages built from it |

## 14. Decisions asked

- Agree the split of section 4.1 (generic in Bruinet, Vuetify 2 in `Bruinet.Web.Vuetify2`, Bouw7 in
  `Bouw7/`).
- Agree that the orchestrator is a script with exact gates and the agent does one stage per run
  (section 6.2).
- Agree the account model (section 8.1), so phase 1 can start.
- Agree the `.testing/` documents and their numbering (section 4.3).
