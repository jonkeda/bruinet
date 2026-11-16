# Bouw7 journeys on MAUI, native Android and native iOS

Status: proposed, 2026-11-16, revised twice the same day after the owner's decisions. Example:
[Contacts](010-Contacts-Example.md). Comparison with the UITests repository:
[020](020-UITests-Comparison.md). The first step is [the Contacts pilot](030-Contacts-Pilot.md),
which decides how far this plan goes.

## The goal

The suite shows parity: it finds where the MAUI app (Exact Construction), the native Android
app and the native iOS app behave differently. Each module gets a few journey tests that run
unchanged on all three apps against the real Acceptance backend. A journey follows a flow a
user really does, such as creating a client, changing it and finding it again. A journey
that passes on one app and fails on another is a parity difference, and the run report shows
it per module.

Three decisions shape the plan:

- **No mock backend.** Journeys run against Acceptance on every app.
- **A few journeys per module.** Two to four per module cover its main flows, and a journey may
  step into a neighbouring module where the flow does. With about 25 modules that is roughly
  50 to 90 journeys, still far fewer than one per module scenario.
- **Every app is a Bruinet head with the same parts:** a fixture, page objects, control objects
  and step classes. The feature file and the way a run works are the same for all three.

The existing MAUI module scenarios are not touched. They keep the mock backend and run
headless on every PR. The journeys are a second, separate suite.

## The model

Bruinet's [AD-014](../../.docs/decisions/ad-014-one-scenario-file-serves-every-head.md)
already says one scenario file serves every head, and each head binds the same phrases with
its own code. The two native apps become two new heads. The names `android` and `ios` are
already MAUI driver tags in [BddConfig.cs](../../src/Bruinet.Bdd/BddConfig.cs), so the
heads are called `native-android` and `native-ios`.

| Head | Drivers | Bruinet stack |
|---|---|---|
| `maui` | Windows; Android and iOS through MauiDevFlow (not headless: it cannot log in for real) | `Bruinet.Maui.*`, `Bruinet.Maui.DevFlow` |
| `native-android` | Emulator or device through Appium UiAutomator2 | `Bruinet.NativeAndroid`, exists |
| `native-ios` | Simulator or device through Appium XCUITest | `Bruinet.NativeIos`, new |

Each head has the same four parts:

- **Fixture:** installs and launches the app, logs in, waits until the main screen is ready,
  and resets between journeys.
- **Page objects:** one per screen the journeys touch, with the same class names in every head.
- **Control objects:** the head's controls (list row, text field, picker, toolbar action,
  dialog), so page objects stay short.
- **Step classes:** the screen phrases, with exactly the same text as the other heads.

What does not touch the screen is shared by all heads: the Acceptance accounts, an API client
that arranges and removes test data, the run's unique test names, and the steps built on them.

## How a journey shows parity

A `Then` checks what the user sees, in the same words on every head, so the same journey
compares the three apps. Journeys live in one folder per module and carry the module tag, so
the report groups them by module.

Some differences are deliberate, for example where Construction follows iOS rather than
Android. Those are already recorded as parity decisions in the module's
`.migration/modules/<module>/320-ParityDecisions.md`. A deliberate difference never hides inside
a head's step code. It gets its own small scenario, tagged with the heads it applies to and
linking the decision, so the report still shows it.

## How each head logs in

Login is the hardest part without a mock. Each head logs in once per device per run and keeps
the session. Only a login journey logs out.

