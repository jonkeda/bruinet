using Bruinet.Bdd.Preview;

namespace Bruinet.Bdd.LanguageServer;

// The Scraper view: one page both IDEs show (a VS Code webview, a Visual Studio tool window). All its
// logic is here; an IDE only relays. The page sends {type: "call", id, method, params} for a
// bruinet/scraper/* request and gets {type: "result", id, value}; the IDE forwards
// bruinet/scraper/changed as {type: "changed", ...}, and does "open" (a file), "diff" (two files),
// "chat" (a prompt for the agent) and "addProject" (into the solution) itself.
//
// The view is a workflow of eight steps: each shows its state
// from bruinet/scraper/progress and one primary action; the header says what to do next.
internal static class ScraperPage
{
    public static string Html { get; } = $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta name="color-scheme" content="light dark">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
        <title>Scraper</title>
        <style>
        {{BddPreview.Stylesheet}}
        body { padding: 0; margin: 0; }
        button, .sc-small { background: var(--uat-surface); color: var(--uat-fg); border: 1px solid var(--uat-border); border-radius: 3px; padding: 0 0.7em; cursor: pointer; font: inherit; height: 1.9em; white-space: nowrap; box-sizing: border-box; }
        button:disabled { opacity: 0.5; cursor: default; }
        button.sc-primary { background: var(--uat-accent); color: var(--uat-bg); border-color: var(--uat-accent); font-weight: 600; }
        button.sc-link { background: none; border: none; color: var(--uat-accent); padding: 0; height: auto; }
        input, select, textarea { font: inherit; color: var(--uat-fg); background: var(--uat-bg); border: 1px solid var(--uat-border); border-radius: 3px; box-sizing: border-box; }
        input:not([type=checkbox]):not([type=radio]), select { height: 1.9em; padding: 0 0.4em; }
        input[type=number] { width: 6em; }
        .sc-changed { outline: 2px solid var(--uat-accent); outline-offset: -1px; }
        .sc-top { position: sticky; top: 0; z-index: 3; background: var(--uat-bg); }
        .sc-header { display: flex; flex-wrap: wrap; gap: 0.6em; align-items: end; padding: 0.5em 0.8em; border-bottom: 1px solid var(--uat-border); }
        .sc-field { display: grid; gap: 0.1em; }
        .sc-field > label { font-size: 0.8em; color: var(--uat-muted); }
        .sc-field-row { display: flex; gap: 0.2em; }
        .sc-next { margin-left: auto; display: flex; gap: 0.6em; align-items: center; background: var(--uat-surface); border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.2em 0.2em 0.2em 0.7em; }
        .sc-strip { display: flex; flex-wrap: wrap; gap: 0.4em; align-items: center; padding: 0.4em 0.8em; border-bottom: 1px solid var(--uat-border); background: var(--uat-surface); }
        .sc-strip[hidden] { display: none; }
        .sc-status { color: var(--uat-muted); }
        .sc-status[data-recording="true"] { color: var(--uat-failed); }
        .sc-problem { color: var(--uat-failed); padding: 0.3em 0.8em; }
        .sc-notice { display: flex; flex-wrap: wrap; gap: 0.5em; align-items: center; padding: 0.4em 0.8em; border-bottom: 1px solid var(--uat-border); background: var(--uat-surface); }
        .sc-notice[hidden] { display: none; }
        .sc-notice span { flex: 1; min-width: 14em; }
        .sc-body { display: grid; grid-template-columns: 14em 1fr; min-height: 30em; }
        .sc-steps { border-right: 1px solid var(--uat-border); padding: 0.5em 0; }
        .sc-step { display: grid; grid-template-columns: 1.6em 1fr auto; gap: 0.4em; align-items: center; padding: 0.35em 0.8em; cursor: pointer; border-left: 3px solid transparent; }
        .sc-step:hover, .sc-aside:hover { background: var(--uat-surface); }
        .sc-step[aria-current="true"] { background: var(--uat-surface); border-left-color: var(--uat-accent); }
        .sc-mark { width: 1.5em; height: 1.5em; border-radius: 50%; display: grid; place-items: center; font-size: 0.8em; border: 1px solid var(--uat-border); color: var(--uat-muted); }
        .sc-step[data-state="Done"] .sc-mark { background: var(--uat-passed); border-color: var(--uat-passed); color: var(--uat-bg); }
        .sc-step[data-state="Next"] .sc-mark { border: 2px solid var(--uat-accent); color: var(--uat-accent); font-weight: 700; }
        .sc-step[data-state="Attention"] .sc-mark { background: var(--uat-skipped, #b26a00); border-color: var(--uat-skipped, #b26a00); color: var(--uat-bg); }
        .sc-step .sc-sum { grid-column: 2 / 4; font-size: 0.8em; color: var(--uat-muted); margin-top: -0.3em; }
        .sc-badge { font-size: 0.8em; padding: 0 0.5em; border-radius: 1em; background: var(--uat-accent); color: var(--uat-bg); }
        .sc-steps hr { border: none; border-top: 1px solid var(--uat-border); margin: 0.5em 0.8em; }
        .sc-aside { padding: 0.25em 0.8em 0.25em 2.8em; cursor: pointer; color: var(--uat-muted); }
        .sc-panel { padding: 0.8em 1.1em; display: flex; flex-direction: column; gap: 0.7em; min-width: 0; }
        .sc-panel h2 { margin: 0; font-size: 1.15em; }
        .sc-panel h3 { margin: 0.2em 0 0; font-size: 1em; }
        .sc-why { color: var(--uat-muted); margin: -0.4em 0 0; }
        .sc-intro { border: 1px dashed var(--uat-accent); border-radius: 4px; padding: 0.5em 0.8em; }
        .sc-intro ol { margin: 0.4em 0 0.3em; padding-left: 1.4em; }
        .sc-intro p { margin: 0; color: var(--uat-muted); }
        .sc-facts { display: flex; gap: 0.6em; flex-wrap: wrap; }
        .sc-fact { border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.3em 0.7em; min-width: 7em; }
        .sc-fact b { display: block; font-size: 1.3em; }
        .sc-fact span { color: var(--uat-muted); font-size: 0.8em; }
        table.sc-table { border-collapse: collapse; width: 100%; }
        .sc-table th, .sc-table td { text-align: left; padding: 0.3em 0.5em; border-bottom: 1px solid var(--uat-border); vertical-align: middle; }
        .sc-table th { font-size: 0.8em; color: var(--uat-muted); font-weight: 600; }
        .sc-actions { display: flex; flex-wrap: wrap; gap: 0.5em; align-items: center; }
        /* A step's main actions, under the header: they stay in sight while the step scrolls. */
        .sc-bar { position: sticky; top: var(--sc-top, 0); z-index: 2; background: var(--uat-bg); padding: 0.4em 0; border-bottom: 1px solid var(--uat-border); }
        details.sc-expander > summary { cursor: pointer; font-weight: 600; }
        details.sc-expander > summary .sc-muted { font-weight: normal; }
        details.sc-expander > fieldset { margin-top: 0.4em; }
        .sc-collected { max-height: 16em; overflow: auto; border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.2em; }
        .sc-collected .sc-item[aria-current="true"] { outline: 1px solid var(--uat-accent); }
        details.sc-more summary { cursor: pointer; color: var(--uat-accent); }
        details.sc-more > div { display: flex; flex-wrap: wrap; gap: 0.4em; margin-top: 0.4em; }
        .sc-footer { margin-top: auto; display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--uat-border); padding-top: 0.6em; }
        fieldset { border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.4em 0.8em 0.6em; margin: 0; }
        legend { padding: 0 0.3em; font-weight: 600; }
        .sc-grid { display: grid; grid-template-columns: max-content minmax(8em, 1fr) max-content minmax(8em, 1fr); gap: 0.35em 0.8em; align-items: center; }
        .sc-grid .sc-wide { grid-column: 2 / -1; display: flex; gap: 0.4em; flex-wrap: wrap; align-items: center; }
        .sc-chip { border: 1px solid var(--uat-border); border-radius: 1em; padding: 0 0.6em; display: inline-flex; gap: 0.3em; align-items: center; }
        .sc-item { padding: 0.15em 0.4em; border-radius: 3px; cursor: pointer; }
        .sc-item:hover, .sc-item[aria-current="true"] { background: var(--uat-surface); }
        .sc-sub { padding-left: 1.2em; color: var(--uat-muted); font-size: 0.9em; }
        .sc-muted { color: var(--uat-muted); }
        .sc-ok { color: var(--uat-passed); } .sc-warn { color: var(--uat-skipped, #b26a00); } .sc-refused { color: var(--uat-failed); }
        .sc-split { display: grid; grid-template-columns: minmax(12em, 1fr) minmax(0, 2fr); gap: 0.8em; }
        pre.sc-outline, pre.sc-code { font-family: var(--uat-mono); font-size: 0.85em; margin: 0; white-space: pre; overflow: auto; max-height: 26em; border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.4em; }
        pre.sc-outline span { display: block; cursor: pointer; border-radius: 2px; }
        pre.sc-outline span:hover { background: var(--uat-surface); }
        pre.sc-outline span[aria-current="true"] { outline: 1px solid var(--uat-accent); }
        pre.sc-summary { font-family: var(--uat-mono); font-size: 0.85em; white-space: pre-wrap; border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.4em; margin: 0; }
        .sc-node { border: 1px solid var(--uat-border); border-radius: 4px; padding: 0.4em; }
        .sc-test-steps { margin: 0.3em 0; padding-left: 1.6em; }
        .sc-output { max-height: 20em; overflow: auto; font-size: 0.85em; white-space: pre-wrap; background: var(--uat-surface); padding: 0.4em; }
        .sc-form { display: grid; grid-template-columns: max-content 1fr; gap: 0.3em 0.6em; align-items: center; }
        .sc-row { display: flex; gap: 0.4em; align-items: center; padding: 0.1em 0; }
        .sc-row .sc-grow { flex: 1; overflow-wrap: anywhere; }
        textarea.sc-note { width: 100%; min-height: 4em; padding: 0.3em 0.4em; }
        .sc-two { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 0.8em; }
        /* A tool window docked narrow: the steps become a row of numbered marks. */
        @media (max-width: 640px) {
          .sc-body { grid-template-columns: 1fr; }
          .sc-steps { border-right: none; border-bottom: 1px solid var(--uat-border); display: flex; flex-wrap: wrap; gap: 0.3em; padding: 0.4em; }
          .sc-step { grid-template-columns: auto; padding: 0.1em; border-left: none; }
          .sc-step .sc-label, .sc-step .sc-sum, .sc-step .sc-badge, .sc-steps hr { display: none; }
          .sc-step[aria-current="true"] .sc-mark { outline: 2px solid var(--uat-accent); outline-offset: 1px; }
          .sc-aside { padding: 0.1em 0.4em; }
          .sc-next { margin-left: 0; width: 100%; justify-content: space-between; }
          .sc-two, .sc-split, .sc-grid { grid-template-columns: 1fr; }
        }
        </style>
        </head>
        <body>
        <div class="sc-top" id="sc-top">
        <header class="sc-header">
          <div class="sc-field">
            <label for="sc-project">Project</label>
            <div class="sc-field-row">
              <select id="sc-project"></select>
              <button id="sc-new-project" type="button" title="New scraped project" aria-label="New scraped project">+</button>
            </div>
          </div>
          <div class="sc-field" id="sc-chat">
            <label for="sc-agent">Agent and model</label>
            <div class="sc-field-row">
              <select id="sc-agent" title="The chat agent (or mode) the prompts run in">
                <option value="bruinet-scraper">Bruinet scraper</option>
                <option value="agent">Agent</option>
                <option value="ask">Ask</option>
              </select>
              <select id="sc-model" aria-label="Model" title="The model the prompts run with"><option value="">The chat's model</option></select>
            </div>
          </div>
          <div class="sc-next" id="sc-next" hidden></div>
        </header>
        <div class="sc-strip" id="sc-strip" hidden>
          <span id="sc-status" class="sc-status"></span>
          <button id="sc-capture" type="button">Capture</button>
          <button id="sc-pick" type="button">Pick</button>
          <button id="sc-assert" type="button" hidden title="Click an element in the browser, then choose what to check">Assert</button>
          <button id="sc-stop" type="button">Stop</button>
          <button id="sc-continue" type="button" hidden title="Signed in in the browser: go on">Continue</button>
          <select id="sc-here" aria-label="Add this page to the config">
            <option value="">This page…</option>
            <option value="host">Add its host to Explore hosts</option>
            <option value="alias">Add it as an alias</option>
            <option value="start">Use it as start URL</option>
            <option value="never">Never visit it</option>
          </select>
        </div>
        <div id="sc-problem" class="sc-problem" hidden></div>
        <div id="sc-notice" class="sc-notice" role="status" hidden></div>
        </div>
        <div class="sc-body">
          <nav class="sc-steps" id="sc-steps" aria-label="Steps"></nav>
          <main class="sc-panel" id="sc-panel"></main>
        </div>
        <script>
        (function () {
          "use strict";
          const host = (function () {
            if (window.chrome && window.chrome.webview) {
              return { post: m => window.chrome.webview.postMessage(m), on: f => window.chrome.webview.addEventListener("message", e => f(e.data)) };
            }
            const api = typeof acquireVsCodeApi === "function" ? acquireVsCodeApi() : { postMessage: m => window.parent.postMessage(m, "*") };
            return { post: m => api.postMessage(m), on: f => window.addEventListener("message", e => f(e.data)) };
          })();
          const $ = id => document.getElementById(id);
          const el = (tag, props, ...children) => {
            const e = document.createElement(tag);
            for (const [k, v] of Object.entries(props || {})) {
              if (k === "class") e.className = v; else if (k.startsWith("on")) e.addEventListener(k.slice(2), v); else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? "" : v);
            }
            for (const c of children.flat()) if (c !== undefined && c !== null && c !== false) e.append(c);
            return e;
          };
          const button = (text, onclick, props) => el("button", Object.assign({ type: "button", onclick }, props || {}), text);
          const primary = (text, onclick, props) => button(text, onclick, Object.assign({ class: "sc-primary", id: "sc-primary" }, props || {}));
          const more = (...items) => el("details", { class: "sc-more" }, el("summary", {}, "More"), el("div", {}, ...items));
          const plural = (n, word) => `${n} ${word}${n === 1 ? "" : "s"}`;

          // Requests go through the IDE to the server; each answer comes back by its id.
          let nextId = 1;
          const waiting = new Map();
          const call = (method, params) => new Promise(resolve => {
            const id = nextId++;
            waiting.set(id, resolve);
            host.post({ type: "call", id, method: "bruinet/scraper/" + method, params: params || {} });
          });
          // A ScraperResult: the value, or the problem shown under the header. A problem stays until
          // the call that raised it succeeds, another step opens, or another problem replaces it:
          // the refresh after a failed call must not wipe it.
          const answer = async (method, params) => {
            const result = await call(method, params);
            if (result && typeof result === "object" && "problem" in result) {
              if (result.problem) showProblem(result.problem, method);
              else if (showProblem.from === method) showProblem(null);
              return result.problem ? undefined : result.value;
            }
            return result;
          };
          const showProblem = (text, from) => { showProblem.from = text ? from : null; $("sc-problem").hidden = !text; $("sc-problem").textContent = text || ""; };
          const say = text => { $("sc-status").textContent = text || ""; if (text && !state.recording && !state.exploring) { $("sc-strip").hidden = false; clearTimeout(say.timer); say.timer = setTimeout(showRunning, 6000); } };

          const STEPS = ["Project", "Connect", "Collect pages", "Review", "Controls", "Page objects", "Verify", "Tests"];
          const state = {
            project: null, projects: [], step: null, progress: null, recording: null, live: null, exploring: null, explored: null,
            pages: [], page: null, capture: null, node: null, reviewing: null, picked: new Set(), pickedPages: new Set(), names: {},
            collected: [], guardsOpen: false, chat: { agent: "bruinet-scraper", model: "" }, models: [],
          };

          host.on(message => {
            if (message.type === "result") { const done = waiting.get(message.id); waiting.delete(message.id); if (done) done(message.value); return; }
            if (message.type === "theme") {
              for (const [name, value] of Object.entries(message.vars || {})) document.documentElement.style.setProperty(name, value);
              if (message.kind) document.documentElement.setAttribute("data-theme", message.kind);
              return;
            }
            if (message.type === "changed") { onChanged(message); }
            if (message.type === "chatted") { showChatted(message); }
            if (message.type === "chatDone") { chatDone(); }
            if (message.type === "agents") { showModels(message); }
            // The IDE opened another solution: its projects.
            if (message.type === "projects") { loadProjects(); }
          });

          // ---- Projects, progress and the step list ----

          async function loadProjects() {
            state.projects = await call("projects") || [];
            const picker = $("sc-project");
            picker.replaceChildren(...state.projects.map(p => {
              const option = new Option(p.label, p.label, false, p.label === state.project);
              option.title = p.folder;
              return option;
            }));
            if (state.projects.length === 0) {
              state.project = null;
              showProblem("No project here has a scraper.config.md yet.", "projects");
              $("sc-steps").replaceChildren();
              $("sc-next").hidden = true;
              await showNewProject();
              return;
            }
            // Found now (the IDE opened a solution, or a project was made): the earlier "none" no longer holds.
            if (showProblem.from === "projects") showProblem(null);
            state.project = picker.value;
            state.recording = await call("state");
            showRunning();
            await loadChatChoice();
            await refresh(true);
          }

          const projectInfo = () => state.projects.find(p => p.label === state.project) || {};

          // The steps' state from the server; the first time, the step to do next opens.
          async function refresh(open) {
            if (!state.project) return;
            state.progress = await answer("progress", { project: state.project });
            const p = state.progress;
            if (!p) return;
            if (open || state.step === null) state.step = p.next ? p.next.step : 8;
            renderSteps();
            await showStep();
          }

          function renderSteps() {
            const p = state.progress;
            const nav = $("sc-steps");
            nav.replaceChildren(...p.steps.map(s => el("div", {
                class: "sc-step", id: `sc-step-${s.step}`, role: "button", tabindex: "0", "data-state": s.state, "aria-current": String(s.step === state.step),
                title: `${s.step} ${s.name}${s.summary ? ": " + s.summary : ""}`, onclick: () => go(s.step), onkeydown: e => { if (e.key === "Enter") go(s.step); },
              },
              el("span", { class: "sc-mark" }, s.state === "Done" ? "✓" : s.state === "Attention" ? "!" : String(s.step)),
              el("span", { class: "sc-label" }, s.name),
              s.count ? el("span", { class: "sc-badge" }, String(s.count)) : el("span", {}),
              s.summary ? el("span", { class: "sc-sum" }, s.summary) : null)),
              el("hr"),
              el("div", { class: "sc-aside", id: "sc-code", role: "button", tabindex: "0", "aria-current": String(state.step === "code"), onclick: () => go("code") }, "Code"));
            const next = $("sc-next");
            next.hidden = !p.next;
            if (p.next) next.replaceChildren(el("span", {}, "Next: ", el("b", {}, p.next.text)), button("Go", () => go(p.next.step), { class: "sc-primary", id: "sc-go" }));
          }

          async function go(step) {
            state.step = step;
            state.reviewing = null;
            state.capture = null;
            showProblem(null);
            renderSteps();
            await showStep();
          }

          async function showStep() {
            const panel = $("sc-panel");
            panel.replaceChildren();
            const s = state.step;
            if (s === "code") return showCode();
            const body = await [null, stepProject, stepConnect, stepCollect, stepReview, stepControls, stepPages, stepVerify, stepTests][s]();
            if (state.step !== s) return; // another step was chosen meanwhile
            const parts = [].concat(body || []).flat().filter(x => x !== null && x !== undefined && x !== false);
            const bar = parts.find(x => x.classList && x.classList.contains("sc-actions"));
            if (bar) {
              parts.splice(parts.indexOf(bar), 1);
              bar.classList.add("sc-bar");
              parts.splice(parts.findIndex(x => x.classList && x.classList.contains("sc-why")) + 1, 0, bar);
            }
            panel.replaceChildren(...parts, footer(s));
          }

          function footer(step) {
            const skip = step === 5 ? button(state.progress && state.progress.skipControls ? "Ask for controls again" : "Skip: page objects without controls", async () => {
              await answer("skipControls", { project: state.project, skip: !(state.progress && state.progress.skipControls) });
              await refresh(false);
            }, { class: "sc-link", id: "sc-skip" }) : el("span");
            return el("div", { class: "sc-footer" }, skip, step < 8 ? button(`Next: ${STEPS[step]} ▸`, () => go(step + 1), { id: "sc-step-next" }) : el("span"));
          }

          const heading = (n, why) => [el("h2", {}, `${n} ${STEPS[n - 1]}`), el("p", { class: "sc-why" }, why)];

          // ---- 1 Project ----

          async function stepProject() {
            const p = projectInfo();
            const c = await answer("config", { project: state.project });
            const platform = el("select", { id: "sc-platform" }, ...[["html", "Html"], ["blazor", "Blazor"]].map(([v, t]) => new Option(t, v, false, v === p.platform)));
            const start = el("input", { id: "sc-start-url", value: p.startUrl || "", placeholder: "https://localhost:5001/", style: "width: 24em" });
            const changes = () => [
              platform.value !== p.platform ? { action: "set", section: "App", values: ["Platform", platform.value] } : null,
              start.value.trim() !== (p.startUrl || "") ? { action: "set", section: "App", values: ["StartUrl", start.value.trim()] } : null].filter(x => x);
            const save = button("Save", async () => {
              const said = await answer("configEdits", { project: state.project, changes: changes() });
              if (said === undefined) return;
              say(said.join(" "));
              await loadProjectsQuietly();
              await refresh(false);
            }, { id: "sc-app-save", disabled: true });
            const update = () => {
              platform.classList.toggle("sc-changed", platform.value !== p.platform);
              start.classList.toggle("sc-changed", start.value.trim() !== (p.startUrl || ""));
              save.disabled = changes().length === 0;
            };
            platform.addEventListener("change", update);
            start.addEventListener("input", update);
            return [...heading(1, "The UI test project the page objects go into."),
              el("table", { class: "sc-table" },
                el("tr", {}, el("th", {}, "Folder"), el("td", {}, el("code", {}, p.folder || ""))),
                el("tr", {}, el("th", {}, el("label", { for: "sc-platform" }, "Platform")), el("td", {}, platform)),
                el("tr", {}, el("th", {}, el("label", { for: "sc-start-url" }, "Start URL")), el("td", {}, start)),
                el("tr", {}, el("th", {}, "Recordings"), el("td", {}, plural((p.sessions || []).length, "session")))),
              el("div", { class: "sc-row" }, save),
              el("div", { class: "sc-actions" }, primary("New project…", showNewProject),
                more(c ? button("Open scraper.config.md", () => host.post({ type: "open", path: c.path })) : null, button("Add a config to an existing project", showCreateConfig)))];
          }

          // A new scraped project: the server writes it, the IDE adds it to the solution.
          async function showNewProject() {
            const field = (label, control, key) => [el("label", { for: control.id }, label), el("div", {}, control, el("div", { class: "sc-refused", "data-problem": key }))];
            const radio = (group, value, text, checked) => el("label", {}, el("input", { type: "radio", name: group, value, checked }), " " + text);
            const name = el("input", { id: "sc-np-name", value: "MyApp.UITests" });
            const folder = el("input", { id: "sc-np-folder" });
            const platform = el("span", { id: "sc-np-platform" }, radio("sc-np-platform", "html", "Html", true), " ", radio("sc-np-platform", "blazor", "Blazor"));
            const url = el("input", { id: "sc-np-url", placeholder: "https://localhost:5001/" });
            const ids = el("input", { id: "sc-np-ids", value: "data-testid, data-test" });
            const refs = el("span", { id: "sc-np-references" }, radio("sc-np-refs", "projects", "Project references (this repository)", true), " ", radio("sc-np-refs", "packages", "NuGet packages"));
            const addToSolution = el("input", { id: "sc-np-add", type: "checkbox", checked: true });
            const solutionFolder = el("input", { id: "sc-np-solution-folder", value: "Scraped" });
            const writes = el("pre", { class: "sc-summary", id: "sc-np-writes" });
            const create = el("button", { type: "button", class: "sc-primary", id: "sc-np-create", disabled: true }, "Create project");
            const picked = group => (document.querySelector(`input[name=${group}]:checked`) || {}).value;
            const params = () => ({
              name: name.value.trim(), folder: folder.value.trim(), platform: picked("sc-np-platform"), startUrl: url.value.trim(),
              testIds: ids.value.split(",").map(t => t.trim()).filter(t => t), references: picked("sc-np-refs"),
            });
            let folderTouched = false;
            folder.addEventListener("input", () => { folderTouched = true; });
            const check = async () => {
              const p = params();
              const problems = await answer("newProjectCheck", p) || {};
              for (const slot of document.querySelectorAll("[data-problem]")) slot.textContent = problems[slot.getAttribute("data-problem")] || "";
              const where = (p.folder || "?").replace(/\\/g, "/").replace(/\/+$/, "");
              writes.textContent = ["Writes:", `${where}/${p.name}.csproj`, `${where}/scraper.config.md`, `${where}/Pages/`].join("\n  ");
              create.disabled = Object.keys(problems).length > 0;
            };
            const propose = async () => {
              if (folderTouched) return;
              const defaults = await answer("projectDefaults", { name: name.value.trim() || "MyApp.UITests" });
              if (!defaults) return;
              folder.value = defaults.folder;
              const r = document.querySelector(`input[name=sc-np-refs][value=${defaults.references}]`);
              if (r) r.checked = true;
            };
            name.addEventListener("input", async () => { await propose(); await check(); });
            for (const input of [folder, url, ids]) input.addEventListener("input", check);
            for (const group of [platform, refs]) group.addEventListener("change", check);
            create.addEventListener("click", async () => {
              const p = params();
              const path = await answer("newProject", p);
              if (!path) return;
              if (addToSolution.checked) host.post({ type: "addProject", path, solutionFolder: solutionFolder.value.trim() || null });
              showProblem(null);
              state.project = p.name;
              state.step = null;
              await loadProjects();
            });
            $("sc-panel").replaceChildren(el("h2", {}, "New scraped project"),
              el("p", { class: "sc-why" }, "A UI test project for an app's page objects: its .csproj, scraper.config.md and Pages folder."),
              el("div", { class: "sc-form" },
                ...field("Name", name, "name"), ...field("Folder", folder, "folder"),
                el("span", {}, "Platform"), el("div", {}, platform, el("div", { class: "sc-refused", "data-problem": "platform" })),
                ...field("Start URL", url, "startUrl"), ...field("Test ids", ids, "testIds"),
                el("span", {}, "Bruinet"), refs,
                el("label", { for: "sc-np-add" }, "Add to solution"), el("span", {}, addToSolution, " in solution folder ", solutionFolder)),
              writes,
              el("div", { class: "sc-actions" }, create, button("Add a config to an existing project instead", showCreateConfig, { class: "sc-link" })));
            await propose();
            await check();
            name.focus();
          }

          async function showCreateConfig() {
            const folders = await call("configCandidates") || [];
            const url = el("input", { placeholder: "https://localhost:5001/", "aria-label": "Start URL" });
            const picker = el("select", { "aria-label": "Project" }, ...folders.map(f => new Option(f, f)));
            const create = async () => {
              if (await answer("createConfig", { folder: picker.value, startUrl: url.value })) { showProblem(null); state.step = null; await loadProjects(); }
            };
            $("sc-panel").replaceChildren(el("h2", {}, "Add a config to an existing project"),
              folders.length
                ? el("div", {},
                    el("p", { class: "sc-why" }, "Write a scraper.config.md into a UI test project: the Scraper records into it and drafts its page objects there."),
                    el("div", { class: "sc-form" }, el("span", {}, "Project"), picker, el("span", {}, "Start URL"), url),
                    el("div", { class: "sc-actions" }, primary("Create config", create)))
                : el("p", { class: "sc-muted" }, "No UI test project here (a *Tests.csproj) to write one into."));
          }

          // ---- 2 Connect ----

          async function stepConnect() {
            const p = state.progress;
            const c = p.lastCheck;
            const firstTime = !p.steps[2] || p.steps[2].state !== "Done";
            const result = text => el("td", { class: c && c.passed ? "sc-ok" : "sc-warn" }, text);
            const rows = [el("tr", {}, el("th", {}, "Start URL"), el("td", {}, el("code", {}, projectInfo().startUrl || ""), " ", button("Change in step 1", () => go(1), { class: "sc-link" })))];
            if (!c) rows.push(el("tr", {}, el("th", {}, "Last check"), el("td", { class: "sc-warn" }, "Not checked yet")));
            else rows.push(el("tr", {}, el("th", {}, "Last check"), result(c.detail)), el("tr", {}, el("th", {}, "Checked"), el("td", { class: "sc-muted" }, new Date(c.at).toLocaleString())));
            return [...heading(2, "Can the Scraper reach the app, signed in, and may it go there?"),
              firstTime ? el("div", { class: "sc-intro", id: "sc-intro" }, el("b", {}, "Three steps to page objects"),
                el("ol", {}, el("li", {}, "Connect to the app"), el("li", {}, "Collect its pages"), el("li", {}, "Draft page objects")),
                el("p", {}, "Each step says what to do next.")) : null,
              el("table", { class: "sc-table" }, ...rows),
              el("div", { class: "sc-actions" },
                primary("Check the app", checkApp, { "data-check": "" }),
                c && c.refused ? button("Allow this host", async () => { await edit("add", "Explore hosts", [new URL(c.url).host]); await checkApp(); }) : null,
                c && c.signIn ? button("Open the browser to sign in", () => record("sign-in")) : null,
                more(
                  button("Open the browser to sign in", () => record("sign-in")),
                  button("Open scraper.config.md", async () => { const cfg = await answer("config", { project: state.project }); if (cfg) host.post({ type: "open", path: cfg.path }); })))];
          }

          async function checkApp() {
            say("Checking the app…");
            const check = await answer("check", { project: state.project });
            say(check ? check.detail : "");
            await refresh(false);
          }

          // ---- 3 Collect pages ----

          async function stepCollect() {
            const c = await answer("config", { project: state.project });
            if (!c) return [];
            const changed = state.progress.steps[2].state === "Attention";
            const firstRun = !(projectInfo().sessions || []).some(s => /-explore(-\d+)?$/.test(s));
            const dry = el("input", { type: "checkbox", id: "sc-dry", checked: firstRun });
            const summary = el("pre", { class: "sc-summary", id: "sc-summary" });
            const risky = el("label", { hidden: true }, el("input", { type: "checkbox", id: "sc-risky" }), " This is a throwaway environment: ClickPolicy all clicks everything, submits too.");
            const start = primary("Explore", () => startExplore(dry.checked), { "data-explore": "" });
            const form = settingsForm(c, async () => {
              const text = await answer("exploreSummary", { project: state.project, dryRun: dry.checked }) || "";
              summary.textContent = text;
              summary.classList.toggle("sc-refused", text.includes("REFUSED"));
              risky.hidden = !text.includes("ClickPolicy all");
              start.disabled = !!state.exploring || text.includes("REFUSED") || (!risky.hidden && !$("sc-risky").checked);
            });
            dry.addEventListener("change", form.refreshSummary);
            const run = state.explored;
            const runs = run ? el("table", { class: "sc-table", id: "sc-runs" },
              el("tr", {}, ...["Last run", "Captures", "Clicks", "Skipped", "Blocked", "Stopped"].map(h => el("th", {}, h))),
              el("tr", {}, el("td", {}, run.dryRun ? "dry run" : "explore"), el("td", {}, String(run.captures.length)), el("td", {}, String(run.clicks)), el("td", {}, String(run.skipped.length)), el("td", {}, String(run.blocked.length)), el("td", {}, run.stopDetail)))
              : el("p", { class: "sc-muted" }, `${plural((projectInfo().sessions || []).length, "recording session")} so far. Explore's report of a run is explore.md in its session.`);
            const session = el("input", { id: "sc-session", placeholder: "recording", style: "width: 10em" });
            const url = el("input", { id: "sc-url", placeholder: projectInfo().startUrl || "the config's start URL", style: "width: 20em" });
            const guards = el("details", { class: "sc-expander", id: "sc-guards", open: state.guardsOpen },
              el("summary", {}, "Guards and budget ", el("span", { class: "sc-muted" }, `up to ${c.maxPages} pages, ${c.maxDepth} deep, ${c.maxMinutes} min, clicks: ${c.clickPolicy}, hosts: ${c.hosts.join(", ") || "none"}`)),
              form.element);
            guards.addEventListener("toggle", () => { state.guardsOpen = guards.open; });
            const result = [...heading(3, "Explore visits the app's pages and states by itself, inside its guards; or record by hand, browsing the app yourself."),
              changed ? el("p", { class: "sc-warn" }, `! ${state.progress.steps[2].summary} since its page object was made. Explore again, or record it by hand.`) : null,
              guards, summary, risky, runs,
              el("div", { id: "sc-collected" }, ...collected()),
              el("div", { class: "sc-actions" }, el("label", {}, dry, " Dry run (links only, nothing clicked)"), start,
                button("Record by hand", () => record(session.value || "recording", url.value || null), { id: "sc-start", title: "Opens the browser: browse the app yourself, each page is captured" }),
                more(
                  el("span", { class: "sc-row" }, "Record by hand: ", el("label", { for: "sc-session" }, "Session"), session, el("label", { for: "sc-url" }, "Start URL"), url),
                  ...LISTS.map(([section, label, rows]) => button(`${label} (${c[rows].length})…`, () => showList(section), { title: LIST_HELP[section] })),
                  button("Open scraper.config.md", () => host.post({ type: "open", path: c.path })),
                  run && run.report ? button("Open explore.md", () => host.post({ type: "open", path: run.report })) : null))];
            setTimeout(form.refreshSummary, 0);
            return result;
          }

          // Explore's guards and budget as a form: a field that differs from the file is marked, Save writes those in one go.
          function settingsForm(c, refreshSummary) {
            const saved = {
              MaxPages: String(c.maxPages), MaxDepth: String(c.maxDepth), MaxMinutes: String(c.maxMinutes), DelayMs: String(c.delayMs),
              Within: c.within || "", LoginUrl: c.loginUrl || "", ClickPolicy: c.clickPolicy, WebsocketApps: c.websocketApps,
              BlockMethods: c.blockMethods.join(", "), danger: Object.assign({}, c.dangerWords), hosts: [...c.hosts],
            };
            const now = JSON.parse(JSON.stringify(saved));
            const status = el("span", { class: "sc-muted", id: "sc-settings-state" });
            const save = button("Save", () => doSave(), { class: "sc-primary", id: "sc-save", disabled: true });
            const undo = button("Undo changes", async () => { await go(3); }, { id: "sc-undo", disabled: true });
            const marks = [];
            const fieldFor = (key, label, type) => {
              const input = el("input", { id: `sc-s-${key}`, type: type || "text", value: saved[key] });
              input.addEventListener("input", () => { now[key] = input.value; update(); });
              marks.push(() => input.classList.toggle("sc-changed", now[key] !== saved[key]));
              return [el("label", { for: input.id }, label), input];
            };
            const choiceFor = (key, label, options) => {
              const select = el("select", { id: `sc-s-${key}` }, ...options.map(o => new Option(o, o, false, o === saved[key])));
              select.addEventListener("change", () => { now[key] = select.value; update(); });
              marks.push(() => select.classList.toggle("sc-changed", now[key] !== saved[key]));
              return [el("label", { for: select.id }, label), select];
            };
            const methods = ["POST", "PUT", "PATCH", "DELETE"].map(m => {
              const box = el("input", { type: "checkbox", "data-method": m, checked: c.blockMethods.includes(m) });
              box.addEventListener("change", () => { now.BlockMethods = [...document.querySelectorAll("[data-method]")].filter(b => b.checked).map(b => b.getAttribute("data-method")).join(", "); update(); });
              return el("label", { class: "sc-chip" }, box, m);
            });
            const words = Object.keys(saved.danger).map(category => {
              const box = el("input", { type: "checkbox", "data-danger": category, checked: saved.danger[category] });
              box.addEventListener("change", () => { now.danger[category] = box.checked; update(); });
              return el("label", { class: "sc-chip", title: "On: its words keep Explore from clicking" }, box, category);
            });
            const hostList = el("span", { class: "sc-wide", id: "sc-hosts" });
            const newHost = el("input", { id: "sc-host-new", placeholder: "add a host", style: "width: 12em" });
            const renderHosts = () => hostList.replaceChildren(...now.hosts.map(h => el("span", { class: "sc-chip" }, el("code", {}, h), button("×", () => { now.hosts = now.hosts.filter(x => x !== h); renderHosts(); update(); }, { class: "sc-link", title: `Remove ${h}` }))),
              newHost, button("Add", () => { const v = newHost.value.trim(); if (v && !now.hosts.includes(v)) { now.hosts.push(v); newHost.value = ""; renderHosts(); update(); } }, { id: "sc-host-add" }));
            renderHosts();
            const changes = () => {
              const list = [];
              for (const key of ["MaxPages", "MaxDepth", "MaxMinutes", "DelayMs", "Within", "LoginUrl", "ClickPolicy", "WebsocketApps", "BlockMethods"])
                if (now[key] !== saved[key]) list.push({ action: "set", section: "Explore", values: [key, now[key]] });
              for (const category of Object.keys(saved.danger))
                if (now.danger[category] !== saved.danger[category]) list.push({ action: "set", section: "Danger words", values: [category, String(now.danger[category])] });
              for (const h of now.hosts) if (!saved.hosts.includes(h)) list.push({ action: "add", section: "Explore hosts", values: [h] });
              for (const h of saved.hosts) if (!now.hosts.includes(h)) list.push({ action: "remove", section: "Explore hosts", values: [h] });
              return list;
            };
            const update = () => {
              const list = changes();
              for (const mark of marks) mark();
              save.disabled = undo.disabled = list.length === 0;
              status.textContent = list.length ? `${plural(list.length, "change")}, not saved yet.` : "Saved in scraper.config.md.";
            };
            async function doSave() {
              const said = await answer("configEdits", { project: state.project, changes: changes() });
              if (said === undefined) return;
              say(said.join(" "));
              await go(3);
            }
            const element = el("fieldset", { id: "sc-settings" }, el("legend", {}, "Guards and budget"),
              el("div", { class: "sc-grid" },
                ...fieldFor("MaxPages", "Max pages", "number"), ...fieldFor("MaxDepth", "Max depth", "number"),
                ...fieldFor("MaxMinutes", "Max minutes", "number"), ...fieldFor("DelayMs", "Pause between clicks (ms)", "number"),
                ...fieldFor("Within", "Within"), ...fieldFor("LoginUrl", "Sign-in page"),
                ...choiceFor("ClickPolicy", "Clicks", ["links", "safe", "not-unsafe", "all"]), ...choiceFor("WebsocketApps", "Websocket apps", ["links", "warn", "ignore"]),
                el("label", {}, "Blocked requests"), el("span", { class: "sc-wide" }, ...methods),
                el("label", {}, "Danger words"), el("span", { class: "sc-wide" }, ...words),
                el("label", { for: "sc-host-new" }, "Explore hosts"), hostList),
              el("div", { class: "sc-actions", style: "margin-top: 0.6em" }, save, undo, status));
            update();
            return { element, refreshSummary, hasChanges: () => changes().length > 0 };
          }

          // What this run collected (Explore, or a recording by hand), newest first.
          function collected() {
            const items = state.collected;
            if (!items.length) return (state.recording || state.exploring) ? [el("p", { class: "sc-muted" }, "Collected pages show here as they are captured.")] : [];
            return [el("h3", {}, `Collected in this run: ${plural(items.length, "capture")} of ${plural(new Set(items.map(i => i.page)).size, "page")}`),
              el("div", { class: "sc-collected" }, ...items.slice().reverse().map((i, n) => el("div", { class: "sc-item", "aria-current": String(n === 0), title: i.location, onclick: () => openCollected(i) },
                el("code", {}, i.page), el("span", { class: "sc-muted" }, ` ${i.title || ""}  ${i.session} #${i.number}`))))];
          }

          function renderCollected() { if ($("sc-collected")) $("sc-collected").replaceChildren(...collected()); }

          async function openCollected(item) {
            await go(4);
            const page = state.pages.find(p => p.key === item.page);
            if (page) await openCapture(page.key, page.captures.find(c => c.session === item.session && c.number === item.number) || page.captures[0]);
          }

          // The config's lists that steer Explore: the section, its label, and its rows in the config.
          const LISTS = [
            ["Never click", "Elements never clicked", "neverClick"],
            ["Always click", "Elements always clicked", "alwaysClick"],
            ["Never visit", "Pages never visited", "neverVisit"],
            ["Allow requests", "Requests allowed", "allowRequests"],
            ["Aliases", "Aliases", "aliases"],
          ];
          const LIST_HELP = {
            "Never click": "Explore never clicks these elements, even when they look safe (an Empty cache button).",
            "Always click": "Explore clicks these elements even when their words look risky (a Filters toggle). Never click wins over always click.",
            "Never visit": "Explore never goes to these paths (an admin area).",
            "Allow requests": "Requests Explore lets through although their method is blocked (a search that POSTs).",
            "Aliases": "Other hosts of the same app (a sign-in page): pages there are not recorded.",
          };

          // A list of the config (Never click, Always click, Never visit, Allow requests, Aliases): add and remove rows.
          async function showList(section) {
            const c = await answer("config", { project: state.project });
            if (!c) return;
            const rows = { "Never click": c.neverClick, "Always click": c.alwaysClick, "Never visit": c.neverVisit, "Allow requests": c.allowRequests, "Aliases": c.aliases }[section];
            const hint = { "Never click": "text: Empty cache, or selector: [data-testid=x]", "Always click": "text: Filters, or selector: ...", "Never visit": "/admin", "Allow requests": "POST /graphql", "Aliases": "https://login.example.com/" }[section];
            const input = el("input", { placeholder: hint, "aria-label": `Add to ${section}`, style: "width: 22em" });
            const add = async () => {
              const value = input.value.trim();
              if (!value) return;
              const pair = /^(\w+)\s+(.+)$/.exec(value);
              await edit("add", section, section === "Allow requests" ? (pair ? [pair[1].toUpperCase(), pair[2]] : ["", value]) : [value]);
              await showList(section);
            };
            $("sc-panel").replaceChildren(el("h2", {}, `3 Collect pages › ${LISTS.find(l => l[0] === section)[1]}`),
              el("p", { class: "sc-why" }, LIST_HELP[section]),
              ...rows.map(row => el("div", { class: "sc-row" }, el("span", { class: "sc-grow" }, row),
                button("Remove", async () => { await edit("remove", section, [section === "Allow requests" ? row.split(" ")[0] : row]); await showList(section); }))),
              el("div", { class: "sc-row" }, input, button("Add", add)),
              el("div", { class: "sc-footer" }, button("‹ Collect pages", () => go(3), { class: "sc-link" }), el("span")));
          }

          async function startExplore(dryRun) {
            const started = await answer("explore", { project: state.project, dryRun });
            if (!started) return;
            state.collected = [];
            state.exploring = "Explore starting…";
            renderCollected();
            showRunning();
          }

          // ---- 4 Review ----

          async function stepReview() {
            state.pages = await answer("pages", { project: state.project }) || [];
            const captures = state.pages.reduce((n, p) => n + p.captures.length, 0);
            const run = state.explored;
            const list = el("div", { id: "sc-pages" });
            for (const page of state.pages) {
              list.append(el("div", { class: "sc-item", title: page.location, onclick: () => openCapture(page.key, page.captures[0]) },
                el("code", {}, page.key), el("span", { class: "sc-muted" }, ` ${page.name}`),
                page.pageObject ? el("span", { class: page.changedSinceGenerated ? "sc-warn" : "sc-muted" }, `  ${page.pageObject}${page.changedSinceGenerated ? ": changed" : ""}`) : null));
              for (const c of page.captures)
                list.append(el("div", { class: "sc-item sc-sub", onclick: () => openCapture(page.key, c) }, `${c.session} #${c.number}, ${c.nodes} nodes, ${new Date(c.at).toLocaleString()}`));
            }
            const skipped = run ? groupBy(run.skipped, k => k.reason) : [];
            return [...heading(4, "What was recorded, and what Explore left alone. Nothing to do here unless something looks wrong."),
              el("div", { class: "sc-facts" },
                el("div", { class: "sc-fact" }, el("b", {}, String(state.pages.length)), el("span", {}, "pages")),
                el("div", { class: "sc-fact" }, el("b", {}, String(captures)), el("span", {}, "captures")),
                run ? el("div", { class: "sc-fact" }, el("b", {}, String(run.skipped.length)), el("span", {}, "skipped in the last run")) : null,
                run ? el("div", { class: "sc-fact" }, el("b", {}, String(run.blocked.length)), el("span", {}, "requests blocked")) : null),
              state.pages.length ? el("div", { class: "sc-split" }, list, el("div", { id: "sc-capture-view" }, el("p", { class: "sc-muted" }, "Choose a page or a capture to see its outline."))) : el("p", { class: "sc-muted" }, "Nothing recorded yet: collect pages first (step 3)."),
              skipped.length ? el("h3", {}, "Skipped by Explore, by reason") : null,
              skipped.length ? el("p", { class: "sc-why" }, "Never click it: Explore leaves it alone from now on. Always click it: Explore clicks it although its words look risky.") : null,
              skipped.length ? el("table", { class: "sc-table", id: "sc-skipped" }, ...skipped.map(([reason, items]) => el("tr", {},
                el("td", {}, reason), el("td", {}, items.slice(0, 4).map(i => i.label).join(", ") + (items.length > 4 ? ` and ${items.length - 4} more` : "")),
                el("td", {}, items.length === 1 ? [button("Never click it", () => edit("add", "Never click", [`text: ${items[0].label}`]), { title: LIST_HELP["Never click"] }), " ", button("Always click it", () => edit("add", "Always click", [`text: ${items[0].label}`]), { title: LIST_HELP["Always click"] })] : null)))) : null,
              run && run.blocked.length ? el("h3", {}, "Blocked requests") : null,
              ...(run ? run.blocked.map(b => el("div", { class: "sc-row" }, el("span", { class: "sc-grow" }, `${b.method} ${b.url} `, el("span", { class: "sc-muted" }, b.reason)),
                button("Allow this request", () => edit("add", "Allow requests", [b.method, new URL(b.url).pathname])))) : [])];
          }

          const groupBy = (items, key) => { const map = new Map(); for (const i of items) { const k = key(i); if (!map.has(k)) map.set(k, []); map.get(k).push(i); } return [...map.entries()]; };

          async function openCapture(key, capture, node) {
            if (state.step !== 4) await go(4);
            state.page = key;
            state.capture = capture;
            state.node = node || null;
            const text = await answer("outline", { project: state.project, page: key, session: capture.session, number: capture.number, maxLines: 2000, focus: node || 0 });
            if (text === undefined) return;
            const page = state.pages.find(p => p.key === key);
            const older = page ? page.captures.filter(c => c !== capture) : [];
            const live = isLive(capture);
            const pre = el("pre", { class: "sc-outline" });
            for (const line of text.split("\n")) {
              if (!line) continue;
              const match = /#(\d+) /.exec(line);
              const number = match ? Number(match[1]) : null;
              pre.append(el("span", { "data-node": number, "aria-current": String(number === state.node), onclick: () => selectNode(number) }, line));
            }
            $("sc-capture-view").replaceChildren(el("h3", {}, key),
              el("div", { class: "sc-actions" },
                button("Open file", () => host.post({ type: "open", path: capture.file })),
                older.length ? button("Compare", () => host.post({ type: "diff", left: older[0].file, right: capture.file, title: `${key}: ${older[0].session} #${older[0].number} ↔ ${capture.session} #${capture.number}` }), { title: "Compare with the capture before it" }) : null,
                page && (page.pageObject || older.length) ? button("Changes", () => showDiff(key), { title: "What changed: since the page object was made, or since the capture before" }) : null,
                el("span", { class: "sc-muted" }, live ? "Live: click a line to show it in the browser." : "")),
              el("div", { id: "sc-node" }), pre);
            if (state.node) await selectNode(state.node, true);
          }

          const isLive = capture => !!(state.recording && state.live && capture && capture.session === state.live.session && capture.number === state.live.number);

          async function selectNode(number, scroll) {
            if (!number) return;
            state.node = number;
            for (const line of document.querySelectorAll("pre.sc-outline span")) line.setAttribute("aria-current", String(Number(line.getAttribute("data-node")) === number));
            const current = document.querySelector(`pre.sc-outline span[data-node="${number}"]`);
            if (scroll && current) current.scrollIntoView({ block: "center" });
            if (isLive(state.capture)) await answer("highlight", { node: number });
            const similar = await answer("similar", { project: state.project, page: state.page, node: number }) || [];
            const suggested = (await answer("suggest", { project: state.project, page: state.page, node: number }) || []).find(s => s.node === number);
            $("sc-node").replaceChildren(el("div", { class: "sc-node" },
              el("div", {}, current ? current.textContent.trim() : `#${number}`),
              el("div", { class: "sc-muted" }, `The same kind of node: ${similar.length} on ${new Set(similar.map(s => s.page)).size} page(s).`),
              suggested ? el("div", {}, el("code", {}, `${suggested.control}<…> ${suggested.name} => new(${suggested.scope || "this"}, ${suggested.locator});`), el("span", { class: "sc-muted" }, `  by ${suggested.why}`)) : null,
              el("div", { class: "sc-actions" },
                button("Make a control", () => chat(`Make a control for the node #${number} of the recorded page ${state.page} of ${state.project} (it occurs ${similar.length} times).`)),
                button("Never click this", () => nodeMatch(number, "Never click"), { title: "Explore never clicks it" }),
                button("Always click this", () => nodeMatch(number, "Always click"), { title: "Explore clicks it whatever its words say" }))));
          }

          async function showDiff(key) {
            const diff = await answer("diff", { project: state.project, page: key });
            if (!diff) return;
            const list = (title, lines) => lines.length ? [el("h4", {}, title), el("pre", { class: "sc-outline" }, lines.join("\n"))] : [];
            $("sc-node").replaceChildren(el("div", { class: "sc-node" },
              el("div", {}, `${diff.addedCount} added, ${diff.removedCount} removed, ${diff.changedCount} changed`),
              ...list("Added", diff.added), ...list("Removed", diff.removed), ...list("Changed", diff.changed.map(c => `${c.before}\n  → ${c.after}`)),
              el("div", { class: "sc-actions" }, button("Update page object", () => chat(`The recorded page ${key} of ${state.project} changed since its page object was made: update the page object (scrape_diff_page).`)))));
          }

          async function nodeMatch(number, section) {
            const match = await answer("elementMatch", { project: state.project, page: state.page, node: number });
            if (match) await edit("add", section, [match]);
          }

          // ---- 5 Controls ----

          // A pattern's own key: its signature, and its variant when one signature is several controls.
          const key = p => `${p.signature}${p.lifted ? " ↑" : ""}${p.variant ? ` (${p.variant})` : ""}`;

          async function stepControls() {
            if (state.reviewing) return reviewControl(state.reviewing);
            const patterns = await answer("patterns", { project: state.project }) || [];
            const checks = await answer("controlChecks", { project: state.project }) || [];
            const toMake = patterns.filter(p => !p.control && !p.hint && (p.pages >= 2 || p.kind === "Repeated" || p.kind === "Collection"));
            // Nothing ticked at first: the person picks what the agent makes.
            for (const p of toMake) if (!(key(p) in state.names)) state.names[key(p)] = p.name;
            const count = () => toMake.filter(p => state.picked.has(key(p))).length;
            const make = primary("", () => makeControls(toMake.filter(p => state.picked.has(key(p)))), { "data-make": "" });
            const label = () => { const n = count(); make.textContent = `Make ${plural(n, "control")} with the agent`; make.disabled = n === 0; };
            label();
            const all = el("input", { type: "checkbox", id: "sc-all-patterns", "aria-label": "All patterns", checked: toMake.length > 0 && count() === toMake.length });
            all.addEventListener("change", () => { for (const p of toMake) all.checked ? state.picked.add(key(p)) : state.picked.delete(key(p)); for (const box of document.querySelectorAll("[data-pattern]")) box.checked = all.checked; label(); });
            const rows = toMake.map(p => {
              const box = el("input", { type: "checkbox", "data-pattern": key(p), checked: state.picked.has(key(p)) });
              box.addEventListener("change", () => { box.checked ? state.picked.add(key(p)) : state.picked.delete(key(p)); label(); });
              const name = el("input", { value: state.names[key(p)], "aria-label": `Control name for ${key(p)}`, style: "width: 12em" });
              name.addEventListener("input", () => { state.names[key(p)] = name.value.trim(); });
              const example = p.examples[0];
              return el("tr", {}, el("td", {}, box), el("td", {}, el("code", {}, p.signature), p.variant ? el("span", { class: "sc-muted" }, ` ${p.variant}`) : null), el("td", {}, `${p.occurrences}× on ${plural(p.pages, "page")}`),
                el("td", { class: "sc-muted" }, example ? example.line.replace(/^#\d+ /, "").slice(0, 60) : ""), el("td", {}, name));
            });
            const made = checks.map(c => {
              const misses = c.parts.filter(p => p.readable && p.found < p.of);
              return el("tr", {},
                el("td", {}, el("b", {}, c.name), el("br"), el("small", { class: "sc-muted" }, c.file)),
                el("td", {}, el("code", {}, c.pattern)),
                el("td", {}, `${c.occurrences}× on ${plural(c.pages, "page")}`),
                el("td", { class: c.passed ? "sc-ok" : c.accepted ? "sc-muted" : "sc-warn" }, c.passed ? "every part found" : c.accepted ? "accepted" : misses.map(m => `${m.name} in ${m.found} of ${m.of}`).join(", ")),
                el("td", {}, button(c.done ? "Review" : "Review and fix", async () => { state.reviewing = c.name; await showStep(); }, { class: c.done ? "" : "sc-primary", "data-review": c.name })));
            });
            return [...heading(5, "Parts that repeat become a control first, so page objects use them instead of a member each. Tick the ones to make; the agent makes them in one go."),
              toMake.length ? el("h3", {}, "To make") : null,
              toMake.length ? el("table", { class: "sc-table", id: "sc-to-make" },
                el("tr", {}, el("th", {}, all), ...["Pattern", "Where", "Looks like", "Control name"].map(h => el("th", {}, h))), ...rows) : null,
              toMake.length ? el("div", { class: "sc-actions" }, make, more(button("Show all patterns", showPatterns))) : el("p", { class: "sc-muted" }, "No pattern asks for a control."),
              checks.length ? el("h3", {}, "Made") : null,
              checks.length ? el("table", { class: "sc-table", id: "sc-made" },
                el("tr", {}, ...["Control", "From", "Occurs", "Its parts in the recordings", ""].map(h => el("th", {}, h))), ...made) : null];
          }

          // What goes with every brief.
          const BRIEF_RULES = [
            "Read only the files under \"Read only\". Do not search the repository: the brief has the facts. Use scrape_get_page only to look at an occurrence again.",
            "Decide what the control is from the facts. The hints are a reading of them, not an order: where you see it differently (another base, one control for two variants, other members), say why, and do that.",
            "First say in a few lines what the control will offer: its base, its parts, its members. Then write it.",
            "Write the template at \"Write\", in \"Namespace\", with \"First line\" first: constructors, and a protected virtual *Core method for each thing a test reads or does (the generator makes the public members from them); hand-written members only where the html-control skill says so. For a component, a control per part. Not only constructors.",
            "Run \"Check\" until it passes. Then report each member, and any part that is not found in every occurrence.",
          ].map((rule, i) => `${i + 1}. ${rule}`).join("\n");

          // The most controls one prompt carries: the agent's whole job stays well inside a small context.
          const BRIEFS_PER_PROMPT = 5;

          async function makeControls(patterns) {
            const batch = patterns.slice(0, BRIEFS_PER_PROMPT);
            const briefs = [];
            for (const p of batch) {
              const brief = await answer("controlBrief", { project: state.project, signature: p.signature, variant: p.variant || null, lifted: !!p.lifted, name: state.names[key(p)] || p.name });
              if (!brief) return;
              briefs.push(brief.text);
            }
            for (const p of batch) state.picked.delete(key(p));
            const rest = patterns.length - batch.length;
            chat(`Make ${plural(batch.length, "control")} for ${state.project}, one per brief below.\n\n${BRIEF_RULES}\n\n${briefs.join("\n\n")}`,
              `When the agent has written ${batch.length === 1 ? "it, it shows" : "them, they show"} under Made.${rest > 0 ? ` ${plural(rest, "more control")} stay ticked: make them when this batch is done.` : ""}`);
            if (state.step === 5) await showStep();
          }

          // A made control again: its brief, with what the check found and the person's note.
          async function fixControl(c, note) {
            const brief = await answer("controlBrief", { project: state.project, signature: c.pattern, variant: c.variant || null, lifted: !!c.lifted, name: c.name });
            const found = c.parts.map(p => `${p.name} ${p.readable ? `${p.found} of ${p.of}` : "not read"}`).join(", ");
            chat(`Fix the control ${c.name} of ${state.project}, in ${c.file} (keep its first line; when it is a plain .cs with members written by hand, it may become a .tpl.cs template). Where its parts are found in the recordings: ${found}. ${note}\n\n${BRIEF_RULES}\n\n${brief ? brief.text : ""}`,
              "When the agent has changed it, Check again shows where its parts are found.");
          }

          async function showPatterns() {
            const patterns = await answer("patterns", { project: state.project }) || [];
            $("sc-panel").replaceChildren(el("h2", {}, "5 Controls › All patterns"),
              el("table", { class: "sc-table" }, el("tr", {}, ...["Kind", "Pattern", "Where", "Control"].map(h => el("th", {}, h))),
                ...patterns.map(p => el("tr", {}, el("td", {}, p.given ? `${p.kind} (config: ${p.given})` : p.kind), el("td", {}, el("code", {}, p.signature), p.variant ? el("span", { class: "sc-muted" }, ` ${p.variant}`) : null, p.hint ? el("div", { class: "sc-muted" }, p.hint) : null), el("td", {}, `${p.occurrences}× on ${plural(p.pages, "page")}`), el("td", {}, p.control || "")))),
              el("div", { class: "sc-footer" }, button("‹ Controls", () => go(5), { class: "sc-link" }), el("span")));
          }

          // A made control: its code, where its parts are found, a note for the agent; accept, fix, check again.
          async function reviewControl(name) {
            const c = (await answer("controlChecks", { project: state.project }) || []).find(x => x.name === name);
            if (!c) { state.reviewing = null; return stepControls(); }
            const code = await answer("code", { project: state.project, path: c.file }) || "";
            const misses = c.parts.filter(p => p.readable && p.found < p.of);
            const note = el("textarea", { class: "sc-note", id: "sc-note", placeholder: "What should change, e.g. the text is sometimes in a div: find it by the row's own text." });
            note.value = misses.map(m => `${m.name} (${m.locator}) is found in ${m.found} of ${m.of} occurrences: find it so it matches all of them.`).join("\n");
            const full = `${projectInfo().folder}/${c.file}`.replace(/\\/g, "/");
            return [el("h2", {}, "5 Controls ", el("span", { class: "sc-muted" }, `› ${c.name}`)),
              el("p", { class: "sc-why" }, `Made from the pattern ${c.pattern}, which occurs ${c.occurrences}× on ${plural(c.pages, "page")}. Check where its parts are found, then accept it, fix it by hand, or tell the agent what to change.`),
              el("div", { class: "sc-two" },
                el("div", {}, el("h3", {}, "Code ", button(`Open ${c.file}`, () => host.post({ type: "open", path: full }), { class: "sc-link" })), el("pre", { class: "sc-code", id: "sc-control-code" }, code)),
                el("div", {}, el("h3", {}, "Its parts in the recordings"),
                  el("table", { class: "sc-table", id: "sc-parts" }, el("tr", {}, ...["Part", "Locator", "Found"].map(h => el("th", {}, h))),
                    ...c.parts.map(p => el("tr", {}, el("td", {}, p.name), el("td", {}, el("code", {}, p.locator)),
                      el("td", { class: !p.readable ? "sc-muted" : p.found === p.of ? "sc-ok" : "sc-warn" },
                        !p.readable ? "not read (made in code)" : `${p.found} of ${p.of}`,
                        p.misses.length ? [" ", button(`show ${p.misses.length === 1 ? "it" : "them"}`, () => showMiss(p.misses[0]), { class: "sc-link" })] : null)))),
                  c.accepted ? el("p", { class: "sc-muted" }, "Accepted as it is.") : null)),
              el("h3", {}, "Tell the agent what to change"), note,
              el("div", { class: "sc-actions" },
                primary("Fix with the agent", () => fixControl(c, note.value), { "data-fix": "" }),
                button(c.accepted ? "Take back the accept" : "Accept", async () => { await answer("acceptControl", { project: state.project, control: c.name, accepted: !c.accepted }); await refresh(false); }, { "data-accept": "" }),
                button("Check again", () => showStep()),
                more(button("Open the file to fix by hand", () => host.post({ type: "open", path: full })))),
              el("div", {}, button("‹ All controls", async () => { state.reviewing = null; await showStep(); }, { class: "sc-link", "data-back": "" }))];
          }

          async function showMiss(miss) {
            await go(4);
            const page = state.pages.find(p => p.key === miss.page);
            if (page) await openCapture(page.key, page.captures[0], miss.node);
          }

          // ---- 6 Page objects ----

          async function stepPages() {
            state.pages = await answer("pages", { project: state.project }) || [];
            const count = () => state.pages.filter(p => state.pickedPages.has(p.key)).length;
            if (state.pickedPages.size === 0) for (const p of state.pages) if (!p.pageObject || p.changedSinceGenerated) state.pickedPages.add(p.key);
            const draftButton = primary("", () => draftPicked(), { "data-draft": "" });
            const improve = button("Improve with the agent", () => improvePicked(), { "data-improve": "" });
            const label = () => { const n = count(); draftButton.textContent = `Draft ${plural(n, "page object")}`; draftButton.disabled = improve.disabled = n === 0; };
            const all = el("input", { type: "checkbox", id: "sc-all-pages", "aria-label": "All pages", checked: state.pages.length > 0 && count() === state.pages.length });
            all.addEventListener("change", () => { for (const p of state.pages) all.checked ? state.pickedPages.add(p.key) : state.pickedPages.delete(p.key); for (const box of document.querySelectorAll("[data-page]")) box.checked = all.checked; label(); });
            const rows = state.pages.map(p => {
              const box = el("input", { type: "checkbox", "data-page": p.key, checked: state.pickedPages.has(p.key) });
              box.addEventListener("change", () => { box.checked ? state.pickedPages.add(p.key) : state.pickedPages.delete(p.key); label(); });
              const name = el("input", { value: state.names[p.key] || p.pageObject || "", placeholder: "the drafted name", "aria-label": `Class name for ${p.key}`, "data-rename": p.key, style: "width: 14em" });
              name.addEventListener("change", () => rename(p, name));
              const status = p.pageObject ? (p.changedSinceGenerated ? el("span", { class: "sc-warn" }, "changed since") : el("span", { class: "sc-ok" }, "drafted")) : el("span", { class: "sc-muted" }, "not drafted");
              return el("tr", {}, el("td", {}, box), el("td", {}, el("code", {}, p.key)), el("td", {}, name), el("td", { class: "sc-muted" }, p.name), el("td", {}, status));
            });
            label();
            return [...heading(6, "A page object per page: tick the pages to draft; give a class a better name before or after. Drafting again keeps the name."),
              state.pages.length ? el("table", { class: "sc-table", id: "sc-page-objects" },
                el("tr", {}, el("th", {}, all), ...["Page", "Class", "Title", "State"].map(h => el("th", {}, h))), ...rows) : el("p", { class: "sc-muted" }, "Nothing recorded yet: collect pages first (step 3)."),
              el("div", { class: "sc-actions" }, draftButton, improve,
                more(button("Draft all pages", async () => { for (const p of state.pages) state.pickedPages.add(p.key); await draftPicked(); }),
                  button("Generate page objects (agent)", () => chat(`Make page objects and controls for the recorded pages of ${state.project}.`)))),
              el("div", { id: "sc-draft-result" })];
          }

          async function rename(page, input) {
            const name = input.value.trim();
            if (!name || name === page.pageObject) return;
            if (!page.pageObject) { state.names[page.key] = name; say(`${page.key} is drafted as ${name}.`); return; }
            const result = await answer("renamePageObject", { project: state.project, page: page.key, name });
            if (!result) { input.value = page.pageObject; return; }
            say(`${result.from} is now ${result.to} (${result.files.join(", ")}).${result.usedElsewhere.length ? ` Still named ${result.from} in ${result.usedElsewhere.join(", ")}: rename there with the IDE.` : ""}`);
            await refresh(false);
          }

          async function draftPicked() {
            const keys = state.pages.filter(p => state.pickedPages.has(p.key)).map(p => p.key);
            if (!keys.length) return;
            say(`Drafting ${plural(keys.length, "page object")} and building…`);
            const result = await answer("draftPages", { project: state.project, pages: keys });
            say("");
            if (!result) return;
            // A name given before the page was drafted.
            for (const p of result.pages) {
              const wanted = state.names[p.key];
              if (wanted && p.outcome === "Written" && wanted !== p.className) await answer("renamePageObject", { project: state.project, page: p.key, name: wanted });
            }
            state.pickedPages.clear();
            await refresh(false);
            const build = result.build;
            $("sc-draft-result").replaceChildren(el("h3", {}, "Drafted"),
              el("p", {}, build ? (build.succeeded ? "The project builds with them." : "The project does not build: see the drafts put back.") : "Nothing new to write."),
              ...result.pages.map(p => el("div", { class: "sc-node" },
                el("div", {}, el("b", {}, `${p.outcome} `), `${state.names[p.key] || p.className} `, el("span", { class: "sc-muted" }, `${p.key}, ${p.members} members, ${p.unstable.length} left out`)),
                p.reason ? el("div", { class: "sc-muted" }, p.reason) : null,
                ...p.removed.map(m => el("div", {}, `Removed ${m.name}, last drafted from ${m.lastSeenIn || "?"}`)),
                ...p.problems.slice(0, 5).map(x => el("div", { class: "sc-refused" }, `line ${x.line}: ${x.code} ${x.message}`)))));
          }

          function improvePicked() {
            const keys = state.pages.filter(p => state.pickedPages.has(p.key));
            chat(`Improve the drafted page objects of ${state.project} for ${keys.map(p => `${p.key} (${p.pageObject || "not drafted yet"})`).join(", ")}: use the project's controls, give members better names, drop members a test would not use; scrape_build until it passes.`);
          }

          // ---- 7 Verify ----

          async function stepVerify() {
            const last = state.progress.lastVerify;
            return [...heading(7, "Opens each page in the running app and finds every member's locator."),
              last ? el("div", { class: "sc-facts" },
                el("div", { class: "sc-fact" }, el("b", { class: last.passed ? "sc-ok" : "sc-refused" }, String(last.found)), el("span", {}, "found once")),
                el("div", { class: "sc-fact" }, el("b", {}, String(last.members - last.found)), el("span", {}, "not found once, or found through their controls")),
                el("div", { class: "sc-fact" }, el("b", {}, String(last.problems)), el("span", {}, "problems"))) : el("p", { class: "sc-muted" }, "Not verified yet."),
              last ? el("p", { class: "sc-why" }, `Last run ${new Date(last.at).toLocaleString()}.`) : null,
              el("div", { class: "sc-actions" }, primary("Verify", verify, { "data-verify": "" })),
              el("div", { id: "sc-verify-result" })];
          }

          async function verify() {
            say("Verifying the page objects in the running app…");
            const result = await answer("verify", { project: state.project });
            say("");
            if (!result) return;
            await refresh(false);
            const passed = result.pages.every(p => !p.problem && p.members.every(m => m.verdict === "Once" || m.verdict === "Unreadable"));
            $("sc-verify-result").replaceChildren(el("h3", {}, passed ? "Every member found once" : "Not every member found once"),
              ...result.pages.map(p => el("div", { class: "sc-node" },
                el("div", {}, `${p.className} `, el("span", { class: "sc-muted" }, p.key)),
                p.problem ? el("div", { class: "sc-refused" }, p.problem) : null,
                ...p.members.filter(m => m.verdict !== "Once").map(m => el("div", { class: m.verdict === "Unreadable" ? "sc-muted" : "sc-refused" },
                  `${m.name}: ${m.locator}, ${m.verdict === "Several" ? `found ${m.count} times` : m.verdict === "NotFound" ? "not found" : "not read (its control makes its locator)"}`)),
                el("div", { class: "sc-muted" }, `${p.members.filter(m => m.verdict === "Once").length} of ${p.members.length} found once`))));
          }

          // ---- Tests ----

          // Each test recording: its steps as the scenario would say them, its gaps, its drafts, and Run once.
          async function stepTests() {
            const tests = await answer("tests", { project: state.project }) || [];
            const name = el("input", { type: "text", id: "sc-test-name", placeholder: "What it shows: Submitting the form thanks the user", "aria-label": "The test's name" });
            return [...heading(8, "Use the app as a person would and assert what it shows: the actions become a test that calls the page objects, a scenario of catalog phrases, or both."),
              el("div", { class: "sc-actions" }, name, primary("Record a test", () => recordTest(name.value.trim() || "test"), { id: "sc-record-test", title: "Opens the browser with Assert and Stop: what you do is recorded" })),
              tests.length ? tests.map(testView) : el("p", { class: "sc-muted" }, "No test recorded yet.")];
          }

          function testView(t) {
            const gaps = t.resolved.steps.filter(s => s.gap);
            const target = el("select", { "aria-label": "Draft as" }, ...[["", "As the config says"], ["xunit", "xUnit test"], ["scenario", "Scenario"], ["both", "Both"]].map(([v, text]) => el("option", { value: v }, text)));
            return el("div", { class: "sc-node", "data-test": t.session },
              el("div", {}, el("b", {}, t.name), " ", el("span", { class: "sc-muted" }, `${t.session}: ${plural(t.actions, "step")}, ${plural(gaps.length, "gap")}`)),
              el("ol", { class: "sc-test-steps" }, ...t.resolved.steps.map(s => el("li", { class: s.gap ? "sc-refused" : "", "data-event": String(s.event) },
                s.phrase || (s.member ? `${s.member}.${s.verb || "?"}` : s.kind),
                s.gap ? el("div", { class: "sc-muted" }, s.gap.message, " ", button("Fix with the agent", () => fixGaps(t, s.event), { class: "sc-link", "data-fix-gap": String(s.event) })) : null))),
              ...t.drafts.map(d => el("div", { class: "sc-item", "data-draft": d.target, title: d.path, onclick: () => host.post({ type: "open", path: d.path }) },
                `${d.target === "Xunit" ? "xUnit test" : "Scenario"}: ${d.path.split(/[\\/]/).pop()}`,
                d.stale ? el("span", { class: "sc-refused" }, " - the recording changed since: draft again") : null)),
              el("div", { class: "sc-actions" }, target,
                button("Draft", () => draftTest(t, target.value), { "data-draft-test": t.session }),
                t.drafts.length ? button("Run once", () => runTest(t), { "data-run-test": t.session }) : null,
                gaps.length > 1 ? button(`Fix ${Math.min(gaps.length, 5)} gaps with the agent`, () => fixGaps(t, null), { "data-fix-gaps": t.session }) : null),
              el("div", { class: "sc-run", id: `sc-run-${t.session}` }));
          }

          // The agent fills a gap in the page objects or controls; the test is drafted again after.
          async function fixGaps(t, event) {
            const briefs = await answer("testBrief", { project: state.project, session: t.session, event });
            if (!briefs) return;
            chat(`Close ${event ? `the gap at event ${event}` : "the gaps"} of the recorded test ${t.name} in ${state.project}, one brief each. Change only page objects and controls, never a drafted test, and never invent a scenario phrase: leave a gap that has none marked. Then scrape_build, scrape_resolve_events, scrape_draft_test and scrape_run_drafts for ${t.session}.\n\n${briefs.join("\n\n---\n\n")}`,
              "When the agent is done, step 8 resolves the test again.");
          }

          async function recordTest(name) {
            const started = await answer("startTest", { project: state.project, session: name, url: null });
            if (!started) return;
            state.collected = [];
            setRecording(started);
            showRunning();
          }

          async function draftTest(t, target) {
            const result = await answer("draftTest", { project: state.project, session: t.session, target: target || null, name: null });
            if (!result) return;
            await refresh(false);
            const box = $(`sc-run-${t.session}`);
            if (box) box.replaceChildren(
              ...result.problems.map(p => el("div", { class: "sc-refused" }, p)),
              ...result.files.map(f => el("div", { class: f.written ? "" : "sc-muted" }, `${f.path}: ${f.written ? "written" : f.note}`)),
              result.gaps.length ? el("div", { class: "sc-muted" }, `${plural(result.gaps.length, "gap")} marked in the draft.`) : null);
          }

          async function runTest(t) {
            say(`Running ${t.name} once: built first…`);
            const results = await answer("runTest", { project: state.project, session: t.session });
            say("");
            const box = $(`sc-run-${t.session}`);
            if (!results || !box) return;
            box.replaceChildren(...results.map(r => el("div", { "data-run": r.passed ? "passed" : "failed" },
              el("b", { class: r.passed ? "sc-ok" : "sc-refused" }, r.passed ? `Passed (${r.passedCount})` : "Did not pass"), " ",
              el("span", { class: "sc-muted" }, r.filter),
              r.problem ? el("div", { class: "sc-refused" }, r.problem) : null,
              r.output.length ? el("pre", { class: "sc-output" }, r.output.join("\n")) : null)));
          }

          // ---- Code ----

          async function showCode() {
            const classes = await answer("controls", { project: state.project }) || [];
            $("sc-panel").replaceChildren(el("h2", {}, "Code ", el("span", { class: "sc-muted" }, "the project's page objects and controls")),
              classes.length ? el("table", { class: "sc-table" }, el("tr", {}, ...["Kind", "Class", "Made from"].map(h => el("th", {}, h))),
                ...classes.map(c => el("tr", { class: "sc-item", title: c.file, onclick: () => host.post({ type: "open", path: c.file }) },
                  el("td", {}, c.kind), el("td", {}, c.name), el("td", { class: "sc-muted" }, c.header ? (c.header.page || c.header.control || "scraped") : "")))) : el("p", { class: "sc-muted" }, "No page objects or controls yet."));
          }

          // ---- Running: a recording or Explore, on every step ----

          // A recording's state only moves forward: the server's notices can arrive out of order (a
          // capture's before the start's), and the start's answer can come after both.
          const stopped = new Set();
          function setRecording(recording) {
            if (recording && stopped.has(recording.session)) return;
            const current = state.recording;
            if (recording && current && recording.session === current.session && recording.captures < current.captures) return;
            state.recording = recording;
          }

          function showRunning() {
            const r = state.recording;
            const running = !!(r || state.exploring);
            $("sc-strip").hidden = !running && !$("sc-status").textContent;
            $("sc-capture").hidden = $("sc-pick").hidden = !r || !!state.exploring;
            $("sc-assert").hidden = !r || !r.actions;
            $("sc-stop").hidden = !running;
            $("sc-here").hidden = !r;
            $("sc-status").setAttribute("data-recording", String(running));
            if (running) $("sc-status").textContent = state.exploring
              || (r.actions ? `Recording a test, ${r.session}: ${plural(r.captures, "event")}. Assert from the browser's toolbar or here.` : `Recording ${r.session}: ${plural(r.captures, "page")}`);
          }

          async function record(session, url) {
            const started = await answer("start", { project: state.project, session, url: url || null });
            if (!started) return;
            state.collected = [];
            setRecording(started);
            showRunning();
            renderCollected();
          }

          async function onChanged(change) {
            if (change.kind === "Exploring" || change.kind === "SignIn" || change.kind === "Explored") {
              if (change.kind === "Exploring") {
                const p = change.progress || {};
                state.exploring = `Exploring: ${p.captures} captured, ${p.clicks} clicks, ${p.skipped} skipped, ${p.blocked} blocked. ${p.doing || ""}`;
                if (change.recording) setRecording(change.recording);
              } else if (change.kind === "SignIn") {
                state.exploring = "Explore waits: sign in in the browser, then Continue.";
                $("sc-continue").hidden = false;
              } else {
                state.exploring = null;
                state.recording = null;
                state.explored = change.explored;
                $("sc-continue").hidden = true;
                $("sc-status").textContent = change.message || "";
                await loadProjectsQuietly();
                await refresh(false);
              }
              showRunning();
              return;
            }
            if (change.kind === "Stopped") { if (change.recording) stopped.add(change.recording.session); state.recording = null; $("sc-status").textContent = ""; await loadProjectsQuietly(); await refresh(false); }
            else setRecording(change.recording);
            if (change.kind === "Failed") showProblem(change.message);
            if (change.kind === "Captured" && change.captured && change.recording) {
              const c = change.captured;
              state.live = { session: change.recording.session, number: c.number };
              state.collected.push({ page: c.page, title: c.title, location: c.location, session: change.recording.session, number: c.number });
              renderCollected();
            }
            showRunning();
          }

          // The sessions a project has (Explore's first run is a dry run): the list again without a redraw.
          async function loadProjectsQuietly() { state.projects = await call("projects") || state.projects; }

          // ---- Shared ----

          // The prompt goes to the IDE with the chosen agent and model; it sends it into Copilot Chat, or puts
          // it on the clipboard. "chatted" says which, and "chatDone" that the answer is complete. The Bruinet
          // scraper agent has the instructions; a plain agent or mode gets them from the prompt file.
          function chat(prompt, after) {
            state.chatAfter = after || null;
            const custom = state.chat.agent === "bruinet-scraper";
            host.post({ type: "chat", prompt: custom ? prompt : `/bruinet-scrape-pages ${prompt}`, agent: state.chat.agent, model: state.chat.model || null });
          }

          function chatDone() {
            const step = state.step;
            if (step === 5 || step === 6 || step === 8) showStep();
            const notice = $("sc-notice");
            if (!notice.hidden) notice.firstChild.textContent = `The agent is done.${step === 5 || step === 6 || step === 8 ? ` Step ${step} is checked again.` : ""}`;
          }

          // The person's agent and model for this project (the config's when they chose none).
          async function loadChatChoice() {
            const choice = await call("chatChoice", { project: state.project });
            const value = choice && "value" in choice ? choice.value : choice;
            state.chat = { agent: (value && value.agent) || "bruinet-scraper", model: (value && value.model) || "" };
            $("sc-agent").value = state.chat.agent;
            showModels({ models: state.models });
          }

          function showModels(message) {
            state.models = message.models || state.models || [];
            const picker = $("sc-model");
            const known = state.models.some(m => m.id === state.chat.model);
            picker.replaceChildren(new Option("The chat's model", ""), ...state.models.map(m => new Option(m.name || m.id, m.id)),
              ...(state.chat.model && !known ? [new Option(`${state.chat.model} (not offered now)`, state.chat.model)] : []));
            picker.value = state.chat.model;
            if (message.agents === false) $("sc-agent").title = "Visual Studio uses the chat window's own mode: set it there. The model is used.";
          }

          async function saveChatChoice() {
            state.chat = { agent: $("sc-agent").value, model: $("sc-model").value };
            await answer("setChatChoice", { project: state.project, agent: state.chat.agent, model: state.chat.model });
          }

          function showChatted(message) {
            const what = message.via === "sent"
              ? `The prompt is sent to Copilot Chat${message.model ? ` (${message.model})` : ""}: follow the agent there.${message.agentIgnored ? " Visual Studio runs it in the chat window's own mode." : ""}`
              : message.via === "chat"
              ? "The prompt is in Copilot Chat, not sent yet: check agent mode is on, then send it."
              : `The prompt is on the clipboard${message.opened ? " and Copilot Chat is open" : ""}: paste it into Copilot Chat in agent mode, then send it.`;
            const notice = $("sc-notice");
            notice.replaceChildren(...[el("span", {}, what, state.chatAfter ? ` ${state.chatAfter}` : ""),
              state.chatAfter ? button("Check again", () => showStep(), { id: "sc-check-again" }) : null,
              button("×", () => { notice.hidden = true; }, { class: "sc-link", title: "Close", "aria-label": "Close" })].filter(x => x));
            notice.hidden = false;
          }

          // One change to the config; the sentence the server gives says what it wrote.
          async function edit(action, section, values) {
            const said = await answer("configEdit", { project: state.project, action, section, values });
            if (said === undefined) return false;
            say(said);
            return true;
          }

          $("sc-new-project").addEventListener("click", () => { state.step = 1; renderStepsIfAny(); showNewProject(); });
          $("sc-agent").addEventListener("change", saveChatChoice);
          $("sc-model").addEventListener("change", saveChatChoice);
          function renderStepsIfAny() { if (state.progress) renderSteps(); }
          $("sc-project").addEventListener("change", async () => { state.project = $("sc-project").value; state.page = state.capture = null; state.reviewing = null; state.picked.clear(); state.pickedPages.clear(); state.names = {}; await loadChatChoice(); await refresh(true); });
          $("sc-capture").addEventListener("click", () => answer("capture", {}));
          $("sc-assert").addEventListener("click", () => answer("assert", {}));
          $("sc-stop").addEventListener("click", () => state.exploring ? call("exploreStop") : answer("stop"));
          $("sc-continue").addEventListener("click", async () => { $("sc-continue").hidden = true; await call("exploreContinue"); });
          $("sc-here").addEventListener("change", async () => {
            const what = $("sc-here").value;
            $("sc-here").value = "";
            const location = await call("location");
            if (!what || !location) return;
            const url = new URL(location);
            if (what === "host") await edit("add", "Explore hosts", [url.hostname]);
            else if (what === "alias") await edit("add", "Aliases", [url.origin + "/"]);
            else if (what === "start") await edit("set", "App", ["StartUrl", location]);
            else await edit("add", "Never visit", [url.pathname]);
          });
          $("sc-pick").addEventListener("click", async () => {
            $("sc-status").textContent = "Click an element in the browser (Escape cancels)…";
            const picked = await answer("pick");
            showRunning();
            if (!picked || !picked.capture || !picked.node) return;
            await go(4);
            const page = state.pages.find(p => p.captures.some(c => c.session === state.recording.session && c.number === picked.capture.number));
            const capture = page && page.captures.find(c => c.session === state.recording.session && c.number === picked.capture.number);
            if (capture) { state.live = { session: capture.session, number: capture.number }; await openCapture(page.key, capture, picked.node); }
          });
          new ResizeObserver(() => document.documentElement.style.setProperty("--sc-top", `${$("sc-top").offsetHeight}px`)).observe($("sc-top"));
          host.post({ type: "ready" });
          host.post({ type: "agents" });
          loadProjects();
        })();
        </script>
        </body>
        </html>
        """;
}
