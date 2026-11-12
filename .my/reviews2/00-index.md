# Bruinet's twelve reviews: what they found

Written 2026-11-11 at `b2ea4570`. The twelve reviews of `additional-reviews-proposal.md`, each done by its own agent the same day.
Findings only: no source was changed and nothing is committed.

## Outcome

All twelve are written. Each cites file and line; I checked three to five of each review's sharpest claims against the source and every one held.
The call model itself came out well: its poller, its step runner and the Playwright and bUnit heads keep AD-004 and AD-009.
The faults sit around it: in the device heads below a call, in what a failure says, in what is packed, and in documents that trail this week's renames.

Two findings are broken today, not just weak. The packages `pack.ps1` makes cannot be restored, and a Stride or Windows MAUI test can pass on a fault.

## The reviews

| Review | Its sharpest finding | Proposals |
| --- | --- | --- |
| [1 Failure messages](01-failure-messages.md) | "Not found" names no page, member or time waited; a scenario that fails to bind reaches no tool. | 10 |
| [2 Waits and sleeps](02-waits-and-sleeps.md) | One guess in 46 sleeps; Stride's start-up wait expires without throwing. | 13 |
| [3 MCP tools](03-mcp-tools.md) | 53 tools in one flat list of two families; one suite that cannot load fails every suite tool. | 11 |
| [4 Head parity](04-head-parity.md) | `Submit()` runs an `EditForm`'s handler on bUnit only; the MAUI contracts never ran on a device head. | 11 |
| [5 Swallowed errors](05-swallowed-errors.md) | Stride reads a failed query as "absent"; Windows and Appium reads answer "no" for any error. | 10 |
| [6 Docs against the code](06-docs-against-code.md) | The quick start's first page object does not compile; `Shell.md` describes a control that is gone. | 5 |
| [7 Skills and instructions](07-skills-and-instructions.md) | The MAUI control skill names the wrong exception; six rules have copies that disagree. | 5 questions |
| [8 Test suite](08-test-suite.md) | Six unit projects with 158 tests run in no script and no CI step. | 13 |
| [9 Processes, files, secrets](09-processes-files-secrets.md) | SQL recordings keep every value and go into git; traces read password fields. | 10 |
| [10 Command line](10-command-line.md) | Exit 1 means both "found problems" and "crashed". | 14 |
| [11 Packages and first use](11-packages-and-first-use.md) | Seven of the packed libraries cannot be restored (`NU1101 Bruinet.Recording`). | 11 |
| [12 Quality settings](12-quality-settings.md) | Code style is not enforced, though AD-033 says it runs as errors. | 7 |

## What several reviews found together

**A test can pass wrongly.**
Stride's `Read<T>` returns null for a failed response, and the Windows and Appium heads of MAUI turn any error into "no" (review 5).
bUnit accepts writes a browser refuses and reads text and visibility from attributes alone (review 4).
A negative assertion on these heads can pass while the app or driver is broken, which is what AD-009 rules out.

**A missing element answers three ways.**
The contract says `IsVisible()` on a missing element is null; every stack's generated code answers false; the three tests that would show it are skipped with a reason that is no longer true (reviews 4 and 8).
This one needs a decision, not a fix: which answer is the rule.

**CI checks less than AD-033 says.**
It runs for `main` only, style rules are suggestions, six test projects are in no list, the packages are never restored after packing, and two guard scripts pass when `git diff` fails (reviews 8, 10, 11 and 12).
Work lands on `move`, so most of it meets the scanner for secrets and nothing else.

**The documents trail the code.**
The quick start fails against the packed `Bruinet.Maui`, found once by search and once by building it (reviews 6 and 11).
`docs2/`, `.github/PIPELINE.md`'s workflow file and a Selenium web head are named and do not exist (reviews 6, 7 and 10).

**Half the control contracts are bound by nothing.**
Eight of sixteen contracts run on no head, and WPF, WinForms, Stride and native Android bind none (reviews 4 and 8).
The call model is proved per stack in near-identical copies while `Bruinet.Core.Tests` never runs it; Core's mutation score is 34%.

## What I would do first

Six pieces, in this order. Each names the review's own proposals.

