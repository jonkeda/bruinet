# AD-045: The Factory Keeps What A Test Proved

**Decided:** the Scraper's factory (`bruinet scrape scraper run`, 2026-11-19 and 20) turns an app's places into page
objects and tests a small group at a time, and keeps a file only when a test proved it on the app. One loop takes a
group's places through read, draft, build and test:

| Decision | What it means |
| --- | --- |
| **A class is kept with a test that passed twice** | A drafted class comes with a test that opens the place and checks what the class declares: its fields, tabs, buttons, sections and openers. The test runs twice. What fails twice, or passes and then fails, is put back: the file is deleted, or a file that was there is as it was. |
| **What is not kept has a reason with a code** | `does-not-build`, `did-not-open`, `label-not-found`, `needs-record`, `nothing-to-check`, and the rest of `ReasonCodes`, each with where its fix belongs. The same reason three times in a batch stops the batch: it is a fault of the process, not of those places. |
| **A fix goes to its source, and replay measures it** | A reason is fixed in a sign, a holder, a template or the reader, never in the drafted file. `scraper replay` drafts every drafted class again from the kept captures, without a browser: nothing may change but what the fix meant to change. |
| **A person's class is theirs** | A file without the Scraper's first line is never written over. It is only added to (`ClassMerge`, add only), and by the loop only with a member a dialog's test names, taken out again when the suite does not build. |
| **The app's shape is data** | How a library draws a control, a tab or a section is a row of a signs file; how a suite writes a class, a test or a spec is a block of its `draft-templates.md`. The engine names no library and no app (`The_engine_s_code_names_no_library_and_no_app`). |
| **What is written on the app is the test's own** | A save test makes a record with the run's marker, saves on it, and deletes it. `scraper records --check` says what an app still holds with a marker. Nothing is saved where the setup's `Writes` is `read-only`. |
| **A person's answers bound it** | `setup.md` holds what may be written, what runs, what a change kept without a person may touch, and how many run at once. `scraper keep` refuses a change outside that scope, one that loses a control, and one that makes what is owed worse. |

This replaces three rows of [AD-027](ad-027-the-scraper-records-into-the-project-and-calls-no-model.md) as it stood
before 2026-11-19: recording by hand, tests drafted from a recording, and the Scraper view in an IDE are gone. The
rest of AD-027 stands: one engine behind the command line and MCP, no model called, the code as the registry.

**Why:** the first factory was a set of scripts around the Scraper's commands and kept 1 class of 21 in its last
loop on Bouw7's Projects, in 22 minutes; a draft that did not pass was left for a person, with no reason kept. With
the loop in the engine, a reason per place and replay as the measure, the same module took 7 loops and 5.1 minutes
and kept 20 classes with their tests. When the tests were made to check buttons and openers as well, 38 of 73
drafted classes turned out to declare members the app does not show: a draft is not proved by building, only by a
test that could fail. Evidence: Bouw7's
[development plan and run report](https://github.com/jonkeda/BruinetB7/tree/main/Bouw7/.my/scraper/docs).

**Ruled out:**
- keeping a drafted class that no test opened, or whose test checks less than the class declares;
- fixing a failing draft by editing the drafted file: the next draft writes it over;
- the engine knowing a library's class names or an app's words;
- a drafted test that saves on a record it did not make, or a record left behind with a marker;
- the factory changing a member of a class a person wrote.

**Broken when:**
- `scrape-loop.ps1 -Target test-site -Factory` does not pass: from nothing, the test site must end as a suite that
  builds and whose kept tests pass, with no counter moved and every planted fault ending as a kept class, a reason,
  a page owed a record, or a person's class left alone;
- `scraper replay` on a suite whose captures and templates did not change says a class would be drafted differently;
- a loop leaves a file it wrote that does not build, or a file of a person's changed;
- a file under `src/Bruinet.Scraper` names a control library or an application.
