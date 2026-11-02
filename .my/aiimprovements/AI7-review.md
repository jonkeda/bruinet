# AI7 review: from a UI element to its source line

## What was built

- `where_is {page, control, suite?}`: the page object member's line, the test id it finds by, and up to five app lines
  (`.razor`, `.cshtml`, `.html`, XAML, code) that make the element, test-id attributes first and the suite's own app
  first.
- `appSource` on `get_failure` and on `run_scenario`'s failures: the top three app lines for the failure's locator.
- No build step: a static search of the test id, which the spike showed finds 267 of 286 controls in four suites
  (AI7-design.md). `UatControlModel` carries the page object's member name.

## Checks

- `Bruinet.Bdd.Workspace.Tests`: 84 passed, then 18 of `BddAppSourcesTests` after the review fixes.
- `Bruinet.Uat.Tests`: 251 passed.
- `Bruinet.Cli.Tests`: 382 of 390 passed. The 8 failures are the generator and data validator tests that always fail
  under `--artifacts-path`. After the fixes, `McpWhereIsTests`, `McpServerTests` and `McpRunTests` passed (28).
- The solution build under `--artifacts-path` stops on the Stride sample's path and on `Bruinet.Maui.UITests`'
  `ControlContracts`. This diff touches neither.
- Agent check (checks/AI7.md): passed by its rule, 2 tool calls as the baseline's, 2 file reads against 6, but the
  agent did not use `where_is`: the seeded bug was one grep away. It found `appSource` noisy for a class locator; fixed.

## Review

The independent review found two blockers. Both are fixed:

- A file or folder that could not be read failed all of `get_failure`. Now reads and walks skip IO and access errors,
  and links are not followed.
- The search scanned other checkouts and ranked them first (`.claude/worktrees`, a submodule). Now it skips
  dot-folders and folders with their own `.git`.

Its concerns, and what was done:

- Fixed: `appSource` is on `run_scenario` as documented.
- Fixed: `appSource` ranks by the suite's project.
- Fixed: nested and XPath locators give their value.
- Fixed: a test id must be the attribute's own value, and a prefix starts a word.
- Fixed: the control's family comes from its first Bruinet base type.
- Fixed: a missing PDB gets its own note.
- Fixed: the file list is kept for a minute and project words per folder.
- Fixed: at most 200 hits are collected.
- Changed: test projects are found by their test framework reference, not by a `tests` folder, so an app beside the
  tests (a test site) is searched.
- Tests added for the walk, the attribute, the prefix and the project ranking. The page object line is now checked
  for the member.
- Open: no test reads `appSource` from a stored failure. Its search is the one `where_is` tests.
