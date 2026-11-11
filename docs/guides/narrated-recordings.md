# Narrated Recordings

Record a scenario, or a whole video script of scenarios, on video with a voice-over that says
what is happening. The scenarios run as the gate runs them (AD-024); the app is recorded while
they run, each spoken line plays while its step runs, and each action is held long enough to be
seen ([Pacing](#pacing)), so the voice and the app stay in step. Each tap, swipe and key the test makes is marked on the video ([Touch Marks](#touch-marks)).
Record from Visual Studio, VS Code, Presenter, an AI agent (MCP) or `dotnet test`.

## You Need

- **ffmpeg**, on `PATH` or named by `BRUINET_FFMPEG`: it encodes the video, lays in the voice and
  joins the scenes. Windows: `winget install Gyan.FFmpeg.Essentials`, then a new terminal. Record
  checks for it first and says so when it is missing.
- **A voice**: a Windows voice (Settings, Time and language, Speech), or a cloud voice
  ([Cloud Voice](#cloud-voice)). Without one, lines are timed from their words and shown as
  captions only.
- A suite whose test class uses `[UatScenarioTheory]`, so scenarios run by their address.

## A Video Script

`*.video.md` beside the feature files (copied to the output with them:
`<None Update="Features\**\*.video.md" CopyToOutputDirectory="PreserveNewest" />`):

```markdown
# Video: How the Todo app works

| Field | Value |
| --- | --- |
| Voice | Microsoft Zira |
| Beat | 600 |
| Pause | 700 |

## Scene: Opening the app

Scenario: Features/TOD.01-see-my-todos.feature.md#TOD.01.1-Opening-the-app-shows-the-list-a-loading-state-shows-first-then-the-content

This is Todo: a small app to keep track of what you have to do.

| Step | Say |
| --- | --- |
| Given I am on the Todo List page | The app opens on your list. |
| When I tap Sync | You tap Sync to fetch it. |

That was the list.
```

| Part | Means |
| --- | --- |
| `# Video:` | the video's title |
| `Voice`, `Beat` | the voice by (part of) its name (`azure:coral` for Azure OpenAI, `azure-speech-service:en-GB-Emily:MAI-Voice-2.1` for Azure Speech), and the wait after each step in ms; the settings when left out |
| `Pause`, `Say` | the least time an action takes in ms (0 to 5000), and `while` (the default) or `before`: when a step's line is said ([Pacing](#pacing)) |
| `Instructions` | how a cloud voice says the lines: its tone, or for Azure Speech a style name (`narrator`); the settings when left out |
| `Touches`, `TouchColor` | the touch marks: `on` (the default), `off` or `outline-only`, and their colour as `#RRGGBB`; the settings, then Exact red `#E8111E`, when left out ([Touch Marks](#touch-marks)) |
| `## Scene:` | a chapter; one scenario, run and recorded |
| `Scenario:` | the scenario's address: its file relative to the suite's config, `#`, its name with every run of other characters as one `-` (the `BddScenario` trait) |
| paragraph before the table | said before the first step |
| `Step`, `Say` | said as that step runs (or before it, with `Say \| before`); the step as the scenario writes it, keyword included (a `## Step:` call by its call line). A step with no row runs silently |
| paragraph after the table | said after the last step |

The preview shows the script as it will play: every step of each scene, with its line or
*silent*. It is checked like a feature file: an address or a `Say` row that finds nothing is an
error (`VID011`-`VID013`), a row out of step order a warning (`VID014`); markup in a line has its own
codes ([Markup In A Line](#markup-in-a-line)).

## Pacing

Three waits keep a recording watchable; none of them is in a test run that does not record.

| Wait | When | How long |
| --- | --- | --- |
| The line | a step's line starts as the step starts; the next line, or the outro, waits for it | the line's length |
| The pause | after each control action: a click, a choice, text set or cleared, a key | until the action has taken `Pause` (700 ms by default); a slower action waits nothing more |
| The beat | after a step and its line | `Beat` (600 ms by default) |

The pause is per action, not per step, so a page-object method that opens a list and picks from it
shows both. An action that runs another (an Invoke as a Click) pauses once, reads and asserts never
pause, and the pause is not part of the call's own time in the log. A step without a line of its
own follows its beat while the line before it still plays, so a state that lasts a moment (a
loading state after a tap) is still there to check. `Say | before` keeps the older
order: each line is said, then its step runs in silence. A scene's kept recording is out of date
when `Pause` or `Say` change.

## Markup In A Line

A `Say` cell, or a scene's intro or outro paragraph, may hold SSML inline: how a line is said, not only what:

```markdown
| When I tap Save | and save.<break time="400ms"/> Your <emphasis level="strong">todo</emphasis> is on the list. |
| Given I am on the list | Welcome to <sub alias="Brew-ee-net">Bruinet</sub>, in <say-as interpret-as="cardinal">3</say-as> steps. |
```

A script with no markup works as before. The narrator writes the SSML document (`speak`, the language, the voice) from the
script's `Voice`, so a line holds only its inside. A line is markup when it holds a known element; `x < 5` and `Tom & Jerry`
are plain text, and a tag-like word that is no element (`<brek/>`) is a warning and is never spoken. Captions, the
timeline, the review page and the preview always show the plain words (a `sub` shows its text, not its alias); the preview
marks a line that has markup.

| Element | Means |
| --- | --- |
| `break` (`time="500ms"`, `"2s"`, up to 20 s; or `strength`) | a pause |
| `emphasis` (`level`), `prosody` (`rate`, `pitch`, `volume`) | stress; speed, pitch, loudness |
| `say-as` (`interpret-as`), `sub` (`alias`), `phoneme` (`ph`, IPA) | how a number, a name, a word is read |
| `p`, `s`, `lang` | a paragraph, a sentence (both pause), a foreign phrase |
| `mstts:express-as`, `mstts:silence` | Azure Speech's style for the line (it wins over the script's `Instructions`), and a silence |

Not every voice follows every element. The check knows by the voice the script names, and says so before recording:

| Voice | Follows |
| --- | --- |
| A Windows voice | `break`, `emphasis`, `prosody`, `sub`, `phoneme`, `p`, `s` (measured; `say-as`, `lang` and the `mstts:` elements are dropped) |
| Azure Speech, a neural voice | every element above |
| Azure Speech, a MAI voice | the same except `break` (use `p` or `s` for a pause) and `phoneme` |
| Azure OpenAI (`azure:`) | none: its line is the plain words, a `sub` said as its alias |

An element a voice does not follow is dropped and its words are kept (`VID023`), never spoken. With no voice named in the script
the settings decide at record time, and the check says nothing.

| Code | Says |
| --- | --- |
| `VID020` | the markup is not well-formed: an element never closed or closing nothing |
| `VID021` | warning: a tag-like word is no SSML element, so it is not spoken (write `&lt;` to say a `<`) |
| `VID022` | an attribute value the voices do not know: a break over 20 s, an unknown `prosody` rate, a `sub` with no alias |
| `VID023` | warning: the script's voice does not follow an element; it is dropped, its words kept |
| `VID024` | an element where it may not go: inside `say-as`, `sub` or `phoneme` |
| `VID025` | `pronunciations.md` lists a term twice, or a row has nothing to say |

### Pronunciations

A suite lists how its names are said once, in `pronunciations.md` beside `uat.config.md`, so no script is cluttered with tags:

```markdown
| Term | Say | IPA |
| --- | --- | --- |
| Bruinet | Brew-ee-net | |
| Todo | | ˈtuːduː |
```

Before a line is spoken each whole-word, case-sensitive occurrence of a term in its plain text becomes `<sub alias="Say">`, or a
`<phoneme>` when only an IPA is given; a term inside a tag the author wrote is left alone. The alias reaches every voice, one that
follows no markup included. A scene kept earlier is out of date when a term changes, and not before: a suite with no
`pronunciations.md` keeps its scenes' hashes.

Example: [samples/Todo/.../VideoScripts/how-todo-works.video.md](../../samples/Todo/tests/Bruinet.Samples.Todo.Uat/VideoScripts/how-todo-works.video.md).

## Record

| From | How |
| --- | --- |
| Visual Studio, VS Code | **Record** on a video script's title in the Scenario Preview, or beside **Run** on a scenario heading. It builds, runs, and offers **Watch** when the video is made. The preview lists a file's recordings to watch |
| Presenter, a whole script | **Narrator mode** (the video camera): open the script, **Record** the checked scenes, **Stitch**. Scene by scene: a scene recorded again replaces that scene only ([Presenter](../platform-guides/presenter.md#narrator-mode)) |
| Presenter, one run | **Record** (the dot) in Present mode's run toolbar: each run is recorded while it is on, narrated by the scene a video script has for the scenario. **Watch** opens the last one |
| An agent | MCP `record_video` (a script or a scenario), then `review_page`; the `narrated-review` skill |
| `dotnet test` | the variables below, with a filter on the scenes' addresses |

```powershell
# From the suite's project folder
$env:BRUINET_RECORD = "VideoScripts\how-todo-works.video.md"
$env:BRUINET_RECORDERS = "..\..\..\..\tools\Bruinet.Cli\bin\Debug\net10.0\recorders"
dotnet test --filter "BddScenario~Features/TOD.01-see-my-todos.feature.md#TOD.01.1"
```

| Variable | Says |
| --- | --- |
| `BRUINET_RECORD` | a video script (full, or relative to the suite's folder) records its scenes; `1` records every scenario, silently |
| `BRUINET_RECORD_DIR` | the Recordings folder; `TestResults\<run>` when not set (the IDEs and Presenter: `TestResults\recordings`) |
| `BRUINET_RECORD_OUT` | the scenes folder, for a host that names the video |
| `BRUINET_RECORD_KEEP` | with a script: its scenes are kept in `<Recordings>\<script>.scenes` when the video is made, so a scene can be recorded again on its own, here or in Presenter's Narrator mode, and the video stitched again |
| `BRUINET_VOICE`, `BRUINET_BEAT`, `BRUINET_PAUSE` | the voice, the beat (ms) and the action pause (ms), when the script does not say |
| `BRUINET_VOICE_INSTRUCTIONS` | a cloud voice's tone, when the script does not say |
| `BRUINET_VOICE_LIBRARY` | the folder of a suite's [voice clips](#voice-clips), when it is not `speech` beside its project |
| `BRUINET_VOICE_REUSE` | `1`: a line with no clip fails the recording instead of being paid for |
| `BRUINET_TOUCH_COLOR` | the touch marks' colour (`#RRGGBB`), when the script does not say |
| `BRUINET_VOICE_KEY`, `BRUINET_VOICE_ENDPOINT` | a cloud voice's key and endpoint, over the file's `azureFoundry` object ([Cloud Voice](#cloud-voice)) |
| `BRUINET_SPEECH_ENDPOINT`, `BRUINET_SPEECH_KEY`, `BRUINET_SPEECH_VOICE`, `BRUINET_SPEECH_SAMPLE_RATE` | Azure Speech's settings, over the file's `azureSpeech` object ([Azure Speech](#azure-speech)) |
| `BRUINET_RECORDERS` | a folder with `Bruinet.Narrator.Windows` and the window recorder: a `net10.0-windows` suite cannot reference them, so the language server keeps a copy in its `recorders` folder |
| `BRUINET_FFMPEG` | ffmpeg's executable, when it is not on `PATH` |

## Cloud Voice

Azure OpenAI's text to speech (`gpt-4o-mini-tts`), or an audio chat model such as `gpt-audio`, speaks the lines when a script, `BRUINET_VOICE`
or the IDEs' Voice setting names `azure:<voice>` (`azure:coral`, `azure:alloy`, `azure:sage`, ...),
or names no voice while a cloud voice is set up. It sounds the same on every machine and works for
every platform's runs. A script can set its tone:

```markdown
| Field | Value |
| --- | --- |
| Voice | azure:coral |
| Instructions | calm and friendly, a product demo |
```

Its endpoint and key go in `voice.secrets.json`, which git ignores. The Presenter writes it for you
(Settings, Narrator, Cloud voice: [the Presenter](../platform-guides/presenter.md#record)), with
Try to hear it first; or write it by hand, as `voice.secrets.example.json` at the repository root
shows. Its settings are the file's `azureFoundry` object:

```json
{
  "azureFoundry": {
    "endpoint": "https://<resource>.openai.azure.com",
    "deployment": "gpt-4o-mini-tts",
    "apiVersion": "2025-03-01-preview",
    "key": "<the resource's key>",
    "voice": "coral"
  }
}
```

The endpoint is the resource; a URL copied from Foundry with its path (`/openai/...`) works too, a
Realtime URL (`wss://`) does not.

**An audio chat model.** A deployment whose name says `audio` (`gpt-audio`, `gpt-audio-1.5`,
`gpt-4o-audio-preview`), or an endpoint copied from a chat completions URL, is asked through chat
completions (`/openai/v1/chat/completions`) to read each line as written, with the script's
`Instructions` as its tone. A chat model may reword or answer a line, so what it says it said is
checked: nearly all the line's words, in order (numbers may come back spelled out). A misread line
is asked again, twice; then the recording fails, quoting what it said. For a script that must be
read as written, text to speech is the surer voice. Its voices are those of the Presenter's list
(`marin` and `cedar` are its newest).

The first of these that has an endpoint and a key is used:

1. `BRUINET_VOICE_KEY`, `BRUINET_VOICE_ENDPOINT` (and `BRUINET_VOICE_DEPLOYMENT`,
   `BRUINET_VOICE_API_VERSION`): for a build agent, from its secret store; they override the file.
2. `voice.secrets.json` beside the suite's `uat.config.md`, or in a folder above it up to the
   repository's root.
3. `%USERPROFILE%\.bruinet\voice.secrets.json`: one key for every repository, outside them all. The
   place to start with. (A repository other than Bruinet ignores the file only if its own
   `.gitignore` says so: `**/voice.secrets.json`.)

The key goes only into the request's `api-key` header; it is never in a log, a timeline or a
message. A line is asked for once, on every machine: see [Voice clips](#voice-clips). A recording that
names a cloud voice fails when none is set up, or when the service refuses (the message says
why); it never falls back to another voice. `BRUINET_VOICE_INSTRUCTIONS` gives the tone when the
script does not.

## Voice clips

A cloud line is paid for once. Its clip goes into the suite's `speech` folder, `<key>.opus`, which you commit; the mp4 stays
out of git. Recording a video again records the UI only: the clips set the pace, and the voice is laid in afterwards.

A voice looks for a line in three places:

1. the suite's `speech` folder, which costs nothing;
2. `%LOCALAPPDATA%\Bruinet\speech-cache`, which costs nothing, and the clip is added to the folder;
3. the service, the only step that costs; the clip goes into both.

A clip is named by the service, the voice, the tone, the sample rate (Azure Speech) or deployment (Azure OpenAI) and the
words, after pronunciations. The endpoint is not in the name, so another region or machine finds it. One reworded line costs
one clip, and a line moved to another step or script costs nothing. A changed line still makes its scene out of date,
because its length sets the scene's timing. A Windows voice is free and keeps no clips.

The folder is `speech` beside the suite's project file, whichever folder the run starts in; `BRUINET_VOICE_LIBRARY` names
another. A clip is Opus at 32 kbit/s, about a twelfth of its WAV, because each one stays in the repository's history;
ffmpeg needs libopus for it.

A recording's `.timeline.json` and `record_video` say what it did:

```text
Speech: 46 lines, 45 reused, 1 asked (38 characters)
```

With `BRUINET_VOICE_REUSE=1` a line found nowhere fails the recording instead of being paid for.

`bruinet voice` goes through the lines of a test project's scripts without recording (`--project <folder>`, default
the current one):

| Command | What it does |
| --- | --- |
| `bruinet voice list` | lists the lines with no clip and their characters; exit 1 when there are any |
| `bruinet voice fill` | adds what the local cache holds, for free |
| `bruinet voice buy` | as `fill`, and buys the rest; asks first, `--yes` skips the question |
| `bruinet voice prune` | deletes the clips no script says any more; asks first, and refused when a script has errors |
| `bruinet voice index` | writes `speech-clips.md` beside the scripts: per video, each line, where it is said, and its clip |

The clip names are hashes, so `index` is how a person sees which clip says what. Run `fill` and `index` after
recording and commit the lists with the clips; a reworded line changes its row.

## Azure Speech

Azure AI Speech, the service with a named neural voice for each language and region, speaks the lines when a
script, `BRUINET_VOICE` or the IDEs' Voice setting names `azure-speech-service:<voice>`: the voice's name as
the service gives it, after the prefix. A neural voice (`azure-speech-service:nl-NL-FennaNeural`) and a MAI voice
(`azure-speech-service:en-GB-Emily:MAI-Voice-2.1`, the model after a colon) are named the same way; the name goes
to the service whole, and its first two parts are the language. It is the second cloud voice: the same voice
every time, billed per character with a free tier (the service's pricing page has the current prices).
`azure:` names stay Azure OpenAI's.

```markdown
| Field | Value |
| --- | --- |
| Voice | azure-speech-service:en-GB-Emily:MAI-Voice-2.1 |
| Instructions | narrator |
```

`Instructions` is a **style**, not free text: `narrator`, `educational` or `audiobook` suit a voice-over, and
Emily and Harry (`en-GB-Harry:MAI-Voice-2.1`) both have them. Once the voices list has been loaded (the
Presenter's **Load voices**), the script check warns, as VID030, when the voice lacks the style. Use
`MAI-Voice-2.1`, not `-Flash`, for a video: the long-form model's fidelity matters there, its latency does not.
The MAI voices are in public preview, with no service-level agreement; a video that is published should use a generally
available neural voice, named the same way.

The settings are the `azureSpeech` object of the same `voice.secrets.json`, found in the same places, beside the Azure OpenAI
voice's `azureFoundry`; saving one never changes the other:

```json
{
  "azureSpeech": {
    "endpoint": "eastus2",
    "key": "<the Speech resource's key>",
    "voice": "en-GB-Emily:MAI-Voice-2.1",
    "sampleRate": 24000
  }
}
```

The endpoint is a region (`eastus2`), the resource's URL, or a URL pasted with a path (the path is dropped).
`sampleRate` is `24000` (the default, the Azure OpenAI voice's rate) or `48000`; anything else is refused.
`BRUINET_SPEECH_*` override the file. The key goes only into the `Ocp-Apim-Subscription-Key` header. A line is
asked for once, as for the other voice; the clip's name holds the service, the voice, the style, the sample rate
and the text. With no voice named, Azure OpenAI is used when it is set up, else Azure Speech, else a Windows
voice. A named Azure Speech voice fails when it has no settings, and when the service answers 401 (the key is wrong or
belongs to another region), 400 (check the voice's name) or stays busy after a few asks.

## Touch Marks

The video shows what the test pressed, the same way on Windows, Android and in a browser: the
app is not changed and nothing moves a pointer (AD-005). While a scene records, each action the
test runs is logged with its target's bounds, taken just before it runs, into the scene's
`timeline.json` (`interactions`). The marks are drawn from that log when the video is made, so a
tap's ring can close in a quarter of a second *before* the tap, while the screen still shows
what is about to be pressed.

| The test | The video |
| --- | --- |
| a tap, a toggle, a choice in a list or picker | a ring closes onto the control, then a dot and a ripple; the control is outlined |
| a double tap, a long press | two ripples; a ring growing while the press is held |
| a swipe, pull to refresh | a dot travels the swipe's way with a fading trail, then an arrow |
| text set or cleared | the field is outlined, with a keyboard chip (the text itself shows in the app) |
| Enter, Back, the flyout menu | a chip in the bottom-right corner: `⏎ Enter`, `← Back`, `☰ Menu` |

Each mark is the colour (`TouchColor`, the settings, `BRUINET_TOUCH_COLOR`, else Exact red
`#E8111E`) over a white stroke and a dark halo, so it reads on light and dark pages, and is sized
from the video's height. `Touches | outline-only` draws only the outlines and chips; `off` none.

The marks are drawn into the made video only: a kept scene's `video.mp4` stays clean, and
`Touches` and `TouchColor` are not part of a scene's hash, so changing them needs no new
recording, only **Stitch** again. Scrolling to a control is not marked. Drawing needs an ffmpeg
with libass (`ffmpeg -filters` lists `subtitles`); without it, the video is made unmarked, with
a warning.

## What You Get

In the Recordings folder, for a script `how-todo-works.video.md`:

| File | Holds |
| --- | --- |
| `how-todo-works.mp4` | the scenes joined, the voice laid in, a chapter per scene; a new recording replaces it |
| `.srt` | the captions: a cue per spoken line, and one after a failed step |
| `.timeline.json` | each scene's steps (start, end, result, message), lines and interactions, on the video's clock |

The video is made when the last scene of the script has run; a failed scene is recorded as it
happened and marked in the timeline. A scenario recorded on its own is named after it.

Kept scenes (Narrator mode, or `BRUINET_RECORD_KEEP`) stay in `<script>.scenes\scene-NN`: the
scene's `video.mp4`, its spoken lines and its `timeline.json`, which holds what the scene was
recorded from. A scene whose lines, steps, voice, beat, pause or `Say` changed since is out of date.

## Recorders

| App | Recorded with | Note |
| --- | --- | --- |
| MAUI (Windows), WPF, WinForms | Windows.Graphics.Capture | records the window while it is covered; a minimized window gives no frames; Windows draws a yellow border around it |
| A Chromium page (Playwright, Blazor) | Chromium's screencast | headless or not |
| Android | `adb shell screenrecord` | restarted every 3 minutes and joined; a join may miss a fraction of a second |
| Anything else on Windows | ffmpeg's screen region | the last resort: it records whatever covers the area |

Recording is not input (AD-005): no recorder focuses, activates or moves the app.
