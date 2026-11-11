// The tests run.ts starts inside VS Code: Step through Todo's TOD.01.1 as a user does, through the
// extension's commands (the keys F10, F11 and F5 are bound to them), with a breakpoint at line 24.
import * as assert from "assert";
import * as childProcess from "child_process";
import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";
import type { BruinetBddApi, StepEvent } from "../extension";
import type { StepPaused } from "../stepping";

const workspace = process.env.BRUINET_TODO!;
const feature = path.join(workspace, "tests", "Bruinet.Samples.Todo.Uat", "Features", "TOD.01-see-my-todos.feature.md");
const heading = 15;

// Named waits: the first stop comes after a build, the runner's start and the app's.
const firstStopMs = 5 * 60_000;
const nextStopMs = 60_000;
// At most how long the language server takes to look at a written file.
const watcherSettleMs = 1_000;
const breakpointsFile = path.join(process.env.LOCALAPPDATA!, "Bruinet", "bdd-breakpoints.json");

// Polls a condition the extension cannot announce: an app process the runner starts.
async function waitFor(condition: () => boolean, withinMs: number, message: string): Promise<void> {
  const deadline = Date.now() + withinMs;
  while (!condition()) {
    assert.ok(Date.now() < deadline, message);
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
}

// How often waitFor looks.
const pollMs = 250;

// The Todo app's processes now, by id.
function todoApps(): number[] {
  const list = childProcess.execFileSync("tasklist", ["/FI", "IMAGENAME eq Bruinet.Samples.Todo.App.exe", "/FO", "CSV", "/NH"], { encoding: "utf8" });
  return [...list.matchAll(/^"[^"]+","(\d+)"/gm)].map((match) => Number(match[1])).sort((a, b) => a - b);
}

export async function run(): Promise<void> {
  const extension = vscode.extensions.getExtension<BruinetBddApi>("bruinet.bruinet-bdd");
  assert.ok(extension, "The extension is not loaded.");
  const api = await extension.activate();
  assert.ok(api, "The extension did not start: is tools/Bruinet.Cli built?");

  const events: StepEvent[] = [];
  const builds: boolean[] = [];
  let arrived: (() => void) | undefined;
  api.onStep((event) => {
    if (event.kind === "build") {
      builds.push(event.built);
      return;
    }

    events.push(event);
    arrived?.();
  });
  const next = async (withinMs: number): Promise<StepEvent> => {
    const deadline = Date.now() + withinMs;
    while (events.length === 0) {
      const left = deadline - Date.now();
      assert.ok(left > 0, `No step event within ${withinMs} ms.`);
      await new Promise<void>((resolve) => {
        arrived = resolve;
        setTimeout(resolve, left);
      });
    }

    return events.shift()!;
  };
  const pausedAt = async (line: number, withinMs = nextStopMs): Promise<StepPaused> => {
    const event = await next(withinMs);
    assert.strictEqual(event.kind, "paused", `Expected a stop at line ${line}, got ${JSON.stringify(event)}.`);
    assert.strictEqual(event.kind === "paused" && event.paused.line, line);
    const editor = vscode.window.activeTextEditor;
    assert.strictEqual(editor?.document.uri.fsPath.toLowerCase(), feature.toLowerCase(), "The feature file is not in front.");
    assert.strictEqual(editor.selection.active.line + 1, line, "The caret is not at the stop.");
    const paused = (event as Extract<StepEvent, { kind: "paused" }>).paused;
    console.log(`paused at line ${line}` + (paused.app ? ` app ${paused.app}` : "") + (paused.page ? ` page ${paused.page}` : "") + (paused.arguments ? ` ${JSON.stringify(paused.arguments)}` : ""));
    return paused;
  };

  fs.mkdirSync(path.dirname(breakpointsFile), { recursive: true });
  fs.writeFileSync(breakpointsFile, JSON.stringify({ files: { [feature]: [24] } }));
  await vscode.window.showTextDocument(vscode.Uri.file(feature));

  const refused = await vscode.commands.executeCommand<string | undefined>("bruinetBdd.stepThrough", vscode.Uri.file(feature), heading);
  assert.strictEqual(refused, undefined, `The Step-through run did not start: ${refused}`);
  await pausedAt(18, firstStopMs);

  await vscode.commands.executeCommand("bruinetBdd.stepOver");
  await pausedAt(19);

  // F11 at a step that is no call: the next step, as Step into is in a debugger.
  await vscode.commands.executeCommand("bruinetBdd.stepInto");
  await pausedAt(23);

  // F5: on to the breakpoint.
  await vscode.commands.executeCommand("bruinetBdd.continue");
  // The stop says what the step works with (D5): the page the step before left, the arguments as read.
  const atBreakpoint = await pausedAt(24);
  assert.strictEqual(atBreakpoint.page, "Todo List");
  assert.deepStrictEqual(atBreakpoint.arguments, { control: "Sync" });

  for (const line of [25, 26, 27]) {
    await vscode.commands.executeCommand("bruinetBdd.stepOver");
    await pausedAt(line);
  }

  await vscode.commands.executeCommand("bruinetBdd.stepOver");
  const ended = await next(nextStopMs);
  assert.ok(ended.kind === "ended" && ended.status === "passed", `The run did not pass: ${JSON.stringify(ended)}.`);
  console.log("ended: passed");
  assert.deepStrictEqual(builds, [true], "The first Step-through run builds the suite's project.");

  // I1: the runner reads the scenario files from the source, so a change to one alone needs no build.
  // Waited on the language server's next look at the file, bounded, rather than a guess (review 02, finding 12).
  const looked = new Promise<void>((resolve) => {
    const watching = vscode.languages.onDidChangeDiagnostics((change) => {
      if (change.uris.some((uri) => uri.fsPath === vscode.Uri.file(feature).fsPath)) {
        watching.dispose();
        resolve();
      }
    });
  });
  fs.writeFileSync(feature, fs.readFileSync(feature));
  await Promise.race([looked, new Promise((resolve) => setTimeout(resolve, watcherSettleMs))]);
  const again = await vscode.commands.executeCommand<string | undefined>("bruinetBdd.stepThrough", vscode.Uri.file(feature), heading);
  assert.strictEqual(again, undefined, `The second Step-through run did not start: ${again}`);
  await pausedAt(18, firstStopMs);
  assert.deepStrictEqual(builds, [true, false], "A feature file's change alone built the project again.");
  console.log("second run: not built");
  await vscode.commands.executeCommand("bruinetBdd.stop");
  const stopped = await next(nextStopMs);
  assert.ok(stopped.kind === "ended" && stopped.status === "stopped", `The second run did not stop: ${JSON.stringify(stopped)}.`);

  // I4: the preview's Run is the runner's, with no stops (the breakpoint at 24 is passed), no build.
  const run = await vscode.commands.executeCommand<string | undefined>("bruinetBdd.runScenario", vscode.Uri.file(feature), heading);
  assert.strictEqual(run, undefined, `The run on the runner did not start: ${run}`);
  const ran = await next(firstStopMs);
  assert.ok(ran.kind === "runEnded" && ran.status === "passed", `The run on the runner did not pass: ${JSON.stringify(ran)}.`);
  assert.deepStrictEqual(builds, [true, false, false], "A run on the runner built though nothing but a feature file changed.");
  console.log(`run on the runner: ${ran.status}`);

  // AD-025: Todo's fixture starts each scenario clean on the app as it is (its data reset in place),
  // so the warm runner keeps the app: the second run uses the same app process, and passes.
  await waitFor(() => todoApps().length === 1, nextStopMs, "The Todo app is not open after the run.");
  const apps = todoApps();
  await vscode.commands.executeCommand("bruinetBdd.runScenario", vscode.Uri.file(feature), heading);
  const ranAgain = await next(firstStopMs);
  assert.ok(ranAgain.kind === "runEnded" && ranAgain.status === "passed", `The second run on the runner did not pass: ${JSON.stringify(ranAgain)}.`);
  assert.deepStrictEqual(todoApps(), apps, "The second run started another app.");
  console.log(`second run on the runner: ${ranAgain.status}, same app ${apps[0]}`);
}
