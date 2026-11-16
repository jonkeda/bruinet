# Pilot step 2: where we are and how to make it small again

Status: decided on 2026-11-16. The proof of concept is cut to one small journey, J0, on MAUI,
then native Android, then native iOS. The rich-text, filter, picker and Bruinet search work is on
[the remaining work list](050-Remaining-Work.md). This belongs to
[the Contacts pilot](030-Contacts-Pilot.md).

## What I was trying to do

Step 2 of the pilot says the MAUI app on Windows is done when three journeys and the share
scenario pass twice in a row, live against Acceptance. I followed that bar literally, so I
was trying to make J1 to J4 pass in full: create a client with a rich-text comment, change
the comment, filter and mark favourites, create a project for the client, and share.

Only J1 passes now, twice in a row. J2 fails on the filter sheet, J3 fails on the client
picker in the project form, and I have not tried J4 (share) yet.

## Where the hours went

Almost all the time went into J1's rich-text comment, not into the journey idea. The comment
editor and its preview are web pages inside the app (WebView2), and Windows UI Automation
handles them badly. One problem led to the next:

- Windows cannot tap a plain tap gesture, so the comment's edit icon and the editor's Save
  button needed the app's tap opt-in.
- UI Automation cannot type into or read a web page, so the test now writes and reads the
  comment through the browser's debugging port.
- Closed editors keep their web pages alive, which once made a failed save look like a pass.
- Once the field shows a comment, a search through the window no longer finds the edit icon,
  because the search goes off course inside the web page. That is a bug in how Bruinet looks
  things up on Windows. I fixed it in Bruinet and was checking that fix.

The Construction test suite has never automated typing rich text. The Custom Attributes module
records this as an accepted gap. So J1 put the hardest control on the hardest platform at the
very start.

## What I made too complex

- **The finish line was too far for a proof of concept.** Four scenarios use a filter sheet,
  favourites, pickers, a project form, rich text and share. A proof of concept needs one
  journey that runs the same way on two apps.
- **I put rich text in the first journey.** It is the hardest field we have, and it says
  nothing about whether one feature file can drive several apps.
- **I fixed framework gaps in the middle of the pilot.** I fixed each problem in the app, the
  test head or Bruinet instead of writing it down as a finding and moving on.
- **I set no time limit and kept no log.** The pilot plan asks for one log line per work
  session, and I did not keep it, so I cannot give exact hours.

## The proposal: a small proof of concept

One journey, two apps, a time limit:

1. **One simple journey:** create a contact with a name and a type, find it in the list, open
   it and check the name. No comment, filter, favourites, project or share.
2. **Two apps:** the MAUI app on Windows, which already does all of these steps, and then
   native Android. The proof is that the same feature file passes on both.
3. **Half a day per blocker:** when a step needs more than that, it becomes a finding in the
   pilot report and is skipped.
4. **J1 to J4 stay** in the feature file as the later target. They don't run in the proof of
   concept.

This would finish step 2 almost straight away, and step 4 (native Android) becomes the real
test of the idea.

## What to do with the work already done

None of this is committed yet.

| Work | Proposal |
|---|---|
| Tap opt-ins on the comment's edit icon and Save, with their pin test | Keep: two small attributes, already tested |
| Typing and reading web pages through the browser debugging port | Park in the MAUI head, unused by the simple journey |
| Bruinet fix for searches through a web view | Park on a branch until a Bruinet test covers it |

## Decisions (2026-11-16)

- The proof of concept is one small journey, J0, on MAUI Windows, then native Android, then
  native iOS.
- The Bruinet search fix is parked as a patch, with its test, on the remaining work list.
