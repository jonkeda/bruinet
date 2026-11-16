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

### Step 0 results (from source, 2026-11-16)

All six checks are answered from the source of the backend, both native apps and
Construction. Four still need one live confirmation on Acceptance; that needs a test
account's credentials, which this machine does not have yet.

**1. The refresh token rotates.** Every refresh returns a new refresh token and invalidates
the old one, and the default lifetime is one hour
(bouw `apps/login/src/Bundle/Authentication/Session/UserSessionRefresher.php:32-44`,
`login/.env.dist:54`). A stored token therefore goes stale between runs, so every fixture
must be able to log in through the portal, and must write the new token back after each
refresh. Two heads can never share one token, so each head gets its own account, or the
heads run one after another. *Live:* confirm the Acceptance lifetime, which the environment
may override.

**2. Cleanup by API delete works, in order.** Heimdall hard-deletes contacts and projects
(`DELETE /contact`, `DELETE /project`, rights `contacts.delete` and `projects.delete`), but a
contact with references, such as a project, cannot be deleted
(bouw `apps/heimdall/src/Component/Contact/ContactWriter.php:176-193`).
The cleanup hook deletes the run's projects first, then its contacts. We still use a
dedicated test tenant with the run prefix, so a failed cleanup harms nobody. Favourites are
stored on the device in every app, so a fresh app install clears them.

**3. The account needs these rights:** `contacts.view`, `contacts.create`, `contacts.update`,
`contacts.delete`, `projects.view`, `projects.create` and `projects.delete`. *Live:* check
that the web suite's Creator account has them in the test tenant and can open all three
apps; otherwise create one mobile test user per head.

**4. The environment is not a blocker.** The earlier note was wrong: both native apps have an
environment picker on the login screen, and Acceptance maps to `-acc` hosts on iOS and
Android (exact-bouw-ios `bouw7-Universal-Swift/Abstractions/Login/LoginSessionManager.swift:79-97`,
exact-bouw-android `app/src/main/java/com/gobuildit/bouw7/MainApplication.java:928-949`).
On iOS the picker is visible in debug builds, and in release builds after seven taps on the
logo. No app change is needed. *Live:* iOS still has an older constants file that points
"acceptance" at `-dev` hosts
(exact-bouw-ios `bouw7-Universal-Swift/Constants.swift:14-23`); the
first iOS run must check that the Contacts and project screens do not use it.

**5. Navigation is the same shape in both native apps.** Contacts and Projects are tabs; New
sits in the toolbar; the project form has a contact field that opens a picker; and the
project detail shows the client, which opens the contact.

| Journey step | Native Android | Native iOS |
|---|---|---|
| Create a contact | Contacts tab, New, `ContactCreationWindow` with name, type and comments | Contacts tab, create, `ContactCreationVC` with type and comments |
| Search and filter | Search view and a type filter that is remembered | Search bar and type filter; whether it is remembered is unknown |
| Favourite | Checkbox on `ContactDetailActivity` | Button on `ContactDetailVC` |
| Share as a card | **Not there**: only NFC | Share sheet with a vCard |
| Project for a client | Projects tab, New, `input_contact` picker | Projects list, create, Contact field |
| Open client from project | Tap the client on the project info | Tap the contact view on the project detail |

This confirms the share scenario's tags `@maui @native-ios`: it is the first parity
difference. Android has stable resource IDs on these screens (`input_name`, `spinner`,
`input_comments`, `input_contact`); iOS has no accessibility identifiers at all, so the iOS
head starts with text locators and adds identifiers only where text is ambiguous.

Both native apps log in through the system browser: Android opens the portal with an
external intent, and iOS opens it in `SFSafariViewController`. The portal returns a token
to the app through a callback link (`exactbouwacc://login.do?token=` on Android,
`exactbouw://login.do?token=` on iOS). So a native fixture either drives the browser
pages, or gets a portal token another way and opens the callback link. Which is cheaper
is decided in step 4.

**6. MAUI login on Android does not work through DevFlow today.** Construction logs in through
an embedded web view
(construction `Exact.Construction/Modules/Authentication/Views/LoginView.xaml:181`),
but the Android build does not turn on web view debugging, and Bruinet's DevFlow driver does
not run scripts in web views
([DevFlowMauiDriver.cs:612](../../src/Bruinet.Maui.DevFlow/DevFlowMauiDriver.cs)). The UI tests
already contain a Playwright CDP helper for Android
(construction `Exact.Construction.UITests/Auth/Automation/PlaywrightCdpAndroidWebAuthAutomation.Android.cs`).
Proposal: turn on web view debugging in Debug builds for Android, forward its port with adb,
and reuse that helper for the portal and the organization chooser. A debug-only hook that
plants a refresh token is the fallback, but rotation makes it fragile, and a coordinate
fallback is the last resort. *Decision needed* in step 3.

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
- **Secrets in logs.** Tokens travel in callback links and refreshes; Appium server logs and
  run artifacts stay local, and the fixture never logs a token.
- **The token rotates.** A head that loses its fresh token must log in through the portal
  again, so every fixture keeps a working portal login, not only a stored token.

## Next action

Step 0 is answered from source. Before step 1, get a test tenant and one account per head
with the rights in result 3, and run the four live confirmations. Then start step 1.
