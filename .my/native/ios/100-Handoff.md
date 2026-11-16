# Handoff board: Windows and Mac

The only status for the native work. Each machine edits its own rows and appends to Events.
Pull before editing, push right after.

## Windows machine (Contacts pilot, steps 0 to 4)

| Item | State | Note |
|---|---|---|
| Pilot step 0: check the ground | Not started | |
| Pilot step 1: journeys and shared support | Not started | |
| Pilot step 2: MAUI on Windows | Not started | |
| Pilot step 3: MAUI on Android | Not started | |
| Pilot step 4: native Android | Not started | |
| Generic list and dialog steps in Core | Not started | Mac phase I6 waits on this |
| Scraper Android source | Not started | Mac phase I7 waits on this |
| Handoff to Mac | Not ready | Mac phase I8 waits on this |

## Mac (native iOS plan)

| Item | State | Note |
|---|---|---|
| I0: set up the Mac | Not started | |
| I1: skeleton and unit tests | Not started | |
| I2: driver, element, readiness, host | Not started | |
| I3: controls | Not started | |
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
