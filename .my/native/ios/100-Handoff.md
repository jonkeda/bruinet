# Handoff board: Windows and Mac

The only status for the native work. Each machine edits its own rows and appends to Events.
Pull before editing, push right after.

## Windows machine (Contacts pilot, steps 0 to 4)

| Item | State | Note |
|---|---|---|
| Pilot step 0: check the ground | Done | Live on Acceptance for MAUI Windows |
| Pilot step 1: journeys and shared support | Done | `Journeys/` in Construction; native iOS head skeleton included, with its head check |
| Proof of concept J0 on MAUI Windows | Done | Passed twice in a row, 30 s and 25 s; the results are in [the pilot plan](../030-Contacts-Pilot.md) |
| Proof of concept J0 on native Android | Done | Passed twice in a row on the emulator, 40 s and 38 s |
| Pilot step 2: J1 to J4 on MAUI Windows | Waiting | On the proof of concept; J1 passed once with parked work, see [remaining work](../050-Remaining-Work.md) |
| Pilot step 3: MAUI on Android | Not started | |
| Pilot step 4: native Android | Not started | |
| Generic list and dialog steps in Core | Not started | Mac phase I6 waits on this |
| Scraper Android source | Not started | Mac phase I7 waits on this |
| Handoff to Mac | Not ready | Mac phase I8 waits on this |

## Mac (native iOS plan)

| Item | State | Note |
|---|---|---|
| I0: set up the Mac | Done | Xcode 27.2, Appium 3.9 with XCUITest 12.19, iOS 27 simulator; Construction and `exact-bouw-ios` build and launch on the simulator; `exact-bouw-ios` needs `CODE_SIGN_IDENTITY=-` so its app-group entitlement is embedded, else it stops at start. No Java, so the Android projects and the full solution do not build here |
| I1: skeleton and unit tests | Done | `Bruinet.NativeIos` and its tests in the solution under 2 Platforms/iOS; 83 unit tests pass, Hover and RightClick contracts skip |
| I2: driver, element, readiness, host | Done | Measured live on the simulator; `IosHost` and `IosRecorder` in `Bruinet.Appium`, recorder times from simctl's "Recording started" |
| I3: controls | Done | 22 generated controls plus navigation bar, toolbar, alerts, sheets, stepper, table and collection views. DatePicker deferred. Measured live in I4 |
| I4: gallery sample and UI tests | Done on iOS | `Bruinet.Samples.NativeIos.App` and `Bruinet.NativeIos.UITests`: 28 UI tests and both shared gallery features pass on the iOS 27 simulator; 87 unit tests pass. The features are linked from the Android test project. Rerunning them on Android waits on Windows |
| I5: docs, skills, decision | Done | Platform, run and quick-start guides with compiled samples; skills `ios-control` and `ios-ui-test`; AD-042 (with iOS in AD-010, AD-013 and the control-object contract); CHANGELOG; the doc indexes |
| J0 on native iOS | Not started | May start now, see [the Mac prompt](200-Mac-Prompt.md) |
| I6: generic list and dialog steps | Waiting | On Windows: generic steps |
| I7: Scraper iOS source | Waiting | On Windows: Scraper Android source |
| I8: Contacts pilot on iOS | Waiting | On Windows: handoff |

States: Not started, In progress, Done, Waiting, Blocked (say on what).

## What the handoff contains

When the Windows machine sets "Handoff to Mac" to Ready, it lists here:

- the journeys project and the Contacts feature file paths;
- the shared support (accounts, API client, run prefix, cleanup) and how to set its secrets;
- the native Android head as the model for the iOS head, with its page and control names;
- the answers to pilot step 0, especially the refresh token, cleanup and the iOS environment;
- the pilot log so far.

## Events

Newest last. One line each: date, machine, what happened or what is asked.

- 2026-11-16, Windows: plan written; Mac may start I0.
- 2026-11-16, Windows: pilot step 0 answered from source. For the Mac: iOS Acceptance maps
  to `-acc`, login is `SFSafariViewController`, and `exact-bouw-ios` has no accessibility
  identifiers.
- 2026-11-16, Mac: I0 to I3 done; `Bruinet.NativeIos` builds clean and its unit tests pass.
  Open for I4: off-screen table cells, alerts and sheets, picker wheels and DatePicker.
- 2026-11-16, Windows: step 0 done. The `create` account logged in and created a contact and
  a project on Acceptance through Bruinet; the old iOS `-dev` constants are unused. The
  DevFlow-enabled Windows build is fixed in Construction.
- 2026-11-16, Windows: step 1 done. For the Mac: `Journeys/Exact.Bouw7.NativeIos.UITests`
  is the iOS head skeleton (fixture on `NativeIosTestFixtureBase`, head `native-ios`); its head
  check lists the 20 phrases the iOS head needs. A portal token can be had headless without
  the device, and the access token lives 15 minutes. Bruinet now maps `NativeIosTestFixtureBase`
  to target NATIVEIOS.
- 2026-11-16, Mac: I4 done on iOS. Asked of Windows: rerun the native Android gallery suite;
  its only changes are the tags and `Head` below.
- 2026-11-16, Mac: per AD-014 the shared gallery features now say `Target NATIVEANDROID,
  NATIVEIOS` and `@native` instead of `@android`; the gallery suites name `Head native-android`
  and `native-ios`. No engine change was needed.
- 2026-11-16, Mac: measured in I4, for the I5 guides: the permission prompt lives in Springboard;
  the Save Password sheet shows only through `/alert/text`; a search under an element also
  matches the element itself; `mobile: scroll` gave up on a row 150 down after 167 s, so
  scrolling is done by swipes with the idle wait off, then a drag; a view inside an off-screen
  cell reports the first row's position; each table lookup costs about 1.2 to 1.5 s; a slider
  lands within about 5%.
- 2026-11-16, Mac: I5 done. AD-042 "Native iOS mirrors native Android" records the mirror rule;
  the iOS differences are listed once, in `docs/platform-guides/native-ios.md`. Windows: please
  check that `Bruinet.Docs.Samples` and the docs tests still build and pass there.
- 2026-11-16, Windows: the owner cut the pilot to a small proof of concept: J0 on MAUI
  Windows, then native Android, then native iOS. The Mac may start J0 on iOS now with
  [the Mac prompt](200-Mac-Prompt.md). Larger work is on [remaining work](../050-Remaining-Work.md).
- 2026-11-16, Windows: J0 passes twice in a row on MAUI Windows and on native Android, from
  the same feature file. J0 is pushed in `Journeys/Exact.Bouw7.Journeys/Features/Contacts`;
  `Journeys/Exact.Bouw7.NativeAndroid.UITests` is the model for the iOS head. For the Mac:
  the native app logs in by its link `exactbouwacc://login.do?token=` with a portal refresh
  token from `AcceptanceApi.GetPortalRefreshTokenAsync`; saving a new contact returns to the
  list, which shows it only after a pull to refresh; the fixture restarts the app once per
  session, because a kept app reopens on the screen the last run left.