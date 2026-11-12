# Bruinet review: summary of the nine answers

Nine reviewers each answered one prompt from `.my/copilot` (01–09). Each one read Bruinet's README and could look
at its source. The full answers are in this folder. Reviewers 02, 04 and 08 hit a usage limit or chose not to search,
so their competitor facts and prices come from model knowledge, not from fresh web searches.

## What is genuinely new

Almost every piece has a precedent somewhere. Three pieces are rare: MauiHeadless (running the same page objects
as the device heads), SQL replay for UI tests, and test selection based on the test ids and routes each test
touched. The combination is what is unusual: one typed test model running on device, browser, in-process and
service heads, with machine-readable output that feeds selection, replay and agents. The verdict is significant
innovation for the .NET/MAUI niche, but not strategically unique and not a moat (01).

## The idea you wanted challenged: one scenario, many heads

The reviewer split it into three claims (03):

- **One scenario across the UI heads of one app** is sound and well built. It is not new in general, since
  Android (Espresso on Robolectric or a device), Flutter and Avalonia already do it, but it is new for MAUI.
- **The service head** mostly keeps the wording consistent. In the Todo sample only 2 of 41 scenarios run there,
  and the one shared with a UI head checks the database, not the screen.
- **The shift is in what tests cost**, which matters a lot for agent loops. It is not a shift in test strategy.

The risk is not the gaps between heads, which are documented unusually well. It is that a pass on a cheap head
looks exactly like a pass from the device that judges the gate, and the Windows gate never uses real input. Also,
8 of the 10 head differences found in one app were bugs in Bruinet's own drivers.

## MAUI headless

The capability is rare for MAUI, but common in other stacks (Avalonia.Headless, bUnit, Flutter widget tests,
Robolectric). It can plausibly become the default fast first stage for MAUI testing, but never the judge: there is
no layout, no pixels, no handlers, no virtualization and no real input. The enterprise risks are its reflection on
MAUI internals, the refactoring each app needs, the net10.0-only support, and the bus factor (02).

## Position and value

The strongest position is narrow: one way to test a MAUI app (and its Blazor and desktop siblings) from headless
to the device, aimed at MAUI teams stuck with slow Appium suites or no UI tests. "AI-native", "cross-platform
automation" and "quality engineering platform" are traps (04). In order of enterprise value: faster feedback,
device-free execution, cross-stack testing, test selection, agent integration, SQL replay, generated page objects,
narrated recordings (05).

## What blocks adoption

Every reviewer who looked raised the same points, and they come before any capability gap:

- **Outsiders cannot get it.** The repo is private and packages go only to a private GitHub Packages feed, so the
  README's `dotnet tool install` and `dotnet add package` fail for outsiders (06, 07, 04).
- **One author.** About 1,130 of 1,133 commits are by one person, spread over 43 packages and eight heads (05, 07).
- **Ownership.** The repo is `test-tooling` under a company's GitHub organization, so who owns the IP has to be
  settled first (04, 07, 08).
- **The front door.** The Quick Start never defines the `AppFixture` its test uses, and a newcomer has to clone and
  build everything (07).

## Capability gaps

The serious gaps are:

- Visual regression, which also clashes with the headless heads that draw no pixels.
- Web accessibility checks against WCAG.
- Standard reports (Cucumber Messages, JUnit, HTML) and export to Xray or TestRail.
- Sharding and cloud devices.

Contract testing and HTTP replay are not missing: `Bruinet.Contracts` and `Bruinet.Mocking.Http` exist, but the
README never mentions them (06).

## Commercial potential

As a company that sells a test framework, Bruinet is weak: the niche is small and expects the framework to be
free, and every piece can be copied. As an open-source project it is strong. The realistic routes are MAUI and
desktop support or migration services, hosted run history and diagnostics, or an acquisition by a .NET tooling
vendor (08).

## What Bruinet is, in one sentence

For a CTO: a .NET-only layer that separates your acceptance tests from the UI technology and the device, at the
price of betting on a young, broad, small-team framework (09). The investor sentence was the hardest to write,
because the README doesn't say who pays or why now.

## The recommendations most reviewers share

- Lead with MauiHeadless and MAUI testing as the way in.
- Publish to nuget.org and settle ownership with the employer.
- Add a `dotnet new` template with a five-minute path to a green test.
- Add a table of what each head can and cannot prove.
- Move Stride, Presenter and narration out of the headline and label them experimental.
- Find a second maintainer.
