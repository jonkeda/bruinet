# Processes, files and secrets: review and proposals

Written 2026-11-11 at `b2ea4570`, as review 9 of `additional-reviews-proposal.md`.
Read from the source under `src/` and `tools/`; nothing was built or run, and `voice.secrets.json` was not opened.
Not a penetration test: `security.yml` runs gitleaks and CodeQL, and this review does not repeat them.

## Outcome

Starting processes and keeping the cloud voice's key are in good shape: almost every start uses an argument list without the shell, and the key reaches only the request's header.
The weak part is what gets written and kept. SQL recordings and step traces store whatever the app sent or showed, with no redaction, and the SQL recordings are meant for git.
HTTP recordings do redact by default, with a few gaps. Two MCP tools take a path or a git revision without checking it, and the SQL replay hook is a control endpoint that only an environment variable switches on.

Nothing here needs a redesign. Each proposal is a default, a refusal or a paragraph in a guide.

## What is good and should stay

- Process starts use `ArgumentList` and `UseShellExecute = false` nearly everywhere (`BddMcpServer.Run`, `CodeGate`, `TestRunner`, `WebAppHost`, `Ffmpeg`, `AndroidRecorder`, the `git` helpers in `Bruinet.Cli`).
- `write_code` stays inside the scraped project, takes only `.cs`, and refuses `bin`, `obj` and `Recordings` (`src/Bruinet.Scraper/Code/CodeGate.cs:27-34`).
- `delete_results` counts first (`dryRun` defaults to true) and never deletes outside a run folder, whatever a manifest says (`tools/Bruinet.Cli/Lsp/BddRunResults.cs:127`, `:180`).
- The runner's pipe has a random name and `PipeOptions.CurrentUserOnly` (`src/Bruinet.Bdd/Runner/BddRunnerClient.cs:131-132`).
- `MockApiServer` binds to `127.0.0.1` only, with WireMock's admin interface off (`src/Bruinet.Mocking/MockApiServer.cs:304-309`); `WebAppHost` starts the app on `127.0.0.1` too (`src/Bruinet.Html/Testing/WebAppHost.cs:68`).
- The MCP host and the language server speak over stdio; no HTTP or socket listener was found for either.
- HTTP recordings write named headers, query parameters and JSON fields as `***`, keep only three kinds of request header, and a scan stops a save that still holds a key (`src/Bruinet.Mocking.Http/HttpRedaction.cs:35-101`, `HttpReplaySession.cs:437-445`).
- The voice key: `ToString` leaves it out, error messages name the endpoint only, a broken file's message does not quote the file, and the clip's hash does not include it (`src/Bruinet.Narrator/VoiceSecrets.cs:57`, `SpeechSecrets.cs:250-253`, `CloudVoice.cs:148-168`).
- The Scraper never reads a password field's value, drops a secret field's value, and keeps the signed-in profile under `%LOCALAPPDATA%`, outside the repository (`src/Bruinet.Scraper/Html/PlaywrightScrapeSource.cs:41`, `Projects/Redactor.cs:33-34`, `Projects/ScrapeProject.cs:172-178`).
- The SQL hook's test id is `^[A-Za-z0-9._-]{1,200}$`, so it cannot name a path outside the recordings folder (`src/Bruinet.Mocking.Data/Replay/ReplayHook.cs:105`, `:50`).

## Findings

### 1. SQL recordings keep everything, and go into git

- A recording stores each command's text, every parameter value and every result row as they are (`src/Bruinet.Mocking.Data/Replay/Recording.cs:26-51`, `:262-281`).
- `Bruinet.Mocking.Data` has no redaction at all: no option, no scan, no mask.
- `docs/guides/sql-replay.md` says the recordings are "kept in git" and says nothing about what they hold.
- How it goes wrong: a suite that signs in records the users table. With ASP.NET Identity that is the email, `PasswordHash`, `SecurityStamp` and the authenticator key, pushed to the remote with the next commit. Tomorrow's studio suite, with its admin accounts, is such a suite.
- A miss prints the live parameter values and the recorded ones into the test's failure message (`ReplaySession.cs:320-328`), so they also reach the test log and the trace.