| Head | Login |
|---|---|
| Native Android | Debug build: pick Acceptance in the login screen's environment list, log in through the portal in the browser, or open the callback link `exactbouwacc://login.do?token=<portal token>` with a token obtained another way. No app change. |
| Native iOS | Debug build shows the environment picker (release: seven taps on the logo); pick Acceptance, then the same choice with `exactbouw://login.do?token=<portal token>`. No app change. |
| MAUI on Windows | The existing WebView2 helper drives the Bouw7 portal through CDP ([devflow-auth skill](https://github.com/exact-mobile-apps/construction/blob/main/.github/skills/construction-devflow-auth/SKILL.md)). |
| MAUI on Android and iOS | The portal in the in-app web view. On Android, turning on web view debugging in Debug builds lets the existing Playwright CDP helper drive it; see the pilot's step 0 results. |

Refresh tokens rotate and live about an hour, so no head can rely on a stored token alone
([pilot step 0 results](030-Contacts-Pilot.md#step-0-results-from-source-2026-11-16)).

MAUI cannot take the native shortcut: its callback must carry the `state` value it created
when the login started.

## What exists and what is missing

| Area | Today | Missing |
|---|---|---|
| Bruinet native Android | Full stack and gallery sample | Nothing for XML views |
| Bruinet native iOS | Nothing; only MAUI on iOS | The stack, an iOS host, a gallery sample |
| Heads | `maui`, `web`, `mixed`, `service` | `native-android`, `native-ios` |
| Scraper | Html and Blazor | A native Appium source (see phase 1) |
| Journeys | None | The feature files and the shared support |
| Native Android app | Resource ids, environment list, deep-link login | Possibly ids on list rows |
| Native iOS app | Environment picker, deep-link login, no identifiers | Identifiers where text locators are not enough |

## Decisions to take

1. **Where the suites live.** Proposal: in this repository. The journeys project holds the
   feature files and shared support; each head is its own test project that links them.
2. **Test tenant and accounts.** Proposal: one Acceptance organization for UI tests only,
   with one user per head, because a rotating refresh token cannot be shared between heads.
3. **One environment for all heads.** All three apps reach `-acc` hosts through their
   Acceptance setting, so every head points at the same environment and tenant. iOS still has
   an older constants file with `-dev` hosts, which the first iOS run must rule out.
4. **MAUI on Android and iOS.** Proposal: turn on web view debugging in Debug builds and drive
   the portal with the existing Playwright CDP helper. A debug-only hook that plants a token
   is the fallback. Alternative: run MAUI journeys on Windows only.
5. **Which journeys per module.** Proposal: two to four per module, chosen from the module's
   parity matrix (`310-ParityMatrix.md`): its main user flows, plus each flow where a
   difference is suspected. Contacts gives the first ones.
6. **Module order.** Proposal: start with the modules nearest to sign-off, because parity
   evidence matters most there: Contacts, then Projects, then the rest by finish order.

## Phases

### Phase 0: Agree

Answer the six decisions. The refresh token rotates (pilot step 0), so each fixture keeps a
working portal login and writes back every new token.

### Phase 1: Bruinet

In the Brinell submodule, on the `move` branch, following [Brinell/AGENTS.md](../../AGENTS.md).

1. Add the two head names and an `@native` tag meaning both, with an AD that extends AD-014.
2. Add a head check: per head, the journey phrases that have no binding yet. It is the work
   list per head, and later a gate.
3. Build `Bruinet.NativeIos` in the shape of `Bruinet.NativeAndroid`: the XCUITest adapter,
   pages, readiness, collections and UIKit controls (button, label, text field, switch,
   segmented control, search bar, table and collection rows, navigation and tab bar, alert
   and action sheet, date picker). Add an iOS host (simctl install, erase, open URL) and a
   recorder to `Bruinet.Appium`.
4. Add a native source to the Scraper. AD-027 already says a platform is a source: the Appium
   page source of UiAutomator2 and XCUITest becomes a snapshot, and drafting writes page
   objects from it. Explore stays off on Acceptance; capture is by hand. This gives Bruinet
   what the UITests screen map does (see [the comparison](020-UITests-Comparison.md)).
5. Write the native iOS guides and the three-head section of the BDD suite guide.

### Phase 2: Journeys and shared support

1. Add the journeys project with one folder of feature files per module and `bdd.config.md`
   metadata for all heads.
2. Add the shared support: accounts from environment variables, the Acceptance API client
   (built on the one the contract tests use), unique names per run and head, and a cleanup
   hook that deletes this run's records and sweeps leftovers older than a day.
3. Write the arrange and cleanup steps once, for all heads.

### Phase 3: MAUI head

Add the MAUI journeys test project with a fixture that logs in for real, page objects reused
from the existing suite where they fit, and the screen steps. Run on Windows first.

### Phase 4: Native Android head

Add `Exact.Bouw7.NativeAndroid.UITests`: a fixture that installs the debug APK, picks the
environment, logs in by deep link and opens the main screen; then page objects, control
objects and steps for the journeys. Add `scripts/run-journeys-native-android.ps1`.

### Phase 5: Native iOS head

The same with `Exact.Bouw7.NativeIos.UITests` and a macOS script. Add identifiers to the
native iOS app only where a text locator is ambiguous, and only for journey screens.

### Phase 6: CI and reports

Journeys run nightly, not on PRs, because they depend on Acceptance. With 50 to 90 journeys,
the heads run in parallel and each head can split its journeys over devices by module.

The report is a parity table per module: each journey against each head, so a parity gap is
one red cell. A journey that fails on one head only is triaged as a test gap, an Acceptance
data problem, or a real parity difference. A real difference goes to the module's parity
matrix and, when it is accepted, to its parity decisions; it never becomes a skip. A module's
parity table is evidence for its sign-off.

### Then, module by module

After Contacts runs on every head, each further module follows the same steps: pick its
journeys, write the feature files, then the page objects and steps per head. The native page
objects are drafted by the Scraper from captures, so most of the per-module work is the steps
and the hand-written part of each page.

## Risks

- **Acceptance changes under the tests.** Data, rights and releases change without notice.
  Each journey creates what it needs and checks only its own rows.
- **Parallel heads share a tenant.** Names carry the run and the head, so heads never see
  each other's records.
- **Secrets in logs.** The refresh token is in the deep link, and Appium logs its commands.
  Appium server logs stay local and are not attached to reports.
- **The portal changes.** Only the MAUI head depends on it; the native heads skip it.
- **Native work is thrown away.** A few journeys per module means native page objects for most
  screens of the apps, and those go when native is replaced. The Scraper drafts them to keep
  that cost low, and the feature files and MAUI head stay.
- **The nightly run gets long.** Splitting by module over devices keeps it within the night;
  a module's journeys can also run on their own when that module is being finished.

## Next action

Run [the Contacts pilot](030-Contacts-Pilot.md): native Android first, then native iOS on the
Mac. It covers phases 1 to 5 for Contacts only, and its result decides the rest.
