# AD-024: A Recording Is A Paced Gate Run

A narrated video of a scenario (the narrator) is made by the engine the xUnit gate runs, not by
the tool that asks for it:

| Decision | What holds |
| --- | --- |
| **The runner records** | A gate run with `BRUINET_RECORD` records its scenes (`Bruinet.Uat` through `Bruinet.Narrator`). Visual Studio, VS Code, the MCP tool `record_video` and CI only set the variables and run `dotnet test`; Presenter asks its suite's runner process for the same pacer and recorder (AD-025). One engine, so a recording shows what the gate does. |
| **Timing is a seam, not a wait** | `BddExecutionContext.Pacer` (`IBddStepPacer`) is waited on before and after each step; it is null unless something records. The voice is made before the run, so each line's length is known; a line plays while its step runs (`Say \| before`: before it), the run only takes as long as the speech, and the voice is laid in afterwards at its recorded time. The second seam is the call model: while a scene records, `ControlCalls.Act` holds each outermost action until it has taken the scene's pause (`ActionPacing`, set beside the interaction log), outside the call's logged time, so a step that does several things shows each. |
| **Narration lives in a video script** | `*.video.md`: scenes, each a scenario address, an intro, `Step | Say` rows and an outro. A feature file stays a test. The script is checked like a feature file (AD-022's one checker, `VID0xx`). |
| **Capture is not input** (AD-005) | A recorder reads what is already drawn: Windows.Graphics.Capture for a window (it records a covered one), Chromium's screencast for a page, `adb screenrecord` for a device. It never focuses, activates or moves the app. The screen-region recorder, which records whatever covers the area, is only the last resort. |
| **One clock, separate outputs** | Every time is on the `Stopwatch` clock, so a step's time in `timeline.json` is its time in the video. The video, the `.srt` captions and the timeline are separate files; the captions are never burned in. ffmpeg encodes, lays in the voice and joins the scenes; Record checks for it first. |
| **Touch marks are logged facts, drawn when the video is made** | While a scene records, each driver action reports itself to `Interactions` (`Bruinet.Core.Recording`) just before it runs: its kind and its target's bounds, in the driver's coordinates (on Windows physical pixels, read per-monitor aware whatever the test process's DPI awareness, as the frames are), and a swipe's points. The FlaUI, Appium and Playwright elements report from their own action methods, outside their retries, and an action that runs another is one mark; a target the tree cannot show (a `RefreshView`) has its bounds from the bridge's `GetBounds`. The recorder says which area of those coordinates its frames show (`RecordingResult.Source`). The scene's `timeline.json` keeps them (`interactions`, `source`); the composer draws the marks from them as an ASS track that ffmpeg's `subtitles` filter burns into the made video only. A kept scene's video stays clean, and the script's `Touches` and `TouchColor` are not in its hash: they are composition. |
| **Scenes can be kept** | A script's scenes can stay in `<Recordings>\<script>.scenes` (`BddKeptScenes`; Presenter's Narrator mode, or `BRUINET_RECORD_KEEP` on the gate). Each scene's `timeline.json` holds a hash of what it was recorded from (its lines, its scenario's steps, the voice, the beat), so a scene is known to be recorded, failed or out of date, one scene is recorded again without the rest, and the video is stitched from the latest recording of each. |
| **A voice names its synthesizer; a cloud voice's key never enters the repository** | `azure:<voice>` is Azure OpenAI text to speech (`AzureOpenAiSpeech`, in `Bruinet.Narrator`: it needs only HTTP), or an audio chat model (a deployment named `*audio*`) told to read the line, whose transcript must hold nearly all the line's words in order or the line is asked again, then fails; `azure-speech-service:<voice>` is Azure AI Speech (`AzureSpeech`: SSML over HTTP, a style as `mstts:express-as`, the key in `Ocp-Apim-Subscription-Key`, 24 or 48 kHz), whose settings are the `azureSpeech` object of the same file (Azure OpenAI's its `azureFoundry`) and `BRUINET_SPEECH_*`; any other name is a platform voice. With none named, Azure OpenAI comes first when it is set up, then Azure Speech. Voices are chosen by service, and the two cloud voices share the clip cache, the WAV header fix and the retry on a busy service (`CloudVoice`). A named cloud voice without settings fails the recording; it never becomes another voice. Its key comes from `BRUINET_VOICE_KEY` or a `voice.secrets.json` that git ignores (a suite's folder up to the repository root, then `%USERPROFILE%\.bruinet`), and goes only into the request's header: never a log, a timeline, a hash or a message. A script's `Instructions` (the tone) is part of a scene's hash. |
| **A line is paid for once, on every machine** | A cloud voice's clips are committed beside the suite (`ClipLibrary`, `speech\<key>.opus`), on top of the local cache, which keeps its own names, so a clip paid for earlier moves in free. The key holds what changes what is said and leaves out the endpoint, so another region finds it. The folder is found from the suite's project, not its output. Opus at 32 kbit/s, because a clip stays in the history for good: a twelfth of a WAV, and no difference was heard once it is the video's AAC. The mp4 stays out of git, and a scene folder's copies too. |
| **Platform recorders are loaded, not referenced** | A recorder or voice that needs a platform's SDK (`Bruinet.Narrator.Windows` holds both for Windows) declares itself with an assembly attribute and is found in the run's folder or in `BRUINET_RECORDERS`: a `net10.0-windows` suite cannot reference a Windows 10 SDK package. |

