# Contacts pilot: Android first, then native iOS

Status: proposed, 2026-11-16. Part of [the plan](000-Plan.md); the journeys are in
[the Contacts example](010-Contacts-Example.md).

## The pilot in short

The pilot runs the Contacts journeys on every app, one app at a time, and measures what it
costs. The order is MAUI on Windows, MAUI on Android, native Android, then native iOS with a
new Bruinet iOS stack on the Mac. At the end we decide, with real numbers, whether the other
modules follow, and for which apps.

Out of scope: other modules, a nightly CI pipeline (the pilot runs on demand and on a
schedule on one machine), and the Bouw7 web app as a fourth head.

## What the pilot must tell us

- **Cost:** hours per app, split into one-off work (fixture, login, controls, iOS stack) and
  work that repeats for every module (page objects, steps, data).
- **Stability:** how often a journey fails for a reason that is not the app, measured over ten
  runs per app.
- **Value:** the parity differences found that the static parity review missed.
- **Run time:** minutes per app for the Contacts journeys.

The pilot keeps a log with one line per work session (date, app, step, hours, note), so
these numbers come from the log and not from memory.

## What the pilot builds on

- **Bruinet native Android** exists, with a gallery sample, so native Android needs no new stack.
- **Bruinet's DevFlow driver** runs MAUI on the Android emulator and the iOS simulator.
  It is being finished beside Appium in the Bruinet repository; the pilot takes the version
  that lands in the submodule. DevFlow drives MAUI only, so the native apps use Appium.
- **Control names and generic steps.** Bruinet now binds phrases such as
  `I type {value} in {control}` through names on the page objects, so each app writes page
  objects with control names and only a few step classes of its own.
- **The Bouw7 web suite** already signs in to Acceptance with named accounts (Creator,
  Reader). The pilot uses the same accounts if they can open the mobile apps.

## Steps

Each step ends with the check in its last column. Estimates are rough, in working days for
one person with AI help, and the log replaces them as the pilot runs.

| Step | Work | Done when |
|---|---|---|
| 0. Check the ground (1-2 days) | The six checks below | Each has an answer, written in the plan |
| 1. Journeys and shared support (2-3 days) | The journeys project, the Contacts feature file, accounts, the Acceptance API client for arranging and cleanup, the run prefix | The feature parses and the head check lists every unbound phrase per app |
| 2. MAUI on Windows (2-3 days) | Fixture with the WebView2 login, the existing page objects with control names, the missing steps | Three journeys and the share scenario pass twice in a row |
| 3. MAUI on Android (1-3 days) | The same head on the emulator through DevFlow; login through the in-app web view | The same scenarios pass on the emulator |
| 4. Native Android (4-6 days) | Fixture, seven page objects, the missing controls, the Appium source for the Scraper | Three journeys pass twice in a row; the parity table has its first two columns |
| 5. Native iOS (8-12 days) | Bruinet iOS stack on the Mac, then the head; MAUI on iOS through DevFlow on the same Mac | The scenarios pass on both iOS apps |
| 6. Decide (1 day) | The pilot report from the log and ten runs per app | The owner chooses how the other modules continue |

That is roughly four to six weeks in total, with native iOS as the largest part.

### Step 0: check the ground

1. **Refresh token.** Log in twice with the same token. If the second fails, the token
   rotates, and the fixture must get a fresh one from the API before each deep link.
2. **Cleanup.** Can a contact and a project be deleted through the API, or only archived?
   If only archived, the journeys use a dedicated tenant that we may fill up.
3. **Account and tenant.** Can the Bouw7 web suite's Creator account open the native apps
   and Construction, with the rights the journeys need? If not, create one mobile test user.
4. **Environment.** Native iOS's "acceptance" setting points at `-dev` hosts, while Android
   and MAUI use `-acc`. Find the iOS setting that reaches `-acc`, or note a small app change.
5. **Navigation.** Where do Contacts and project creation sit in each native app?
6. **MAUI login on Android.** Can DevFlow reach the portal in the in-app web view through
   CDP, including the organization chooser? If not, decide between a coordinate fallback and
   a debug-only login hook in Construction.

### Step 4: native Android in more detail

- **Fixture:** installs the debug APK, picks Acceptance in the login screen's list, opens
  `exactbouwacc://login.do?token=…`, waits for synchronization and the main screen, and resets
  the contact filter.
- **Page objects:** the seven pages in [the Contacts example](010-Contacts-Example.md), with the
  same class and control names as MAUI.
- **Scraper:** add the Appium source for Android here, because every later module depends on
  it. Write one page by hand first, then draft the next ones, and log both times.
- **Steps:** only the phrases the generic steps don't cover, such as the HTML comment editor.
- **Bruinet gaps:** the text, field, button, toggle and picker steps already work on native
  Android. The list and dialog steps do not yet; the pilot moves them to Core once, as
  [generic steps](040-Generic-Steps.md) describes, so every platform shares them.

### Step 5: native iOS in more detail

The Mac builds the Bruinet iOS platform now, in parallel, following
[the native iOS plan](ios/000-Plan.md), and takes over this step when the Windows machine
marks the handoff ready on [the handoff board](ios/100-Handoff.md). The outline:

1. **Bruinet iOS platform** (before the handoff): the full platform mirroring native Android,
   with its gallery sample, guides and skills.
2. **The Appium source for iOS** in the Scraper, after the Android source.
3. **The native iOS head:** fixture (seven taps, pick the environment, deep link), the seven
   pages, the missing steps. Identifiers in the iOS app only where a text locator is ambiguous.
4. **Construction on iOS:** the fixture and login, gated on Appium; DevFlow for the inner loop.

## How we decide

| Outcome | Then |
|---|---|
| A further module costs about two days or less per native app, and journeys are stable | Continue module by module on all apps |
| Native Android pays off, native iOS costs much more | Automate native Android; test native iOS by hand from the same feature files |
| Neither native app pays off | Keep MAUI automated on every platform; test native by hand from the feature files |

"Stable" means at least nine of ten runs per app pass, apart from real parity differences.

## Risks

- **Acceptance.** Data, rights or releases change during the pilot. Each journey arranges its
  own data, and a failure is first checked against Acceptance before it counts against the app.
- **The DevFlow driver is still moving.** MAUI on Android and iOS waits for it; the pilot
  starts on Windows, so it does not block.
- **The iOS platform grows too large.** The Mac builds the controls Contacts needs first, so
  the pilot never waits on the rest.
- **Secrets in logs.** The refresh token is in the deep link; Appium server logs stay local.

## Next action

Do step 0, starting with the refresh token and cleanup checks, because both decide how the
fixtures and the shared support are built.
