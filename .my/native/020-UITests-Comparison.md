# Native Bouw7 tests: the UITests repository or Bruinet

This compares the existing [exact-mobile-apps/UITests](https://github.com/exact-mobile-apps/UITests)
repository with the Bruinet journey plan in [the plan](000-Plan.md). The native apps will be
replaced by Construction, so every hour spent only on them has a short life.

## Decided

- No mock backend: journeys run against Acceptance on every app.
- The purpose is parity: a few journeys per module, two to four, run on every app to find
  where the apps differ.
- Each app is a Bruinet head with a fixture, page objects, control objects and steps, and
  every head runs the same feature files the same way.

That keeps Bruinet. This document now records what we take from UITests and why.

## What the UITests repository is

It holds Appium tests for EOL, SRXP, Synergy and WMS: 48 Gherkin scenarios and about 24,000
lines of C#, run with Reqnroll and NUnit, reported with Allure. All four apps are MAUI apps on
MauiCore. There is no Bouw7 project.

- **One page object serves Android and iOS,** because MAUI gives the same AutomationId on both.
  Only about 24 places branch per platform, mostly native dialogs.
- **It runs against Acceptance,** with credentials from environment variables. Each scenario
  creates and deletes its own data with a unique description.
- **The apps are not changed for testing,** except for automation IDs on a branch.
- **Its agent workflow is strong:** approved scenarios first, one live walkthrough through
  Appium MCP, a committed screen map, and a quiet PASS or FAIL run script.
- **Its weak spots:** one static driver, inheritance from a 36 KB `BaseTest`, the account
  chosen by the test name's prefix, and a filter that passes when no test matches.

## How UITests logs in

It drives the real login screens with Appium, and keeps the session between scenarios.

- Before each scenario a hook checks whether the app is already logged in, by looking for the
  welcome screen or the app's menu. With a `@login` tag it opens Settings to check that the
  right user is logged in, and logs out if not.
- **EOL and WMS:** pick the environment on the countries screen, tap Login on the welcome
  screen, then type the user name, tap Next, type the password and submit on the Exact Online
  login web page. The web fields are found by their placeholder text on iOS and by
  `EditText` or resource id on Android. On iOS a hardware Return key closes the keyboard,
  because Appium cannot see it.
- **SRXP:** tap the logo eight times to switch to Acceptance, type the e-mail, continue, type
  the password. A hook logs out a session left behind by an interrupted run.
- **Synergy:** a welcome screen, a login-or-demo choice, a server URL screen, then the
  credentials.
- Passwords come from environment variables. An agent types them with a script that prints
  only `ok`, because Appium echoes typed text in its errors.

For Bouw7 the native apps offer a shortcut UITests does not use: both accept the OAuth
callback as a deep link with a refresh token, after the environment is picked on the login
screen. That skips the browser entirely; [the plan](000-Plan.md) describes it per head.

## Why the UITests model does not fit native Bouw7

The one-page-object trick only works because MAUI shares IDs. Native Bouw7 Android uses XML
resource ids and iOS has storyboards with no identifiers, with different screen structures.
Either repository needs page objects per platform, so UITests saves nothing there. And its
feature files would be a second copy with other wording in another repository, which breaks
the rule that one file runs everywhere.

## What to borrow

- **Login per head, session kept:** check at the start, log in only when needed, log out only
  in a login journey.
- **Unique test data with cleanup on failure,** and a sweep for leftovers.
- **A run script that prints only PASS or FAIL,** and fails when no test matched.
- **Secret typing that never shows the value,** and Appium logs kept out of reports.
- **The screen map,** built into the Bruinet Scraper, as below.

## The screen map and the Scraper

The UITests screen map is one Markdown file per screen: its elements and locators, the page
object that covers it, where it navigates, what it requires, platform differences, open
questions, and per platform whether it was verified and at which app commit. A script tells
an agent which screens changed, so it explores only those.

Most of that already exists in Bruinet's Scraper ([AD-027](../../.docs/decisions/ad-027-the-scraper-records-into-the-project-and-calls-no-model.md)):

| Screen map | Scraper |
|---|---|
| Elements and locators | Captured page nodes in `Recordings/`, committed |
| Which page object covers it | The `// <scraped page=… />` header in the code |
| Changed since last checked | The capture hash in that header |
| Verified per platform | **Verify**, which finds each member in the running app |
| Draft page objects | **Draft**, by rules, into `.scraped.cs` |

Two things are missing. The Scraper reads only Html and Blazor, so it needs a native source
that turns the Appium page source of UiAutomator2 and XCUITest into a snapshot. AD-027 already
allows that: a platform is a source. And it has no place for the notes a person writes:
quirks, platform differences, preconditions and open questions. Proposal: keep those as
the documentation of the person's part of the page object, so the code stays the registry.
On Acceptance, Explore stays off and pages are captured by hand, because a click there
changes real data.

## Risks

Acceptance data and accounts can change under the tests, so journeys fail more often than
mock runs; they run nightly, not on PRs. A few journeys per module means native page objects
for most screens, which are thrown away when native goes; drafting them with the Scraper is
what keeps that affordable.