1. **Make the packages restorable.** `pack.ps1` packs every packable project and CI restores a consumer from them (review 11, proposals 1 and 2). A release today ships packages nobody can install.
2. **Stop the heads answering "no" for a fault.** Stride's reads throw; the Windows and Appium catches name their exceptions (review 5, proposals 1 to 3). Run the MAUI contracts on Windows with it, which review 4 says has never been done.
3. **Decide the missing element's answer**, then unskip the three contract tests (reviews 4 and 8).
4. **Keep secrets out of recordings and traces.** Redaction for SQL recordings, password fields masked in traces, the SQL hook refused outside a test setup (review 9, proposals 1 to 3). Before the first release, since recordings are committed.
5. **Make "not found" say where**, and write a result for a scenario that failed to bind (review 1, proposals 1 and 4). The cheapest change to what every red test says.
6. **Run what is not run.** The six test projects into `coverage.ps1`, and the guard scripts fail when `git diff` does (review 8 and review 10).

The renames (MCP tools in review 3, command options in review 10) are free only until the first `Shipped` line. They wait for your yes because every skill and Tomorrow's own documents name those tools.

## My proposal's counts, corrected

- Broad catches: 176, not "about forty"; 38 of them can hide a failure (review 5).
- Skipped tests: 15 literal skips, not six, plus 74 that skip by environment (review 8).
- The 46 sleeps held: 33 pause inside a wait, 12 pace, 1 guesses (review 2).

## What was run and what was read

- **Run:** review 1's failures on Html (Playwright headless and bUnit), Blazor and MAUI Headless; review 8's mutation sample on `Bruinet.Core` and `Bruinet.Bdd`; review 11's pack, one Html consumer and the six `Create….Bat`.
- **Read only:** everything on the FlaUI stacks, Appium, native Android and Stride. No UI test took the desktop or a device.
- **So unproven:** which exceptions UI Automation raises when an app's handler throws (review 5's findings 2 to 4), and every head difference in review 4. Both name the run that would settle them.
- Each review ends with its own list of what it could not settle.

## Settled after the reviews

- `verify_page_objects` does write to the project: `Remember` saves the verify summary in the Scraper project's workflow state (`ScraperService.Workflow.cs:267`). Review 3 left this open.
- Tomorrow's `src` and `tests` do not use `UatControlAssertions` either, so review 5's proposal 5 (delete it) breaks nothing there.

## For the owner

1. **This checkout's suites do not load.** `list_suites`, `list_steps` and `failing` answered a `TypeLoadException` during review 3, probably from a test project built before a rename. My proposal: I rebuild the suites and confirm, since it also hides any failing test from the tools.
2. **Which pieces to make.** My proposal: the six above, each a commit on `move`, starting with the packages.
3. **The missing element: null or false.** My proposal: false, as all six stacks answer today, and change the contract; "is it visible" has a plain answer when it is not there.
4. **Selenium.** No web head uses it. My proposal: take it out of the README and the three docs, and drop `WebDriverManager` if nothing else needs it.
5. **Commit these documents.** My proposal: yes, on `move`, as `.my` is tracked.

## Where the plan stands (2026-11-12)

Every task of `plan.md` from R2-T00 to R2-T104 is done on the `reviews` branch, except R2-T85, which became R2-T119. Each is a commit with its why, pushed to `origin/reviews`; `move` has not moved since `1ec3e616`, so the branch merges as it is.
The two broken findings are fixed: the packages restore in a consumer, and a fault no longer reads as "no" on any head.
The MCP tools and the command line are renamed once, before the first shipped version; the change list holds a row per break for the consumers.

## What is left

- **The consumers, after the merge:** Tomorrow, Kofflard and construction move their submodules and follow the renames (R2-T105 to R2-T107). The change list says what to find and what it becomes.
- **New work the run found:** MAUI's ScrollView as a scrollable control (R2-T115), the Android head's missing reads (R2-T116), the docs' other samples and a quick start per stack (R2-T118), and a slimmer `bruinet` package (R2-T119).
- **Failures that were there before:** R2-T109 to R2-T114 and R2-T117, each with when it was seen; none was caused by this branch, checked at an earlier commit where it mattered.

## What stays unrun

- iOS: nothing ran on it, for want of a Mac. The MAUI contracts' mobile skips name Android's reasons, which iOS may not share.
- The WPF and WinForms UI suites ran only where a task touched them; their contracts run on fakes in the unit tests.
- The Visual Studio and VS Code extensions' own suites were not run; the VS Code test's change (R2-T65) is unrun.
