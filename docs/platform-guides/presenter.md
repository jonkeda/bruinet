# Presenter Platform Guide

`Bruinet.Presenter` is a Windows desktop shell (a MAUI app) for loading and running UAT
workspaces: a narrow bar docked to one side of the screen, with the app under test (or its browser)
placed in the rest.

## Projects

- `src/Bruinet.Presenter`
- `tests/Bruinet.Presenter.Bdd.Tests`: Presenter's own UAT suite

## A Minimal Run

Presenter needs three things built: Presenter, the app under test, and the UAT project that holds
the page objects. The WPF workspace is the cheapest: no Appium, no MAUI workload for the app.

```powershell
# Working directory: Bruinet root
dotnet build samples\Bruinet.Samples.Wpf.App\Bruinet.Samples.Wpf.App.csproj
dotnet build tests\Bruinet.Wpf.UITests\Bruinet.Wpf.UITests.csproj
dotnet run --project src\Bruinet.Presenter\Bruinet.Presenter.csproj -f net10.0-windows10.0.19041.0
```

Then, in Presenter (the toolbar is icon-only; hover for tooltips):

1. **Open** `tests\Bruinet.Wpf.UITests`.
2. **Validate**, and fix anything the Diagnostics tab reports.
3. Select a scenario in the tree, for example *Home page is visible*.
4. **Run**, or **Next** to go one step at a time.

On first start Presenter opens the default sample workspace, which is usually not built yet:
Workspace mode with "Not built yet" and the Diagnostics tab is the expected first screen.

## The Workspace

A workspace is a folder with `bdd.config.md` and `.feature.md` scenario files. It is anchored on a
**UAT project and a fixture** (AD-018):

```markdown
## Runtime

| Field   | Value                             |
| ---     | ---                               |
| Project | ./Bruinet.Samples.Todo.Uat.csproj |
| Fixture | TodoUatFixture                    |
```

- **Project**: Presenter asks MSBuild where the project's output goes, so the config holds no
  `bin/` paths. A bare assembly name under `## Assemblies` still works, and is shown as found by
  search.
- **Fixture**: Presenter creates it by name from the Pages assembly: a non-abstract class with a
  parameterless constructor whose name or full name is `Fixture`, or `Fixture` + `Fixture`. The
  xUnit runners take the fixture as a type argument and only check the name, so a suite can pass
  in xUnit and still name a fixture Presenter cannot find.
- **AppPath** is an optional override. The fixture finds its own app.
- **The suite runs in a process of its own**, started from a copy of the suite's output as a test
  host is: a scenario runs from Presenter as it does under `dotnet test`. The Pages assembly has to
  be the suite's test project (its `runtimeconfig.json` is used) and the suite has to reference
  `Bruinet.Uat`, which is the runner. The runner is started when a built workspace loads and waits
  between runs, from its copy, so the suite can be rebuilt while Presenter is open, and a long
  session does not grow Presenter. It reads the feature and step files from the workspace, and
  writes its results where the IDEs' preview reads them. A fixture still finds its files from its
  own assembly, with `AssemblyFile.FolderOf(typeof(MyFixture).Assembly)`
  (`Bruinet.Core.Composition`), which answers with the output the copy was made from.
