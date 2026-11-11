# Bruinet: proposed further reviews

Written 2026-11-11 at `b2ea4570` as a proposal. All twelve were done the same day: `00-index.md` says what they found and corrects two counts below (broad catches, skips).
The counts come from `git grep` and file listings at that commit; nothing was built or run.

## Outcome

Two reviews exist in `.my/reviews`: the public API (with its proposals 5, 8, 10 and 11) and the decisions checked against it.
Both read what Bruinet declares. Neither looked at how it behaves, what it tells its reader when a test goes red, or what its agents, command line and docs say.
Twelve reviews are proposed below, each one a single document in `.my/reviews2` with findings and proposals, as the API review is.

Start with three: the failure messages, the waits, and the MCP tools.
They touch what every user and every agent meets daily, and each can be done in one sitting.

## What is already covered

- The C# surface: `bruinet-public-api-review.md`, from the 34 `PublicAPI.Unshipped.txt` files.
- The 36 decision records against that surface: `decisions-vs-api-review.md`.
- Speed and test selection: the plans in `.my/test-selection`, so no performance review is proposed here.
- The Scraper's own issues: `.my/ScraperEx`.

## The twelve, in the proposed order

| # | Review | The question it answers |
| --- | --- | --- |
| 1 | Failure messages | Does a red test say what to fix, on every stack? |
| 2 | Waits and sleeps | Does the code keep AD-004 itself? |
| 3 | MCP tools | Can an agent pick the right tool of 53 from its name and description? |
| 4 | Head parity | Does one control behave the same on every head of its stack? |
| 5 | Swallowed errors | Where can a failure disappear before the test sees it? |
| 6 | Docs against the code | Do the guides still compile after this week's renames? |
| 7 | Skills and agent instructions | Do `AGENTS.md` and the 13 skills describe today's Bruinet? |
| 8 | The test suite | What do the 49 test projects prove, and what twice? |
| 9 | Processes, files and secrets | What does Bruinet start, write and record on a user's machine? |
| 10 | Command line | Is `bruinet` one consistent command line, as AD-028 says? |
| 11 | Packages and first use | What does a new consumer install, and does the first test go green? |
| 12 | Quality settings | What is still excused from the checks, and why? |

## 1. Failure messages

The API review praised the typed exceptions but read only their declarations.
This review breaks things on purpose: a wrong locator, a disabled control, a page that never loads, a stale element, a missing phrase, a missing recording.
It runs each on every stack's sample and on both Html heads, and judges the message a person or agent gets.

- **Reads:** the exception types in `Bruinet.Core`, each stack's near-miss text, `failing`, `get_trace` and `why` for the same failures.
- **Judges by:** does it name the control, the page object member, what was expected and seen, and the next thing to try.
- **Gives:** one table of failure by stack, and the messages to rewrite.
- **Needs:** the samples running, so this one builds and runs. About half a day.

## 2. Waits and sleeps

AD-004 says the framework waits for state and nobody sleeps. `src` and `tools` hold 46 `Task.Delay` or `Thread.Sleep` calls.
Some are poll intervals inside a wait and are right; the review sorts each into a poll, a pacing delay (recordings, AD-024) or a guess.

- **Reads:** the 46 sites, every timeout default, and where a timeout can be set.
- **Gives:** the guesses to replace with a condition, and one table of the timeouts with their defaults.
- **Also asks:** are the defaults the same on every stack, and is one place enough to change them.

## 3. MCP tools

The MCP server offers 53 tools; they are the agent's API, and nobody has reviewed them as the C# surface was.
An agent chooses by name and description alone, so overlap costs a wrong call.

- **Reads:** each tool's name, description, parameters and a sample result's size.
- **Looks for:** pairs an agent would confuse (`run_test` and `run_scenario`; `failing`, `last_runs`, `get_trace` and `why`; the four `list_recorded…` and `list_scraped…`), results too large for a context, and verbs that differ for the same act.
- **Gives:** renames, merges and trimmed results, each a row for `ten-agent-proposals.md` to take or leave.

## 4. Head parity

`tests/Bruinet.ControlContracts` holds thirteen contract files and one host, `ControlContracts.Html`.
The review lists which heads run which contract: Playwright and bUnit for Html; Appium, FlaUI and Headless for MAUI; the one-head stacks.
Where a head has no contract run, it reads the head's code against the contract by hand.

