# Handoff board: Windows and Mac

The only status for the native work. Each machine edits its own rows and appends to Events.
Pull before editing, push right after.

## Windows machine (Contacts pilot, steps 0 to 4)

| Item | State | Note |
|---|---|---|
| Pilot step 0: check the ground | Done | Live on Acceptance for MAUI Windows |
| Pilot step 1: journeys and shared support | Done | `Journeys/` in Construction; native iOS head skeleton included, with its head check |
| Pilot step 2: MAUI on Windows | Not started | |
| Pilot step 3: MAUI on Android | Not started | |
| Pilot step 4: native Android | Not started | |
| Generic list and dialog steps in Core | Not started | Mac phase I6 waits on this |
| Scraper Android source | Not started | Mac phase I7 waits on this |
| Handoff to Mac | Not ready | Mac phase I8 waits on this |

## Mac (native iOS plan)

| Item | State | Note |
|---|---|---|
| I0: set up the Mac | Done | Xcode 27.2, Appium 3.9 with XCUITest 12.19, iOS 27 simulator; Construction and `exact-bouw-ios` build. No Java, so the Android projects and the full solution do not build here; launch check moves to I4 |
| I1: skeleton and unit tests | Done | `Bruinet.NativeIos` and its tests in the solution under 2 Platforms/iOS; 83 unit tests pass, Hover and RightClick contracts skip |
| I2: driver, element, readiness, host | Done | Measured live on the simulator; `IosHost` and `IosRecorder` in `Bruinet.Appium`, recorder times from simctl's "Recording started" |
| I3: controls | Done, unproven live | 22 generated controls plus navigation bar, toolbar, alerts, sheets, stepper, table and collection views. DatePicker deferred. Switch, keyboard, alert, sheet, picker-wheel and back-button behaviour to be measured in I4 |
| I4: gallery sample and UI tests | Not started | |
| I5: docs, skills, decision | Not started | |
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