### 2. Traces are on by default and read back every control's value

- Every scenario that passes or fails is traced unless `BRUINET_TRACE=off` (`src/Bruinet.Core/Logging/StepTrace.cs:19-33`).
- After a failed step the trace holds the value of every control on the page (`src/Bruinet.Uat/UatScenarioTrace.cs:72-86`), read through each control's own reader with no exception for a password field (`src/Bruinet.Uat/UatPageState.cs:99-117`).
- The trace also holds each step's text (`UatScenarioTrace.cs:54`), and a step such as `I sign in with password "…"` carries its value.
- No mask or redaction exists in `Bruinet.Core`, `Bruinet.Bdd`, `Bruinet.Uat` or `Bruinet.Html`.
- How it goes wrong: a sign-in scenario fails after the password was typed; `__trace.json` under `TestResults` holds it, a CI job uploads the folder as an artifact, and `get_trace` hands it to an agent.
- The control calls themselves are safe: they keep the page, the control and the action, never an argument (`StepTrace.cs:14`).

### 3. The SQL replay hook is a control endpoint switched on by a variable

- `UseSqlReplayHook()` and `SqlReplayModule` serve `/__bruinet/sql/begin` and `/__bruinet/sql/end` to anyone who can reach the app, whenever `BRUINET_SQL_REPLAY` is set (`src/Bruinet.Mocking.Data.AspNetCore/SqlReplayHook.cs:16-37`, `src/Bruinet.Mocking.Data.SystemWeb/SqlReplayModule.cs:18-47`).
- Nothing checks the environment, the caller's address or a shared secret.
- In `Record`, a caller starts a session and the app writes every query its requests make to disk. In `Replay`, a caller's requests are answered from a recording instead of the database.
- `end` answers 409 with the misses, which hold command text and parameter values (`ReplayHook.cs:139-143`).
- Both paths are plain `GET`s that change state, and the cookie is set without `Secure` (`SqlReplayHook.cs:31`).
- How it goes wrong: the guide's own case, "An app in a process of its own (IIS…)", is a shared test server. The variable stays set on it, or is copied into a deployment's settings, and the hook is open to that network.

### 4. Two MCP tools take a git revision and a path unchecked

- `tests_affected` passes its `base` argument straight to `git diff --name-only <base>` (`tools/Bruinet.Cli/Lsp/BddMcpServer.cs:1181`, `tools/Bruinet.Cli/Selection/SelectionCommands.cs:91`). A value starting with `-` is read by git as an option; `--output=<file>` makes git write that file. `bruinet tests … --since` has the same path (`SelectionCommands.cs:52`).
- `review_page` writes its HTML to `Path.GetFullPath(path)`: any absolute path, and a relative one against the process's folder, not the solution (`BddMcpServer.cs:1358-1362`).
- `record_video`'s `recordingsFolder` is used the same way (`tools/Bruinet.Cli/Lsp/BddLanguageServer.Record.cs:158-159`).
- How it goes wrong: an agent that misreads a prompt, or follows text it found in a page, overwrites a file outside the repository. The other writing tools refuse this; these three do not.
- By design, `write_code` followed by `run_test` or `run_scenario` runs code the agent wrote. That is the tool's purpose, and a guide should say so.

### 5. HTTP redaction has gaps, and personal data is not mentioned

- A streamed answer's parts are scanned but never redacted: `Redact` handles `RequestBody` and `ResponseBody` only (`src/Bruinet.Mocking.Http/HttpRedaction.cs:47-56`), so a token field in a server-sent event stays unless it happens to match a scan pattern.
- Only JSON and form bodies are redacted (`HttpRedaction.cs:119-148`); an XML or SOAP body with a password is kept whole.
- Response headers are kept except eight (`HttpReplaySession.cs:461-467`), and only `Set-Cookie` of them is redacted, so a `Location` with `?code=…` or a token in its query is stored.
- The default names miss common ones: `token`, `secret`, `code`, `assertion`, `key`, `subscription-key` (`HttpReplayOptions.cs:41-42`). The `oauth` preset ignores `code` and `code_verifier` for matching but does not redact them (`HttpReplayOptions.cs:21-22`).
- The scan knows seven shapes of key (`HttpRedaction.cs:23-32`); an Azure resource key (32 hex characters) or an opaque bearer token in a body passes.
- Bodies are kept whole, so names, emails and prompts sent to a model go into git. The guide's Secrets section does not say so.
- A miss prints the live call's differing value (`HttpReplaySession.cs:427`); a redacted field never differs, anything else is shown.