- **A suite built with an older Bruinet** than Presenter says so when it is run ("Rebuild the
  suite"): the two talk in versioned messages.
- **A fixture that implements `IUatScenarioLifecycle`** has its `BeforeFile` and `BeforeScenario`
  called by Presenter as by the suite's test class: a scenario starts on its file's `TestData`.

The **Config** tab edits the file in place: the project, a fixture picker that lists every
candidate with the reason an unusable one is unusable ("abstract", "no parameterless
constructor"), and the derived values (target, Pages assembly, app) with where each came from
and a **Pin** action that writes the value into the file. Save keeps rows it did not change
byte for byte, stays enabled while the config is invalid, and reloads the workspace keeping the
tree's expansion and selection. If the file changed on disk, Save offers **Keep mine** or
**Reload theirs**.

The run toolbar carries the session choices, stored per workspace: **Debug/Release** (for the
tests; the fixture picks the app's configuration), the **platform** (sets `APPIUM_PLATFORM`, and
offers only what the fixture declares with `[UatPlatforms]`), and **Build**, which shows
MSBuild's output verbatim. A build ends the session's app first; the runner then starts again
from the new output.

To build the app with the tests, give the UAT project a reference that builds it without
referencing it:

```xml
<ProjectReference Include="..\..\samples\Bruinet.Samples.Wpf.App\Bruinet.Samples.Wpf.App.csproj"
                  ReferenceOutputAssembly="false" />
```

## The Shell

The top row starts with **Open** (a workspace folder; in Narrator mode, a video script). Then the
three modes, a button each:

- **Present**: the scenarios, the steps and the moves, nothing else. A workspace that loads cleanly
  opens in it.
- **Workspace**: adds the recent folders, the build choices and
  **Build**, the tabs and the Selection panel. A workspace with problems opens in it, and says
  "Not built yet" above **Build** when the suite has no build.
- **Narrator** (the video camera): a video script's scenes and steps in a tree, run and recorded
  scene by scene ([Narrator mode](#narrator-mode)).

**Reload** (every mode) reloads and checks the workspace. At the top right, **Dock** moves the bar
to the other side of the screen (remembered), and **Settings** opens the Settings page (the same
button closes it): the pause between steps of each speed, the App window, When a run ends, and the
theme. The run toolbar is under the
steps: **Run**, **Next**, **Into**, **Out**, **Stop**, **Restart**, and **Speed**.

| Tab | Shows |
| --- | --- |
| Tree | a workspace summary, the Scenarios tree, the Steps list, the status message, and a Selection expander |
| Config | the config editor |
| Diagnostics | config, parse and bind results, and run timings |
| Discovery | the pages and controls the scenarios can name |
| Command Catalog | every phrase, by keyword |

Presenter checks each feature and step file as the xUnit gate and the IDEs do: against the suite's
own catalogs, loaded through `Bruinet.Bdd.Workspace` from a shadow copy of its build. The
Diagnostics tab lists the gate's diagnostics, with severity and code. A suite that is not built yet
is only parsed, and Diagnostics says to build it. A file under an `ExpectedFailures` folder fails
on purpose: it is reported as "expected failure" and does not make the workspace need attention.
The config can be `bdd.config.md` or, for a service suite, `bdd.config.md`.

Pick in a tree, step in a list. The **Scenarios** tree starts at the workspace folder and shows only
Markdown: folders with Markdown in them, `.md` files, and under each `.feature.md` its suite and
scenarios. `bin`, `obj` and non-Markdown files are hidden. A collapsed parent shows the worst
status of its children. The **Steps** list below it holds the picked scenario's steps with their
status; a `## Step:` call is a bold row of its own, with its steps indented under it. The step a
run is at is highlighted and kept in view. Hovering a row shows its full text, and after a failure
the message. Each section is an expander.

The button beside the Steps header opens the **Scenario Preview** the IDEs show, in a window of its
own: the whole feature file with prose, tables and Examples, bound against the suite. It follows
the picked scenario and its run: each step's result, and the step the run is at.

## Running

A run pauses **before** a step, so what is about to happen can be said before it happens. The
moves are those of the IDEs' stepping (AD-022), on keys a presentation clicker sends too:

| Button | Keys | Does |
| --- | --- | --- |
| **Run** | F5 | runs the selection's scenarios; from a pause, goes on. Stops at a breakpoint, a failure or the end |
| **Next** (Step) | Space, Right, Page Down | with no run on, starts one paused before the first step; else runs the highlighted step (a `## Step:` call whole) and pauses before the next |
| **Into** | Down | at a call, goes inside it without running anything |
| **Out** | Up | inside a call, runs the rest of it and pauses after it |
| **Stop** | Esc | ends the run and closes the app |
| **Restart** | Ctrl+R | the scenario again, paused before its first step |

The keys work anywhere in the Presenter window except in a text box. A click on a step sets or
clears its breakpoint. A right-click offers **Run until here** (runs on and pauses before it), **Run from here** (only while a
run is paused before it: skips the steps in between, marked skipped, and runs on; the app must
already be where they lead), **Debug from here** (only while paused before it: Windows asks which
debugger to attach to the runner, and the step then stops just before its step definition, so
Step into goes into it) and **Toggle breakpoint**. The preview's paused bar has the same moves.

Breakpoints are the IDEs' own file, `%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json`: a red dot in the
list marks one, a breakpoint set in Visual Studio or VS Code stops Run in Presenter, and one set in
Presenter stops a debugged run in the IDEs. Presenter's own dots are on the feature file's steps
and calls; a breakpoint an IDE set on a step inside a step file stops Run too, before that step.
The paused bar shows the step's page, app and arguments as the runner read them.

| Selected | Runs |
| --- | --- |
| Workspace or folder | every scenario file under it |
| `.feature.md` file or suite | its scenarios |
| Scenario | that scenario |
| Config or other Markdown | nothing |

**Speed** cycles **Fast**, **Normal** and **Slow**, with a gauge for each; the pause of each is set on
the Settings page (250, 800 and 1500 ms at first), and both are remembered. The pause is read when a
run starts or goes on, and applied between steps, never before the first: seven steps at 1000 ms take
at least six seconds. A step is marked passed only after its command completes. The
status line under the list shows the current step, where the run is paused, or the result
(`Passed: 1/1 scenarios`); per-step timings are in Diagnostics.
### Record

**Record** (the dot at the end of the run toolbar) records each run on video while it is on
([Narrated Recordings](../guides/narrated-recordings.md)). A scenario that a video script of the
suite has a scene for is narrated by it: its intro, each step's line, its outro; any other runs
silently, with its beats. The video is made when the run ends, however it ends, and **Watch**
opens the last one. It needs ffmpeg; without it, Record says how to get it and stays off.

On the Settings page, **Narrator**: the Recordings folder (the suite's `TestResults\recordings`
when empty), the voice when the script names none, with **Try** to hear it, the beat after a step,
the **Touch colour** of the marks on taps and swipes when the script names none
([Touch Marks](../guides/narrated-recordings.md#touch-marks)), and **Speak aloud**, which also plays
each line while the run waits for it.

Under it, **Cloud voice (Azure OpenAI)** sets up the narrator's cloud voice
([Cloud Voice](../guides/narrated-recordings.md#cloud-voice)): it says whether one is set up and
from where (a file, or the environment), and takes the endpoint, the key, the deployment, the API
version and the default voice. **Try** speaks a sample line with the fields as they are, before
anything is saved; **Save** writes the `azureFoundry` object of `voice.secrets.json` for this machine
(`%USERPROFILE%\.bruinet`), or beside the open workspace when its repository's `.gitignore`
ignores the file (**Add the line to .gitignore** adds it); **Remove** takes that object out (the
file goes with the last one), after a second click. The key is typed into a password field and never shown again: after Save the part
says "key set", and an empty key on Save keeps it. Once a cloud voice is set up, the Voice picker
lists the `azure:` voices.

**Azure Speech** is the part below it ([Azure Speech](../guides/narrated-recordings.md#azure-speech)): the
same state line and key rules, an endpoint (a region such as `eastus2`, or the resource's URL), the **Language**
(the system's to begin with) and the **Voices** of that language from the service's list, the **Default voice**
(typed, or picked), and the **Sample rate** (24000 or 48000). **Load voices** asks the service and keeps the
list, so later the picker needs no call; **Try** speaks a sample line before anything is saved; **Save** writes
the file's `azureSpeech` object, in the place the Cloud voice part shows, keeping `azureFoundry`; **Remove**
takes only that object out, after a second click. Its voices join the Voice picker as `azure-speech-service:`.

### Narrator mode

Narrator mode makes a whole video script (`*.video.md`) into its video from Presenter, a scene at
a time.

- **Open**: the mode opens the workspace's last script, or its first. **Open script** picks
  another; its suite is found by walking up from the script to `bdd.config.md`, and loaded as the
  workspace when it is not the one open.
- **The tree**: the script, then its scenes. Under a scene: its intro, each step of its scenario
  with what is said before it (or *silent*), and its outro. What the script has wrong (`VID0xx`)
  shows on its scene or row, and that scene cannot be checked.
- **The checkboxes**: a scene's checkbox puts it in the run; the script's checks or clears them
  all. Beside a scene is what its recording is worth: *not recorded*, *failed*, *out of date* (its
  lines, its scenario's steps, the voice or the beat changed since) or *recorded* with its
  length. Opening a script checks exactly the scenes that need recording.
- **Run** rehearses the checked scenes, in the script's order, recording nothing: paced by
  **Speed**, or by the spoken lines when **Speak aloud** is on. The other moves step through the
  scene shown. A scene that fails does not stop the ones after it.
- **Record** records the checked scenes, each into its own kept folder
  (`<Recordings>\<script>.scenes\scene-NN`); a scene that passes is unchecked. A scene recorded
  again replaces that scene only. **Stop** drops the scene being recorded and keeps the finished
  ones.
- **Stitch** (the link) joins the latest recording of every scene into
  `<Recordings>\<script>.mp4`, replacing the last one, with its captions and timeline. It runs by itself when a
  Record leaves every scene recorded; a failed or out-of-date scene is stitched too, and named,
  so a draft can be watched.
- **Watch** plays the stitched video; the play button on a recorded scene plays that scene alone.
  **Open folder** opens the Recordings folder.

## Supported Targets

The target comes from the fixture's base class.

| Target | App path environment |
| --- | --- |
| `MAUI` | `APPIUM_APP_PATH` |
| `WPF` | `WPF_APP_PATH` |
| `WINFORMS` | `WINFORMS_APP_PATH` |
| `BLAZOR` | `BLAZOR_APP_PATH` |
| `HTML` | `HTML_APP_PATH` |
| `STRIDE` | `STRIDE_APP_PATH` |

Presenter exports `AppPath`, when the config sets one, to the variable for the target.

Presenter shows the app under test as the **App window** setting says, for every target (a MAUI, WPF
or WinForms window, or a browser):

| App window | The app |
| --- | --- |
| Beside the bar (default) | in front of other windows, in the rest of the bar's monitor |
| Maximized, other monitor | filling the largest monitor without the bar; beside the bar when there is one monitor |
| Maximized | filling the bar's monitor, with the bar on top while a run is on |
| Where it opens | in front of other windows, not moved |
| Background | behind everything, as in a test run |

It tells the launcher to leave the window shown (`BRUINET_AUT_WINDOW=shown`: the MAUI driver no longer
sends it behind, and a browser is not headless and opens at `BRUINET_AUT_BOUNDS`). It notes the
top-level windows before a run starts, and once the app's window opens it moves it, without taking
the focus; it tries again after the first step, for an app that opens late. Presenter keeps the keys:
when the app it showed takes the focus (WPF and WinForms do, for real input), Presenter takes it
back after the step. A placement that fails does not fail the run; Diagnostics shows the result.

**When a run ends**: **Close the app** (default) closes it when the run passes, fails or is stopped;
**Keep it open** leaves it where it is until the next run starts, **Stop** is pressed with no run on
("Closed the app"), another workspace is opened or built, or Presenter closes.

## User Settings

`PresenterUserSettingsService` stores `LastOpenedFolder`, up to 10 `RecentFolders`, `LastScript`
(the video script Narrator mode reopens), and the per-workspace session choices. On start Presenter opens the last folder, then the first recent
folder that still exists, then the default sample workspace.

```text
%LOCALAPPDATA%/Bruinet.Presenter/user-settings.json
```

```powershell
$env:BRUINET_PRESENTER_SETTINGS_PATH = "path\to\user-settings.json"
```

## Build

```powershell
# Working directory: Bruinet root
dotnet build src\Bruinet.Presenter\Bruinet.Presenter.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Presenter.Bdd.Tests\Bruinet.Presenter.Bdd.Tests.csproj -v:minimal /nr:false
```