- **Gives:** a matrix of contract by head with run, read or missing, and the differences found.
- **Why now:** AD-030 and AD-032 make the in-process heads the daily ones, so a difference from the real head is a test that passes wrongly.

## 5. Swallowed errors

AD-009 says an app's bug stays a failure. A rough search finds about forty broad `catch` blocks in `src` and `tools`.
The review reads each one and asks what the test sees after it.

- **Sorts into:** rethrown with context, logged and dropped, or silently dropped.
- **Gives:** the catches that can hide a failure, with the narrower exception each should name.

## 6. Docs against the code

`docs/` has 22 guides, 7 platform guides and about 62 C# samples in them.
Proposals 4, 7 and 11 renamed controls, moved namespaces and joined the options types this week.

- **Does:** compiles every sample against today's packages, and follows every link and command.
- **Gives:** the stale samples, and a proposal to keep them compiled by the build (AD-033) so this review is done once.

## 7. Skills and agent instructions

`AGENTS.md` (219 lines), `copilot-instructions.md`, the 13 skills under `.github/skills` and the MCP server's instructions all tell an agent how to work.
They were written at different times and can disagree with each other and with the code.

- **Reads:** every type, command and rule they name, searched for in the source as the decisions review did.
- **Gives:** corrections, the rules stated in more than one place, and what can be cut.

## 8. The test suite

49 projects under `tests/`, six skipped tests.
The review asks per project what it proves and at which tier, not how much it covers.

- **Looks for:** the same behaviour tested once per stack where one contract would do, UI tests that a unit test would prove, skips without a reason, and tests that cannot fail.
- **Uses:** `mutate.ps1` on two or three core projects as a sample of how much the tests catch.
- **Gives:** tests to move down a tier, to join or to delete, and the gaps.

## 9. Processes, files and secrets

Bruinet starts processes (47 `Process.Start` or `ProcessStartInfo` sites), hooks SQL, proxies HTTP and writes recordings, traces and videos.
A recording can hold whatever the app sent: tokens, connection strings, personal data.

- **Reads:** what is started and with which arguments, where files are written and whether a path can leave the project, what the SQL and HTTP recordings keep, and how `voice.secrets.json` is read.
- **Gives:** what to redact by default, what to refuse, and what a guide must warn about.
- **Not:** a penetration test; `security.yml` already runs the scanners.

## 10. Command line

AD-028 decided one command line. The review reads it as a user: every command, option, exit code and help text of `bruinet`, and the scripts in `tools/Scripts` that still stand beside it.

- **Looks for:** options named differently for the same thing, exit codes a CI script cannot rely on, and scripts that should be commands or should go.
- **Gives:** a table of commands with their options, and the renames.

## 11. Packages and first use

Nothing is shipped yet, so the first install is still free to shape.
The review packs with `pack.ps1`, then follows `docs/getting-started` and each `Create….Bat` in an empty folder.

- **Reads:** what each package pulls in, which packages a consumer must name, and where the first run stops.
- **Gives:** the dependency graph a consumer sees, and each step where a new user would stall.

## 12. Quality settings

`Quality.Legacy.globalconfig` still exists, with 20 `#pragma warning disable` and 17 `SuppressMessage` in `src` and `tools`.

- **Reads:** which projects are on the legacy settings, and each suppression's reason.
- **Gives:** the suppressions that can go and the projects that can leave Legacy. The smallest of the twelve.

## Left out on purpose

- **Performance:** covered by the speed plan.
- **The IDE extensions** (`Bruinet.Bdd.VisualStudio`, `.VsCode`): worth a review once the preview settles; review 10's findings will apply to them.
- **Frainet:** a product of its own beside Bruinet.

## For the owner

1. **Which to start.** My proposal: 1, 2 and 3, in that order, then 4 before the first `Shipped` line since a head difference is cheapest to fix now.
2. **Read only, or fix as found.** My proposal: reviews only write findings and proposals, as the API review did; fixes follow as numbered proposals you pick from.
3. **Reviews that build and run** (1, 6, 8, 11) take the machine for a while. My proposal: run those in a worktree, the others from the source alone.
