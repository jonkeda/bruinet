# Plan: pay for a spoken line once, on every machine

Checked against Bruinet `70fb139b`, Tomorrow and Exact.Construction on 2026-11-06. Paths are Bruinet's unless they say otherwise.

## Outcome

A line whose words did not change is never asked of Azure again, on any machine: recording a video again re-records the
UI only. The clips live in git beside the suite, the mp4s stay ignored, and each recording says what it reused and what
it paid for.

## What the code does today

The first request said the recorder always makes the sound again. That holds for part of it only.

- **The UI side already works as asked.** Clip lengths are known before the run, the pacer waits for them, and the voice
  is laid in afterwards at its recorded time (`BddNarrationPacer`, `BddVideoComposer.Arguments`). A new UI recording needs
  the old clips only, not new speech.
- **On one machine a cloud line is already asked once.** `CloudVoice.ClipAsync` looks in
  `%LOCALAPPDATA%\Bruinet\speech-cache` by a hash of the voice and text, per line; a test proves it
  (`ALineSaidBefore_IsTakenFromTheCache_AndAnotherToneIsAskedFor`).
- **That cache is the weak point.** It belongs to one user on one machine, and the copies in the scene folders are deleted
  when the video is composed.

Where money is spent again today:

| Case | Why |
| --- | --- |
| Another machine, a build agent, a cleared profile | The cache is empty, so every line of the script is asked again |
| A new endpoint or region | The cache name holds `_secrets.Endpoint` / `_secrets.Host`, so the same voice and text miss |
| Presenter's rehearsal (`PresenterRecorder.RehearseAsync`, line 184) | `Speech.Find(voice)` passes no instructions, so it pays for other clips than the recording uses, in another tone |
| Not visible at all | Nothing reports reused or paid lines, and the cache holds no text, so a bill cannot be traced to a line |

The Windows voice has no cache and does make its sound each time, but it is free, so it stays out of this plan.

Measured:

- The speech cache holds 840 clips, 117 MB, 750 of them from this month; 676 were made on 2026-11-04.
- Exact.Construction has 34 tracked scripts with 733 step lines, about 29,000 characters, intros and outros on top. That
  matches the cache: those lines look to have been paid for once. The cache holds no text, so this is an inference.
- Neither `azure-pipelines-dev.yml` nor `azure-pipelines-release.yml` records yet. Construction records through
  `scripts/record-module-video.ps1`.

## Where it stands (2026-11-06)

Tasks 1-8 are built, and task 9 as far as it goes. A recording of `todo-in-a-minute-azure-speech` with
`BRUINET_VOICE_REUSE=1` passed: `Speech: 10 lines, 10 reused, 0 asked (0 characters)`, with the voice in the video.

- `bruinet voice` on Construction: 805 lines, all in the local cache, none to buy. That confirms the cache worked there.
- Tomorrow's scripts use the Windows voice, so they have no clips to commit yet. `platform-studio.video.md` has errors (its
  scenes name `PLT.11-thing-bench`, which is gone), so `--prune` refuses there until it is fixed.
- The Todo sample's other 60 lines are in no cache (about 1,900 characters); `--ask` would buy them. Not bought.
- Left: task 10, in Construction's own repository, once it takes this Bruinet. Task 11 waits for a pipeline.

## Why un-ignoring is not enough

Removing the ignore rule alone commits nothing. No clip stays in the repository: the WAVs are in `%LOCALAPPDATA%` and in
`.run` folders that are deleted once the video is made. The clips need a home inside the repository, and the voices need
to look there first.

## Design

**A clip library per suite.** `<suite source>\speech\<key>.opus`, committed. It sits on top of the cache, which stays as
it is:

| Order | Found in | Then | Cost |
| --- | --- | --- | --- |
| 1 | the library | decode to the scene's `line-<n>.wav` | none |
| 2 | the local cache (its current name) | copy to the scene, add to the library | none |
| 3 | nowhere | ask the service, write cache and library | the line |

Step 2 is what saves today's 840 clips from being bought again, and it needs no migration code: the cache keeps its names.

**The key.** The library's key drops the endpoint, so a new region or another person's resource still finds the clip.

