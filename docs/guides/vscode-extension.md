# Bruinet BDD In VS Code

The VS Code extension (`tools/Bruinet.Bdd.VsCode`) is the Visual Studio extension's twin. It
starts the same language server (`bruinet lsp`, from `tools/Bruinet.Cli`) and shows the same Scenario Preview,
so the two IDEs say the same thing about a file. What each feature does is in the Visual Studio
guide ([visual-studio-preview.md](visual-studio-preview.md)). This page is only what differs in
VS Code.

It needs the .NET 10 runtime, and the C# extension (`ms-dotnettools.csharp`) to debug.

## Build And Run

From `tools/Bruinet.Bdd.VsCode`:

```powershell
npm install
npm run compile
```

Open the folder in VS Code and press **F5** to start an Extension Development Host. In it, open a
folder with a suite, such as `samples/Todo`. The extension uses:
- the server in its own `server` folder when packaged;
- else `bruinetBdd.serverPath`;
- else the repository's build of `tools/Bruinet.Cli` (build it first:
  `dotnet build tools\Bruinet.Cli` from the Bruinet root).

## In The Editor

Feature files, step files and the suite configs stay Markdown files. The language server adds
the gate's diagnostics, outline, folding, colours, completion, hover, go to definition, find
references, quick fixes and rename, as in Visual Studio.

## Scenario Preview

**Ctrl+K V** in a feature or step file, or the preview button in the editor's title bar, opens the
preview beside it. It follows the active file, and the caret and the preview follow each other. A
heading's four buttons are Visual Studio's ([Run From The Preview](visual-studio-preview.md#run-from-the-preview)):
**▶ Run** on the suite's runner, **▷ Run as test** and **● Debug** through Test Explorer, and
**⏭ Step**. **Bruinet BDD: Run Scenario on the Runner** and **Bruinet BDD: Step Through Scenario**
in the Command Palette take the scenario at the caret.

The suite's runner is kept ready as in Visual Studio. Settings: `bruinetBdd.warmRunner` (on) and
`bruinetBdd.warmRunnerIdleMinutes` (10), read when the extension starts. A Run or Step builds the
suite's project only when something other than a scenario file changed since its last build.

Old results are deleted as in Visual Studio: **✕** beside a scenario's result, **Delete results**
in the preview's bar for the file, and **Bruinet BDD: Delete All Results of a Suite** in the
Command Palette for a whole suite (VS Code has no BDD Explorer). Each asks first in a modal dialog,
naming how many runs and files, and deletes for good.

A video script (`*.video.md`) previews as the scenes it will record, with **Record** on its title;
a scenario heading has **Record** beside **Run**. Record builds the suite, runs the scenes with
`BRUINET_RECORD`, and offers **Watch** when the video is made; the preview lists the file's
recordings. Settings: `bruinetBdd.recordingsFolder`, `bruinetBdd.voice`, `bruinetBdd.beat`,
`bruinetBdd.touchColor`. It
needs ffmpeg. See [Narrated Recordings](narrated-recordings.md).

## Test Explorer

The **Testing** view lists each suite, its feature files and their scenarios; an outline is one
test for all its rows. Its **Run** is Run as test: it builds the suite's project (unless nothing
changed since its last build), then runs
`dotnet test --no-build --filter BddScenario=<address>`, and marks each scenario from the report
its run wrote. A failed scenario points at its failed step. **Debug** runs the same way; the C#
debugger attaches to the testhost, and step breakpoints stop the run. The suite's test class must
use `[BddScenarioTheory]`; a suite that does not says so in the view. One run at a time, and
cancelling a run stops the test process and the app it started. The **Run on the runner** and
**Step through** profiles (the run button's menu) run one scenario or file on the suite's runner.

## Step Breakpoints

**Ctrl+K F9**, the editor's context menu, or a click on a step's keyword in the preview toggles a
step breakpoint. A red dot marks it in the gutter. The breakpoints are the same per-user file as
Visual Studio's (`%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json`), so a breakpoint set in one IDE
stops a run started from the other.

## Step Through A Scenario

**⏭ Step** (or the **Step through** profile) pauses before the scenario's first step, as in Visual
Studio ([Stop At A Step](visual-studio-preview.md#stop-at-a-step)), with no debugger. The paused
step's file opens at its line, and the preview shows the bar with the step's page, app and
arguments, **📷 Screenshot** and **🐞 Debug from here** (**Bruinet BDD: Debug From Here**: the C#
debugger attaches to the runner, and the next step stops just before its step definition). The
moves:

| Move | Keys | Command |
| --- | --- | --- |
| Step over | F10 | **Bruinet BDD: Step Over BDD Step** |
| Step into (a `## Step:` call) | F11 | **Bruinet BDD: Step Into BDD Call** |
| Step out (of a call) | Shift+F11 | **Bruinet BDD: Step Out of BDD Call** |
| Continue / Stop | F5 / Shift+F5 | **Bruinet BDD: Continue BDD Run** / **Stop BDD Run** |

These keys are the moves only while a Step-through run is paused and nothing is debugged
(`bruinetBdd.stepPaused && !inDebugMode`); otherwise they are VS Code's and the debugger's.

While a **debugged** run is paused at a step, the moves are **Alt+Shift+F10**, **Alt+Shift+F11**
and **Ctrl+Shift+F11** (the context keys `bruinetBdd.paused`, `bruinetBdd.pausedAtCall`,
`bruinetBdd.pausedInCall`), with the debugger's own F5 and Shift+F5; F10 and F11 stay the C#
debugger's. The bar's buttons do the same either way.