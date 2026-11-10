# The decisions checked against the public API review

Written 2026-11-10 at `98a84eff`. The 36 records in `.docs/decisions/` were read against
`bruinet-public-api-review.md` and the change list's Made rows, and the names they cite were searched for in `src/`, `tools/` and `tests/`.
AD-001 to AD-011, AD-013, AD-021, AD-026, AD-029, AD-030 and AD-033 were read whole; the others only where they name an API. Nothing was built or run.

## Outcome

No decision is overturned by the review; the review's changes mostly enforce them (drivers internal for AD-002, `WaitHelper` internal for AD-004).
Ten records named something the review moved, renamed or deleted, and are corrected.
Three records are shorter: AD-010 (943 to 613 words), AD-029 (361 to 145) and AD-013 (its three "too" paragraphs are one table).
Four points need the owner, below.

## Corrected

| Record | Was | Now |
| --- | --- | --- |
| AD-004 | "On MAUI" the framework waits; non-`Try` members wait | Every stack; `Is…` and `Try*` answer about now (proposal 8, rule 1) |
| AD-005 | Broken-when named `Bruinet.Maui.FlaUI` only | Also MAUI's element deriving from the shared `UiaElementBase`, as `ArchitectureTests` already checks |
| AD-009 | `MauiTestContextOptions.NearMiss` | `NearMiss` on every stack's context options (proposal 11) |
| AD-010 | Three stacks, item keys and `RunProbe` per stack, Legacy still to go | Six stacks, one table of what is Core's; rewritten, see below |
| AD-011 | `BddStepAttribute` in `Bruinet.Core.Testing` | `Bruinet.Bdd`, package `Bruinet.Bdd.Abstractions` (proposal 7) |
| AD-013 | Html's `ControlBase` | Each stack's `ViewBase`; the kept `Control` suffix recorded (proposal 4) |
| AD-021 | `BddEffectiveStepKeyword` in Bruinet.Core | In Bruinet.Bdd.Abstractions |
| AD-026 | 84 projects; no `Bruinet.Bdd.Abstractions` | No count (127 project files today); the package named under R4 |
| AD-027 | Snapshot model in `Bruinet.Core.Scraping` | `Bruinet.Scraper.Snapshots` (proposal 7) |
| AD-029 | Body said `WaitHelper.Pause` and `IsFirstModelPage` stay; named a test that is gone | A short superseded note |
| AD-033 | The API row stopped at the unshipped file | Adds `check-api-changes.ps1` and `[Experimental]` `BRUINET010` (proposals 12, 13) |

`invariants.md` follows AD-010 and AD-013.

## Shortened

- **AD-010** kept every rule and its Broken-when; the dated story of each move-down went, and the record points at itself at `98a84eff` for it.
- **AD-029** is superseded, so it keeps what it decided, why, and the one alternative; the full text is at `98a84eff`.
- **AD-013** has one row per stack in place of a paragraph per stack.
- Not touched: AD-033 (1,385 words), AD-024, AD-032, AD-022 and AD-025 are the longest. They are tables of decisions, each row a rule; shortening them means cutting measurements and evidence from the Why sections, which I would do only record by record with the owner's yes.

## For the owner

1. **AD-026 rules out "a driver merged with its platform package", and three stacks do it.** WPF's, WinForms' and native Android's heads are namespaces inside the platform assembly (the review's finding 4). My proposal: amend AD-026 so that a stack with one head keeps it inside and a second head earns the project (R3), since splitting three packages buys nothing today.
2. **Proposal 5 keeps moving what AD-010 lists as per stack.** Rule 2 (`IElementScope<TElement>` in Core, `acae41e2`) landed during this check and AD-010 now says so; one type parameter leaves its reason against three intact. My proposal: rules 3 to 5 each take their part (item roots, containers, collections, rows) off AD-010's "still per stack" list in their own commit.
3. **AD-010's title and file name still say three stacks are ahead of Core.** My proposal: retitle it "Each stack owns its element and driver", keep the file name so links hold.
4. **The review's finding on Narrator's `Bdd…` names contradicts AD-021**, which asks for `Bdd` names outside `Bruinet.Uat`. My proposal: drop the finding from the review; `SpeechSecrets` beside `VoiceSecrets` is the real half of it.

## Not in the decisions, same cause

- `contracts/call-model.md` and `.docs/README.md` still say the call model is MAUI's, Html's and native Android's.
- The call model's R7 says "Non-`Try` members wait", which proposal 8 sharpened: an `Is…` reads once.
- AD-001 does not say why `MauiHead` and `WebHead` may sit in Core (they name a run's head, not an element type; proposal 7). One sentence would close it.

## Unknown

- AD-012, AD-014 to AD-020, AD-022 to AD-025, AD-028, AD-031, AD-032 and AD-034 to AD-036 were searched for changed names, not read line by line; every type they name still exists.
- The review's own unknown is answered: Presenter is an executable and Scraper, Bdd.Workspace and Mocking.Analyzers are not packable, so AD-033's "every packable library declares its API" holds.

Next action: the owner answers points 1 to 4.