| Voice | Library key (`CloudVoice.Hash`) |
| --- | --- |
| `AzureSpeech` | `azure-speech-service`, voice, style, sample rate, what is said |
| `AzureOpenAiSpeech` | `azure-openai`, deployment, voice, instructions, text |

The key is per line, after pronunciations are applied. One reworded line costs one clip, a line moved to another step or
script costs nothing, and a changed term re-asks only the lines that hold it. No secret goes into a key
(`TheCacheKey_HoldsNoSecret` gets a library twin).

**Where the library is.** Under `dotnet test`, `ConfigDirectory` is the `bin` copy, because `uat.config.md` is copied to
the output. A library there would be lost on the next clean. So: `BRUINET_VOICE_LIBRARY` if set, else
`BRUINET_SUITE_SOURCE`'s `speech`, else `speech` in the nearest folder above the config that holds a `*.csproj`.

**Scenes do not change.** `BddKeptScenes.Hash` stays. A changed line still marks its scene out of date, which is right:
the line's length sets the scene's timing, so its UI must be recorded again. Only the unchanged lines come free.

**What a recording says.** `BddSceneClips.SpeakAsync` tallies each line. The run ends with one line, also in
`BddComposedVideo` and the `record_video` result:

```text
Speech: 46 lines, 45 reused, 1 asked (38 characters)
```

**Reuse only.** `BRUINET_VOICE_REUSE=1` makes step 3 fail on the first missing line, naming it, instead of spending;
`bruinet voice` lists them all. This is the setting for a pipeline: it can never pay by accident.

**Housekeeping.** `bruinet voice <suite>` lists the script lines with no clip and the characters they would cost.
`--fill` adds what the local cache holds, free and without a UI run; `--ask` also buys the rest; `--prune` deletes clips no
script uses any more.

## Storage format

A clip is about 134 KB as WAV, and git cannot shrink it: each reworded line stays in history for good. Measured on 30
clips from the cache (24 kHz, 16-bit, mono):

| Format | Share of WAV | Construction's 750 clips | Note |
| --- | --- | --- | --- |
| WAV | 100 % | about 100 MB | No code, exact |
| FLAC | 61 % | about 60 MB | Lossless, in every ffmpeg |
| Opus 32 kbit/s | 8 % | about 8 MB | Lossy, needs libopus in ffmpeg |

Decided (owner, 2026-11-06): Opus at 32 kbit/s. The listening check passed: 10 clips of 1.8 to 12.2 s, Opus then AAC against
AAC alone, sounded the same to the owner, and each clip kept its exact length. The video is AAC at 128 kbit/s either way,
and ffmpeg is already required to record.

## Ignore rules

| Repository | Today | Change |
| --- | --- | --- |
| Tomorrow `.gitignore` | Lines 440-441: `**/*.mp4`, `**/*.wav`; 443-444 the avatars' exceptions | Remove 441 and the now needless `.wav` exception 443, keep the mp4 rules, reword the comment on 439; add `**/*.run/` and `**/*.scenes/` |
| Bruinet `.gitignore` | No sound rule at all | Add `**/*.run/` and `**/*.scenes/` only |
| Construction `.gitignore` | Line 89: `*.mp4`, no sound rule | Add `**/*.run/` and `**/*.scenes/`: a leftover `progression-walkthrough.run` holds 30 WAVs under `Recordings/recordings` |

The two folder rules matter once `.wav` is no longer ignored: scene folders hold a copy of every line, and those must not
be committed. Today no WAV in Tomorrow is ignored outside `bin` and `obj`, so removing the rule shows nothing new.

## Tasks

