// The Bruinet BDD extension for VS Code: the same language server as Visual Studio's
// (bruinet lsp, tools/Bruinet.Cli) for diagnostics, completion and navigation, the same Scenario Preview,
// Test Explorer on the scenarios' addresses, and step breakpoints shared with Visual Studio.
import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";
import { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import * as breakpoints from "./breakpoints";
import { deleteResults, ScenarioPreview } from "./preview";
import { BddStepping, Move, StepPaused } from "./stepping";
import { ScenarioTests } from "./tests";

const featureOrSteps = /\.(feature|steps)\.md$/i;
// A video script previews too, as the scenes it will record.
const previewable = /\.(feature|steps|video)\.md$/i;
let client: LanguageClient | undefined;

/** What a Step-through run said: where it paused, or how it ended. */
export type StepEvent =
  | { kind: "paused"; paused: StepPaused }
  | { kind: "ended"; status: string; message?: string }
  | { kind: "build"; project: string; built: boolean }
  | { kind: "runEnded"; status: string; results: { scenario: string; status: string; message?: string }[] };

/** The extension's exports: for its own tests in a VS Code window. */
export interface BruinetBddApi {
  readonly onStep: vscode.Event<StepEvent>;
}

export async function activate(context: vscode.ExtensionContext): Promise<BruinetBddApi | undefined> {
  const server = serverPath(context);
  if (!server) {
    void vscode.window.showErrorMessage("The Bruinet BDD language server is not built. Build tools/Bruinet.Cli, or set bruinetBdd.serverPath.");
    return;
  }

  const serverOptions: ServerOptions = { command: "dotnet", args: [server, "lsp"] };
  const clientOptions: LanguageClientOptions = {
    documentSelector: [
      { scheme: "file", language: "markdown", pattern: "**/*.feature.md" },
      { scheme: "file", language: "markdown", pattern: "**/*.steps.md" },
      { scheme: "file", language: "markdown", pattern: "**/*.video.md" },
      { scheme: "file", language: "markdown", pattern: "**/uat.config.md" },
      { scheme: "file", language: "markdown", pattern: "**/bdd.config.md" },
    ],
    initializationOptions: {
      solutionDir: vscode.workspace.workspaceFolders?.[0]?.uri.fsPath,
      // AD-025: a suite's runner kept ready, from a copy of its build output.
      warmRunner: vscode.workspace.getConfiguration("bruinetBdd").get<boolean>("warmRunner", true),
      warmRunnerIdleMinutes: vscode.workspace.getConfiguration("bruinetBdd").get<number>("warmRunnerIdleMinutes", 10),
    },
    outputChannelName: "Bruinet BDD Language Server",
  };
  client = new LanguageClient("bruinetBdd", "Bruinet BDD", serverOptions, clientOptions);
  await client.start();

  const tests = new ScenarioTests(client, (line, file, paused) => preview.running(line, file, paused), {
    started: () => stepping.started(),
    get isStepping() {
      return stepping.isStepping;
    },
  });
  const preview = new ScenarioPreview(client, {
    run: (uri, line, debug) => void tests.runAt(uri, line, debug),
    runOnRunner: (uri, line) => void tests.runOnRunnerAt(uri, line),
    step: (uri, line) => void tests.stepAt(uri, line),
    record: (uri, line, suite) => void tests.record(uri, line, suite).then(() => preview.refresh()),
    cancel: () => tests.cancel(),
    move: (move) => void stepping.move(move as Move),
  });
  const stepping = new BddStepping(client, preview, () => tests.stepPaused(false));

  // Step through (bruinet/step/*): the pause in the editor and the bar, the run's state in Test Explorer.
  const stepEvents = new vscode.EventEmitter<StepEvent>();
  client.onNotification("bruinet/step/paused", async (paused: StepPaused) => {
    tests.stepPaused(true);
    await stepping.onStepPaused(paused);
    stepEvents.fire({ kind: "paused", paused });
  });
  client.onNotification("bruinet/step/ended", async (ended: { status: string; message?: string }) => {
    tests.stepEnded(ended.status, ended.message);
    await stepping.onStepEnded();
    stepEvents.fire({ kind: "ended", ...ended });
  });
  tests.onBuild((build) => stepEvents.fire({ kind: "build", ...build }));
  client.onNotification("bruinet/run/ended", (ended: { status: string; message?: string; results: { scenario: string; status: string; message?: string }[] }) => {
    tests.runnerEnded(ended);
    stepEvents.fire({ kind: "runEnded", status: ended.status, results: ended.results });
  });
  const gutter = vscode.window.createTextEditorDecorationType({
    gutterIconPath: context.asAbsolutePath(path.join("media", "breakpoint.svg")),
    gutterIconSize: "70%",
  });
  const showBreakpoints = (editor: vscode.TextEditor | undefined) => {
    if (editor && featureOrSteps.test(editor.document.fileName)) {
      editor.setDecorations(gutter, breakpoints.linesOf(editor.document.uri.fsPath).map((line) => new vscode.Range(line - 1, 0, line - 1, 0)));
    }
  };

  context.subscriptions.push(
    client,
    tests,
    preview,
    stepping,
    gutter,
    vscode.commands.registerCommand("bruinetBdd.stepOver", () => stepping.move("over")),
    vscode.commands.registerCommand("bruinetBdd.stepInto", () => stepping.move("into")),
    vscode.commands.registerCommand("bruinetBdd.stepOut", () => stepping.move("out")),
    vscode.commands.registerCommand("bruinetBdd.continue", () => stepping.move("continue")),
    vscode.commands.registerCommand("bruinetBdd.stepThrough", async (uri?: vscode.Uri, line?: number) => {
      // The scenario at the given heading, or the one the caret is in.
      const editor = vscode.window.activeTextEditor;
      const target = uri ?? editor?.document.uri;
      const heading = line ?? (editor ? headingAbove(editor.document, editor.selection.active.line) : undefined);
      if (target && heading) {
        return tests.stepAt(target, heading);
      }

      void vscode.window.showWarningMessage("Put the caret in a scenario of a feature file to step through it.");
      return "No scenario at the caret.";
    }),
    vscode.commands.registerCommand("bruinetBdd.runScenario", async (uri?: vscode.Uri, line?: number) => {
      // On the suite's runner: the scenario at the given heading, or the one the caret is in.
      const editor = vscode.window.activeTextEditor;
      const target = uri ?? editor?.document.uri;
      const heading = line ?? (editor ? headingAbove(editor.document, editor.selection.active.line) : undefined);
      if (target && heading) {
        return tests.runOnRunnerAt(target, heading);
      }

      void vscode.window.showWarningMessage("Put the caret in a scenario of a feature file to run it.");
      return "No scenario at the caret.";
    }),
    vscode.commands.registerCommand("bruinetBdd.stop", () => stepping.move("stop")),
    vscode.commands.registerCommand("bruinetBdd.debugFromHere", async () => {
      const refused = await stepping.debugFromHere();
      if (refused) {
        void vscode.window.showWarningMessage(`Debug from here: ${refused}`);
      }

      return refused;
    }),
    vscode.commands.registerCommand("bruinetBdd.openPreview", async () => {
      const document = vscode.window.activeTextEditor?.document;
      if (document && previewable.test(document.fileName)) {
        await preview.open(document);
      }
    }),
    vscode.commands.registerCommand("bruinetBdd.toggleStepBreakpoint", () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && featureOrSteps.test(editor.document.fileName)) {
        breakpoints.toggle(editor.document.uri.fsPath, editor.selection.active.line + 1);
      }
    }),
    vscode.commands.registerCommand("bruinetBdd.refreshTests", () => tests.discover()),
    vscode.commands.registerCommand("bruinetBdd.deleteSuiteResults", async () => {
      // The suites as the BDD Explorer lists them; the picked one loses every scenario result.
      const suites = (await client!.sendRequest<{ suites: string[] }>("bruinet/explorer", { view: "results" })).suites;
      const suite = suites.length === 1 ? suites[0] : await vscode.window.showQuickPick(suites, { placeHolder: "The suite whose results to delete" });
      if (suite) {
        await deleteResults(client!, { scope: "suite", suite });
        await preview.refresh();
      }
    }),
    breakpoints.onDidChangeBreakpoints(() => vscode.window.visibleTextEditors.forEach(showBreakpoints)),
    vscode.window.onDidChangeActiveTextEditor(showBreakpoints),
    vscode.workspace.onDidChangeTextDocument((change) => {
      if (!featureOrSteps.test(change.document.fileName)) {
        return;
      }

      // Last change first: each one's line is then still where the file had it before.
      for (const edit of [...change.contentChanges].sort((a, b) => b.range.start.line - a.range.start.line)) {
        const delta = edit.text.split("\n").length - 1 - (edit.range.end.line - edit.range.start.line);
        breakpoints.shift(change.document.uri.fsPath, edit.range.start.line + 1, delta);
      }
    }),
    vscode.workspace.onDidSaveTextDocument((document) => {
      if (featureOrSteps.test(document.fileName)) {
        void tests.discover();
      }
    }),
  );

  vscode.window.visibleTextEditors.forEach(showBreakpoints);
  context.subscriptions.push(stepEvents);
  return { onStep: stepEvents.event };
}

// The scenario heading at or above a 0-based line, as a 1-based line.
function headingAbove(document: vscode.TextDocument, line: number): number | undefined {
  for (let at = line; at >= 0; at--) {
    if (/^##\s+Scenario( Outline)?:/i.test(document.lineAt(at).text)) {
      return at + 1;
    }
  }

  return undefined;
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}

// The server: the setting, else the one packed in the extension, else the repository's own build.
function serverPath(context: vscode.ExtensionContext): string | undefined {
  const configured = vscode.workspace.getConfiguration("bruinetBdd").get<string>("serverPath");
  if (configured && fs.existsSync(configured)) {
    return configured;
  }

  const packed = context.asAbsolutePath(path.join("server", "bruinet.dll"));
  if (fs.existsSync(packed)) {
    return packed;
  }

  for (const folder of vscode.workspace.workspaceFolders ?? []) {
    for (let at = folder.uri.fsPath; ; at = path.dirname(at)) {
      const built = path.join(at, "tools", "Bruinet.Cli", "bin", "Debug", "net10.0", "bruinet.dll");
      if (fs.existsSync(built)) {
        return built;
      }

      if (path.dirname(at) === at) {
        break;
      }
    }
  }

  return undefined;
}
