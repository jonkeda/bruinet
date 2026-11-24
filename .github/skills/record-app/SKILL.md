---
name: record-app
description: Open the Scraper's Recorder for a person who clicks through an app themselves, wait until they stop recording, and take what they recorded on to places and page objects - without ever pressing, typing or picking in the page. Use when asked to record a site or an app, to start or open the recorder, when someone says they will click through it themselves, when an app is a production site the walk may not press on (`Environment production`), or when a person pastes the Recorder's sentence "Read the recording … and go on with its pages".
---

# A person presses, you record

On a production site the walk presses only what is known to show more, and a model that presses there is ruled out
(AD-027, AD-043). So a person presses, in the Recorder: a window with the app in a webview. Your part is before and
after: open it on the right project, say where to go, wait, and take the recording on. You are the navigator; the
person is the hands.

Read first: [the Recorder in the Scraper's guide](../../../docs/guides/scraper.md#the-recorder-a-person-presses), and
the app's own Scraper project (`scraper.config.md`; `setup.md` and `modules.md` when it has them).

## 0. Before it opens

1. **The project.** The folder with the app's `scraper.config.md` (`bruinet scrape list`). With more than one, ask
   which. With none, the app is not set up: the `site-setup` or `scraper-setup` skill first.
2. **The host.** The Recorder starts only on a page of one of the config's `## Explore hosts` (`localhost` and
   `127.0.0.1` when none is named). When the site's host is not there, say so and ask before adding it:
   `bruinet scrape config add "Explore hosts" app.example.com`. It is the person's statement that this site may be
   recorded.
3. **The way in.** The tools `scrape_record_*` when this session has them. Else the command line: `bruinet` when it
   is on the path, else the built program, `tools/Bruinet.Cli/bin/Debug/net10.0/bruinet` in Bruinet's repository.
   When the Recorder's own program is not found, build it once
   (`dotnet build tools/Bruinet.Recorder/Bruinet.Recorder.csproj`), or set `BRUINET_RECORDER` to it.

## 1. Open, and say where to go

| With the tools | From a shell |
| --- | --- |
| `scrape_record_open` (`project`, `url`, `account`) | `bruinet scrape record --folder <project> [--url <address>] [--as <account>]` |

It opens **not recording**. Then tell the person, in a few lines:

- what the list of places still owes, most first (`bruinet scrape scraper status`), so they know where to go: pages
  with buttons nobody pressed, forms never opened, a record's tabs;
- to sign in first when the app asks, and only then press **Start recording**: what happens before Start is in no
  file;
- that **Record values** is theirs to switch on, and that with it on a typed value is kept as it is, a password too;
- to press **Stop recording** when they have seen what they wanted.

Do not start the recording for them unless they ask you to: Start is their word that the page on screen may be
recorded.

## 2. Wait for their Stop

| With the tools | From a shell |
| --- | --- |
| `scrape_record_wait` (`minutes`) | `bruinet scrape record wait --minutes 10` |

It answers when they press Stop, with the session and its counts. When the time ran out first (`ended` false, exit
1), wait again: a person clicking through an app takes as long as it takes. `scrape_record_status` in between says
how far they are, when they ask.

A person who opened the Recorder by hand pastes its sentence instead ("Read the recording `<session>` in `<folder>`
and go on with its pages"): start at step 3 with that session.

## 3. Take the recording on

From the app's Scraper project:

```powershell
bruinet scrape places seed
bruinet scrape places read
bruinet scrape scraper status
```

`seed` adds the pages, tabs and dialogs they reached, each with from where and by which press, and the capture it is
read from; `read` fills in their fields and buttons. Then draft from those captures, with no walk:

```powershell
bruinet scrape scraper run --no-walk
```

or, for a project without the factory's setup, `bruinet scrape draft`. From here on it is the `scrape-app` skill.

## 4. Report

In a few lines: the session, how many pages and presses, the places that are new (by kind), what was drafted and
kept, and what still owes: pages nobody went to, a dialog that opened on a press with no label (it has no place).
When more is owed, say where, and offer another round.

What a person found is not found again by itself. When `bruinet scrape places drift` later says a recorded place
changed, ask for that part to be recorded again.

## Never

- Press, type or pick in the Recorder's page, by any means: a script, a debugging port, the screen, a browser tool.
  Not to help, not to "just open the next page".
- Switch **Record values** on, or ask the Recorder to. Off, you may.
- Add a host to `## Explore hosts`, or change another guard of `scraper.config.md`, without the person saying so.
- Read a recording made with values on into the chat beyond what the task needs: its captures may hold what a
  person typed.
- Use the Recorder where the walk may press (acceptance, local): the walk repeats itself after every change, a
  person does not.
