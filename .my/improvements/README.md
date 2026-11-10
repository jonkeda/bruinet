# What could improve Bruinet

A read of the README against the repository on `move` at b399acbe (2026-10-30), its `.my/` index,
CI and the 2026-10-21 review. Nothing was run. Each point carries a proposal and why.

## Outcome

Bruinet's core is in good shape: most of the 2026-10-21 review landed (the pause sleeps, the
`Run*` move-down into `ControlCalls`, `Bruinet.Core.Legacy`, `BRUINET_TIMEOUT_SCALE`). What holds
it back now is breadth. The README promises eight heads, nine tools and about forty packages,
while a long tail of them is written but never tried, nothing has been released, and the
repository still shows its construction site. The best next work is to finish and prove what
exists, not to add surfaces.

## 1. Say honestly what is proven

The README presents every head and tool at the same level, but `.my/` tells another story: iOS
is written and never run on a simulator, the Visual Studio extension's manual plan is unrun, the
Scraper in Visual Studio is untried, the Presenter's journeys and most narrator checks are unrun.

- Add a status column to the heads and tools tables: verified, written, or experimental.
- A reader then trusts the green rows, and the yellow ones become a visible to-do list.
- Proposed first, because it costs an hour and protects every other claim in the README.

## 2. A pause on new surfaces until the "not tried" list shrinks

Each week adds a capability (voices, SSML, pacing, SQL replay, selection), and each leaves
by-hand checks behind. The open items table in `.my/README.md` is now longer than its topics.

- Run one sweep: the Visual Studio manual plan, the narrator's M-checks, the Presenter journeys.
- Close or drop each item; a check nobody will run is a feature to mark experimental.
- Proposed: no new tool until that table fits on one screen.

## 3. Ship a first preview release

`Version` is 0.1.0, there are no tags, and `release.yml` (on a tag) pushes only to the org's private
GitHub Packages feed, with the run's own `GITHUB_TOKEN`. The CHANGELOG's "Unreleased" already holds
breaking changes "no release shipped". So the README's `dotnet tool install -g Bruinet.Cli`, which
reads nuget.org, cannot work for anyone yet.

- Tag `0.1.0-preview.1`, so the packages and the CLI reach the org's feed.
- A public nuget.org push is a choice of its own: it needs a `NUGET_API_KEY` secret and a public repo.
- Mark everything preview, so breaking changes stay cheap.
- Consumers (Tomorrow, Construction) can then pin a version instead of a submodule commit, when
  they want to.

## 4. Tidy the repository's front door

Done on 2026-10-30 (37f180a7) except Frainet, which stays for now. The first look at the root
contradicted the README's polish.

- `docs2/` is in the layout table but no longer exists: drop the row.
- Six ad-hoc scripts sit at the root (`test-key*.ps1`, `test-pipe.ps1`, `start-appium.ps1`,
  `run-android-tests.ps1`): move them to `tools/Scripts` or delete them.
- `Frainet/` (a dark-factory spec tool, 40 tracked files) lives inside Bruinet without a word in
  the README: give it its own repository, or a row saying why it is here.
- `srcnew/` and `testsnew/` are named after a past move: rename them to `src/` and `tests/` once,
  before a release fixes paths in people's scripts.

## 5. Finish the call model on every stack

**Done 2026-11-10** for the stacks ([../upgrade/plan.md](../upgrade/plan.md)): WPF, WinForms and Stride are on the
call model and `Bruinet.Core.Legacy` is gone, so "one way to write a test" holds for every stack.

- Left: the review's Appium items (M1 to M7; one `WebDriverWait` is still in the tree).

## 6. Close the gaps a BDD user will look for

The Reqnroll comparison named four; hooks and scenario outlines are built since. Two are left:
Cucumber Messages output and a set comparison of tables.

- Cucumber Messages first: it lets every existing report and CI tool read Bruinet's results.
- Then say in the docs that hooks and outlines exist, since that is what a Reqnroll user looks for.
- Designed on 2026-10-30 in [bdd-gaps/design.md](../bdd-gaps/design.md): a set comparison turned
  out built too; the design adds Cucumber Messages (parked until a reader is named), several Examples blocks and a Reqnroll page (built).

## 7. Prove the browser in CI

CI builds, runs unit tests with `Category!=Browser`, and the MAUI suites headless on Windows and
macOS. No browser suite, no Android and no bUnit-versus-browser comparison runs there, so the
claim "the device stays the judge at a gate" is only kept on the owner's machine.

- Add a Playwright job (Chromium, Linux or Windows) for the Html, Blazor and Todo web suites.
- An Android emulator job can wait; mark it so in item 1's table.

## 8. One page for a newcomer to choose

A new user must choose a stack, a head, xUnit or Markdown scenarios, and which of nine tools to
install. The README lists all of them but helps choose none.

- Add a short "Start here" path per stack: one page object, one scenario, one command, on the
  cheapest head, then the same test in the browser or on the device.
- Point the Quick Start at it, and keep the tables for reference.

## Order proposed

Items 1 and 4 first (an afternoon, no code risk), then 2 and 3 together (the sweep feeds the
release notes), then 7, 6 and 5 as the users ask. Item 8 lands with the release.