**Why:** a video made by another runner would show a run the gate never did, and delays tuned by
hand drift from the speech and slow every test run. Evidence (at 9f27d29d):
[design.md](https://github.com/exact-mobile-apps/test-tooling/blob/9f27d29d/.my/narrator/design.md),
[screen-capture.md](https://github.com/exact-mobile-apps/test-tooling/blob/9f27d29d/.my/narrator/screen-capture.md),
[the Narrator mode](https://github.com/exact-mobile-apps/test-tooling/blob/a8b3fc75/.my/narrator/presenter/design.md) (at a8b3fc75).

**Ruled out:**
- a host recording the screen itself, or a separate runner for videos;
- waits or `Say:` lines in feature files for narration, and a spoken template for steps a script
  does not narrate (they run silently);
- a pacer, or any narration cost, on a test run that does not record;
- activating or bringing the app forward to record it; the region recorder as the default;
- captions burned into the video;
- a real or drawn mouse cursor for touches (MAUI on Windows has no pointer, a phone none at all),
  Android's "Show taps", or an overlay drawn by the app (app-side automation, AD-008);
- a decorator around `IMauiElement` for the log: its members with default bodies would run the
  default, not the element's, and a member added later would silently go unlogged.

**Broken when:**
- a scenario or step definition waits for a voice, or a plain `dotnet test` run is slower after
  this change;
- `ActionPacing.Pause` is set outside a scene that records, a read or an assert pauses, or an
  action's pause is counted in its logged time;
- `BddExecutionContext.Pacer` is set without `BRUINET_RECORD`, Presenter's Record, or a rehearsal
  in Presenter's Narrator mode with Speak aloud on;
- a kept scene is stitched as current when its lines, steps, voice or beat changed since;
- a recorder calls `SetForegroundWindow`, focuses an element or moves a window;
- a recording's step times disagree with its video, or its captions are in the pixels;
- a run that does not record reads an element's bounds for the log, or a kept scene's
  `video.mp4` has marks in it;
- a tap's mark is drawn after the screen has changed, or away from its control;
- a line whose words, voice and tone did not change is asked of the service while a clip of it is in the suite's library;
- a clip is written to a suite's output folder, or a recording cannot say what it reused and paid for;
- a cloud voice's key is read from a script, written anywhere but its request's header, or a
  `voice.secrets.json` is committed.