| # | What | Where | Done when |
| --- | --- | --- | --- |
| 1 | Listening check: 10 clips through Opus 32k and into the AAC of a video, against AAC alone | Owner | Done 2026-11-06: Opus confirmed |
| 2 | Ignore rules | The three `.gitignore` files | `git check-ignore` agrees with the table |
| 3 | `ClipLibrary`: `Find(configDirectory, read)`, `TryGet`, `Add` (written whole, then moved, as the cache does), encode and decode through `Ffmpeg`, a clear error when ffmpeg lacks the codec | `src/Bruinet.Narrator/ClipLibrary.cs` | Tests below pass |
| 4 | Library keys; the three steps in `CloudVoice.ClipAsync`; `Library` on `AzureSpeech` and `AzureOpenAiSpeech`, set by `Speech.Find` | `CloudVoice.cs`, `AzureSpeech.cs`, `AzureOpenAiSpeech.cs`, `Speech.cs` | A second machine's empty cache asks nothing |
| 5 | The tally and its line; `BRUINET_VOICE_REUSE` | `BddNarrationPacer.cs` (`SpeakAsync`), `BddRecordingSession.cs`, `BddVideoComposer.cs`, `tools/Bruinet.Cli/Lsp/BddMcpServer.cs` | `record_video` shows the line |
| 6 | Presenter: pass the library at each `Speech.Find`; give `RehearseAsync` the script's instructions and folder | `src/Bruinet.Presenter/Services/PresenterRecorder.cs` | Rehearsal and recording share clips |
| 7 | `bruinet voice <suite>` with `--fill`, `--ask`, `--prune` | `tools/Bruinet.Cli` | The Todo sample's library filled by it |
| 8 | Docs: the guide's cache paragraph (line 230) and kept scenes, AD-024's voice row and a new "Broken when", CHANGELOG, `PublicAPI.Unshipped.txt`, `.github/skills/narrated-review` | `docs/guides/narrated-recordings.md`, `.docs/decisions/ad-024-*.md` | Docs match |
| 9 | Tomorrow: bump the submodule; `--fill` each suite; step-show says clips are committed, by path (`SKILL.md` line 46 says they stay local) | `.claude/skills/step-show/SKILL.md`, `tests/*/speech/` | A recording in a clean profile asks nothing |
| 10 | Construction: `--fill`, commit `speech/`, and the same note in `.github/skills/module-videos` and `scripts/record-module-video.ps1` | Exact.Construction | As task 9 |
| 11 | Only if a pipeline will record: reuse without a key (below) | `Speech.cs`, `VoiceSecrets.cs`, `SpeechSecrets.cs` | An agent with no voice key records a script |

Task 11: `Speech.Find` throws when a named cloud voice has no settings, so an agent needs the key even when every clip is
there. It should fail only on a real miss. The key then needs the voice, deployment and sample rate without the secrets
file, and Construction's scripts name no `Voice`, so they would get a `Voice` row or `BRUINET_VOICE`.

## Tests

All in `tests/Bruinet.Narrator.Tests`, with the fake service `CloudVoiceTests` already has; the codec ones need ffmpeg.

| Test | Proves |
| --- | --- |
| `AClipInTheLibrary_IsNotAskedFor_WithAnEmptyCache` | The second machine costs nothing |
| `AClipOnlyInTheCache_IsAddedToTheLibrary_AndNotAskedFor` | Today's clips are kept |
| `ALineSaidOnAnotherEndpoint_IsFoundInTheLibrary` | The key drops the endpoint |
| `AReworded_Line_IsAskedFor_AndTheOthersAreNot` | One line costs one clip |
| `ReuseOnly_FailsOnAMissingLine_AndNamesIt_AndAsksNothing` | A pipeline cannot spend |
| `TheLibrary_HoldsNoSecret` | Keys and files carry no key |
| `TheLibrary_IsFoundInTheSource_NotInBin` | Clips survive a clean |
| `TheTally_CountsReusedAndAsked` | The line can be trusted |
| `AClipFromTheLibrary_HasTheLengthItWasRecordedWith` | Pacing holds after the codec, within a frame |

Then one real check, for the owner's money: record `todo-in-a-minute-azure-speech.video.md` with the cache folder renamed.
It must report 0 asked.

## Open questions

Settled (owner, 2026-11-06): "the azure runs" are the voice recordings, not a pipeline. So tasks 2-10 are the plan, and task
11 waits until a pipeline is meant to record.

Also settled: the owner found no double billing, and the voice costs $22 per 1M characters.

## Is it worth building?

Probably not for the money. Construction's step lines, about 29,000 characters, cost about $0.64 to buy in full, and under
$1 with the intros and outros. Today's cache already stops repeat payment on one machine, so the library saves that sum once
per new machine or cleared profile.

Against that stand tasks 3-10: new code in the narrator, the CLI and Presenter, and clips in the history of three
repositories.

Parking the library was proposed. Decided (owner, 2026-11-06): build it anyway, tasks 2-10.