### 6. Smaller findings

| Where | What | How it goes wrong |
| --- | --- | --- |
| `src/Bruinet.Narrator/SpeechSecrets.cs:297-301` | The key file is made owner-only on Windows only. | On macOS or Linux it is written with the default mode, readable by other accounts. |
| `src/Bruinet.Stride.Automation/AutomationServer.cs:12`, `:106-111` | The game's pipe has a fixed default name and no `CurrentUserOnly`. | A game started without `--pipe` listens on a known name; the test fixture does give a unique one (`StrideTestFixtureBase.cs:107-114`). |
| `AutomationServer.cs:186`, `:207` | With `VerboseLogging`, every command and answer is printed. | Typed text reaches the game's console log. Off by default. |
| `tools/Bruinet.Bdd.VisualStudio/PreviewController.cs:771-775` | `watch` shell-opens any existing path the preview page sends. | A page that could be made to send another path would start any file. Whether feature text can reach the page's script was not checked. |
| `src/Bruinet.Scraper/Html/PlaywrightScrapeSource.cs:506-511` | `BRUINET_SCRAPER_DEBUG_PORT` opens DevTools on the recording browser. | That browser holds the person's sign-in; any local process can drive it while the variable is set. |
| `tools/Bruinet.Cli/Sql/SqlCommands.cs:295` | `--connection` takes the connection string on the command line. | A SQL login's password lands in shell history and the process list. |
| `src/Bruinet.Mocking.Data/ExtendedEvents.cs:69-70` | The capture session writes whole batches to `.xel` files on the server, kept after `stop`. | The log has no values, as the guide says; the server's files do. |
| `src/Bruinet.Narrator/VoiceSecrets.cs:290`, `src/Bruinet.Appium/AndroidHost.cs:56-60`, `src/Bruinet.Stride/Infrastructure/StrideGameDriver.cs:95` | Arguments joined into one string. | Not a hole, but a folder ending in `\`, or a game argument with a space, is split wrongly. |
| `src/Bruinet.Scraper/Projects/ScraperConfig.cs:42` | Masks are empty by default; snapshots keep all page text. | A recording of a page that shows customer data is committed with it, outside `Scraper.Sites`. |
| `BRUINET_VOICE_KEY`, `BRUINET_SPEECH_KEY` | Every started app inherits the test process's environment (`WebAppHost.cs:69-97`, `DesktopApp.cs:52-68`). | An app under test that dumps its environment on start logs the key. |

## Process starts, in short

All 47 sites were read. None builds a command for a shell.

| Group | Started | Arguments |
| --- | --- | --- |
| `dotnet build`, `test`, `exec`, `run`, `msbuild` | MCP server, `CodeGate`, `TestRunner`, `BddRunnerClient`, `WebAppHost`, `McpReloadingHost`, `MsBuildProjectEvaluator` | List; project and filter from the suite, the filter slugged to `[A-Za-z0-9._/-]` (`BddScenarioAddress.cs:32-45`) |
| `git` | `SqlCommands`, `SelectionCommands`, `VoiceSecrets` | List, except `VoiceSecrets` (one string); `base` unchecked, finding 4 |
| `adb`, `xcrun`, `ffmpeg` | `AndroidRecorder`, `AndroidInstalledBuild`, `IosInstalledBuild`, `Ffmpeg`, recorders | List, except `AndroidHost.Reverse` (one string, serial from the caller) |
| The app under test | `DesktopApp`, `StrideGameDriver`, `WebAppHost` | Path and arguments from the test's own code or settings |
| Visual Studio extension | `dotnet … lsp`, `dotnet test`, `taskkill` | One quoted string each; values are paths and a slugged filter |
| Open a file with the shell | `PreviewController`, `BddRunController`, the Presenter (twice) | `UseShellExecute = true` on a video or folder path; see finding 6 |

Nothing from a feature file reaches a command line except a scenario's name, slugged, and the feature file's own path.
The MCP arguments that reach one are `path`, `suite`, `head`, `timeoutMinutes` and `base`; only `base` is passed on unchecked.

## Proposals

1. **SQL recordings: redact by name, and warn.** Add `Redact` to the session (column and parameter names, default `PasswordHash`, `SecurityStamp`, `AuthenticatorKey`, anything matching `password|secret|token|apikey`), written as a typed `***` that matches anything, as HTTP's does. Run the same seven-pattern scan before `Save`. Add a "What a recording holds" section to `sql-replay.md`: record against seeded test data only, never a copy of production.
2. **Traces: skip password fields and mask named controls.** `UatPageState.ReadControl` writes `***` for a control whose element is a password input, and for names a suite lists (`| TraceMask | Password, Pin |` in `uat.config.md`). `reporting-artifacts.md` says that a trace holds page values and step text, and that `TestResults` is not for public upload.
3. **SQL hook: refuse outside a test setup.** Serve the control paths only to a loopback caller unless `BRUINET_SQL_HOOK_KEY` is set and sent with each control request; answer 404 otherwise. Make `begin` and `end` `POST`. The guide gets one warning line: never set `BRUINET_SQL_REPLAY` on a server others can reach.
4. **MCP: check `base`, confine the two paths.** Refuse a `base` or `--since` that starts with `-`, and pass it after `--end-of-options`. Resolve `review_page`'s `path` and `record_video`'s `recordingsFolder` against the solution and refuse a result outside it, with the message `write_code` already uses.
5. **HTTP: close the gaps.** Redact inside streamed parts and XML bodies by name; redact the query of a `Location` header; add `token`, `secret`, `code`, `code_verifier`, `assertion`, `subscription-key` to the defaults; add a 32-hex Azure key to the scan when it follows a key-like name. Add a line on personal data to the guide's Secrets section.
6. **Voice file: owner-only everywhere.** On Unix create the file with mode `600` (`File.SetUnixFileMode` before the move).
7. **Stride pipe: current user only.** Add `PipeOptions.CurrentUserOnly`, as the runner has, and keep `VerboseLogging` documented as a debugging switch that prints typed text.
8. **`watch`: videos only.** Open a path only when it ends in `.mp4` and lies in the suite's recordings folder; the Presenter and `BddRunController` already get theirs from that listing.
9. **Connection strings from a variable.** Add `--connection-from <VAR>` to `bruinet sql`, and use it in the guide's examples.
10. **One guide page, "What Bruinet writes".** A table of each artifact, where it goes, what it can hold and whether it belongs in git: SQL and HTTP recordings, call logs, traces, screenshots, videos, Scraper snapshots, the browser profile, the speech cache, the `.xel` files. It also carries the two warnings that have no code fix: `BRUINET_SCRAPER_DEBUG_PORT`, and that an MCP agent runs the code it writes.

## Not settled

- Whether another Windows account can drive the Stride pipe depends on the default pipe access list; the code does not restrict it, and it was not tested.
- Whether feature-file text can reach the Visual Studio preview page as script was not read; finding 6's `watch` row matters only if it can.
- Videos were not reviewed frame by frame: the recorders capture a window or a screen region, so whatever is on screen is in the video. A password field shows dots; a notification from another program would show as it is.
- `src/Bruinet.Mocking.Http/HttpMatching.cs` was not read in full; the claim that a redacted field never shows in a miss rests on the guide's "a `***` matches whatever the live call sends".
- The Presenter's settings file (`PresenterUserSettings`) was not checked for stored secrets.

## For the owner

Proposals 1 to 3 change what a first user's repository and build server end up holding, so they are worth doing before a first release.
Proposal 4 is an hour's work. The rest can wait for the guide page in proposal 10, which is the cheapest single step: it tells a user what not to commit today.
