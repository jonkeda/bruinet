using System.Net;
using System.Text;
using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;
using System.Globalization;

namespace Bruinet.Bdd.LanguageServer;

// The BDD Explorer's three views of a suite as HTML (phase 4): its steps grouped by where they come
// from, with the hooks of each step-definition class; its pages with their controls and phrases;
// and the last run of each scenario. Every entry that has a source opens it (data-open).
internal static class BddExplorerWriter
{
    public const string Steps = "steps";
    public const string Pages = "pages";
    public const string Results = "results";

    public static string Page { get; } = $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta name="color-scheme" content="light dark">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
        <title>BDD Explorer</title>
        <style>
        {{BddPreview.Stylesheet}}
        .uat-tabs { display: flex; gap: 0.3em; align-items: center; padding: 0.4em 0; position: sticky; top: 0; background: var(--uat-bg); border-bottom: 1px solid var(--uat-border); }
        .uat-tabs button { background: none; border: 1px solid transparent; color: var(--uat-fg); padding: 0.2em 0.7em; border-radius: 4px; cursor: pointer; }
        .uat-tabs button[aria-selected="true"] { border-color: var(--uat-accent); color: var(--uat-accent); }
        .uat-tabs select { margin-left: auto; }
        .uat-group { margin: 0.8em 0; }
        .uat-group > h3 { font-size: 0.95em; margin: 0 0 0.3em; }
        .uat-entry { padding: 0.15em 0.4em; border-radius: 3px; }
        .uat-entry[data-open] { cursor: pointer; }
        .uat-entry[data-open]:hover { background: var(--uat-surface); }
        .uat-kind { display: inline-block; min-width: 4.5em; color: var(--uat-muted); }
        .uat-id { color: var(--uat-muted); font-size: 0.85em; margin-left: 0.6em; }
        .uat-count { color: var(--uat-muted); font-weight: 400; }
        .uat-delete { margin-left: 0.6em; padding: 0 6px; border: 1px solid var(--uat-border); border-radius: 3px; background: var(--uat-surface); color: var(--uat-muted); font: inherit; font-size: 0.85em; cursor: pointer; }
        .uat-delete:hover { color: var(--uat-failed); border-color: var(--uat-failed); }
        </style>
        </head>
        <body>
        <nav class="uat-tabs" role="tablist">
        <button type="button" role="tab" data-view="steps" aria-selected="true">Steps</button>
        <button type="button" role="tab" data-view="pages" aria-selected="false">Pages</button>
        <button type="button" role="tab" data-view="results" aria-selected="false">Results</button>
        <select id="uat-suite" aria-label="Suite" hidden></select>
        </nav>
        <main id="uat-root"></main>
        <script>
        (function () {
          "use strict";
          const host = window.chrome && window.chrome.webview
            ? { post: m => window.chrome.webview.postMessage(m), on: f => window.chrome.webview.addEventListener("message", e => f(e.data)) }
            : { post: m => window.parent.postMessage(m, "*"), on: f => window.addEventListener("message", e => f(e.data)) };
          const root = document.getElementById("uat-root");
          const picker = document.getElementById("uat-suite");
          host.on(message => {
            // The IDE's theme, as the preview takes it: CSS variables and a light, dark or high-contrast kind.
            if (message.type === "theme") {
              for (const [name, value] of Object.entries(message.vars || {})) { document.documentElement.style.setProperty(name, value); }
              if (message.kind) { document.documentElement.setAttribute("data-theme", message.kind); }
              return;
            }
            if (message.type !== "update") { return; }
            root.innerHTML = message.html;
            for (const tab of document.querySelectorAll("[data-view]")) {
              tab.setAttribute("aria-selected", String(tab.getAttribute("data-view") === message.view));
            }
            const suites = message.suites || [];
            picker.hidden = suites.length < 2;
            picker.replaceChildren(...suites.map(name => new Option(name, name, false, name === message.suite)));
          });
          document.addEventListener("click", event => {
            const file = event.target.closest("[data-delete-file]");
            if (file) { host.post({ type: "deleteResults", scope: "file", uri: file.getAttribute("data-delete-file") }); return; }
            if (event.target.closest("[data-delete-suite]")) { host.post({ type: "deleteResults", scope: "suite", suite: picker.value || null }); return; }
            const tab = event.target.closest("[data-view]");
            if (tab) { host.post({ type: "view", view: tab.getAttribute("data-view") }); return; }
            const entry = event.target.closest("[data-open]");
            if (entry) { host.post({ type: "open", uri: entry.getAttribute("data-open"), line: Number(entry.getAttribute("data-line")) }); }
          });
          picker.addEventListener("change", () => host.post({ type: "pickSuite", suite: picker.value }));
          host.post({ type: "ready" });
        })();
        </script>
        </body>
        </html>
        """;

    public static string StepsView(BddSuite suite, BddCatalogSet catalogs, IReadOnlyList<BddCallable> callables)
    {
        StringBuilder html = new();
        var patterns = catalogs.Default.Patterns.Where(pattern => pattern.Callable is null).ToList();
        html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-count\">{patterns.Count} steps, {callables.Count} callable steps, {catalogs.Default.Hooks.Count} hooks in {Encode(suite.Name)}</p>\n");

        // C# steps by their class; page phrases by page; control verbs and built-ins by themselves.
        foreach (var group in patterns
            .GroupBy(pattern => pattern.ControlVerb is not null ? "Control verbs"
                : pattern.Page is { } page ? $"Page {page}"
                : pattern.CommandId.StartsWith("Builtin.", StringComparison.Ordinal) || pattern.CommandId.StartsWith("Spec.", StringComparison.Ordinal) ? "Built in"
                : TypeName(pattern.CommandId))
            .OrderBy(group => group.Key.StartsWith("Built in", StringComparison.Ordinal) ? 2 : group.Key.StartsWith("Control", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"uat-group\"><h3>{Encode(group.Key)} <span class=\"uat-count\">{group.Count()}</span></h3>\n");
            foreach (var pattern in group.OrderBy(pattern => pattern.Keyword).ThenBy(pattern => pattern.Phrase, StringComparer.OrdinalIgnoreCase))
            {
                var at = pattern.ControlVerb is null && pattern.Page is null ? Method(suite, pattern.CommandId) : null;
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(at)}><span class=\"uat-kind\">{pattern.Keyword}</span>{Encode(pattern.Phrase)}{Parameters(pattern)}<span class=\"uat-id\">{Encode(pattern.CommandId)}</span></div>\n");
            }

            html.Append("</section>\n");
        }

        foreach (var file in callables.GroupBy(callable => callable.Source.FilePath ?? string.Empty).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"uat-group\"><h3>{Encode(Path.GetFileName(file.Key))} <span class=\"uat-count\">{file.Count()}</span></h3>\n");
            foreach (var callable in file)
            {
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(callable.Source)}><span class=\"uat-kind\">{(callable.Kind == BddCallableKind.EachRow ? "Each row" : "Step")}</span>{Encode(callable.Phrase)}</div>\n");
            }

            html.Append("</section>\n");
        }

        // The hooks, with the step-definition class that declares each.
        foreach (var group in catalogs.Default.Hooks.GroupBy(hook => TypeName(hook.Name)).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"uat-group\"><h3>Hooks of {Encode(group.Key)}</h3>\n");
            foreach (var hook in group)
            {
                var tags = hook.Tags.Count == 0 ? string.Empty : $" <span class=\"uat-id\">{Encode(string.Join(' ', hook.Tags.Select(tag => "@" + tag)))}</span>";
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(Method(suite, hook.Name))}><span class=\"uat-kind\">{hook.Kind}</span>{Encode(hook.Name[(hook.Name.LastIndexOf('.') + 1)..])}{tags}</div>\n");
            }

            html.Append("</section>\n");
        }

        return html.ToString();
    }

    public static string PagesView(BddSuite suite, BddCatalogSet catalogs)
    {
        StringBuilder html = new();
        var pages = UatPageSet.Of(catalogs.Default).Values.OrderBy(page => page.Key, StringComparer.OrdinalIgnoreCase).ToList();
        html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-count\">{pages.Count} pages in {Encode(suite.Name)}</p>\n");
        foreach (var page in pages)
        {
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"uat-group\"><h3 class=\"uat-entry\"{Open(BddSourceLocator.Of(page.PageType))}>{Encode(page.Key)} <span class=\"uat-id\">{Encode(page.PageType.Name)}</span></h3>\n");
            foreach (var control in page.Controls.OrderBy(control => control.Name, StringComparer.OrdinalIgnoreCase))
            {
                var navigates = control.NavigatesTo is { } to ? $" <span class=\"uat-id\">→ {Encode(to)}</span>" : string.Empty;
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(BddSourceLocator.Of(page.PageType, control.Name))}><span class=\"uat-kind\">control</span>{Encode(control.Name)}<span class=\"uat-id\">{Encode(control.ControlType.Name)}</span>{navigates}</div>\n");
            }

            foreach (var phrase in catalogs.Default.Patterns.Where(pattern => pattern.Page == page.Key || pattern.Page == page.Name))
            {
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(Method(suite, phrase.CommandId))}><span class=\"uat-kind\">{phrase.Keyword}</span>{Encode(phrase.Phrase)}</div>\n");
            }

            html.Append("</section>\n");
        }

        return html.ToString();
    }

    public static string ResultsView(BddSuite suite, IReadOnlyList<(string File, BddScenarioRun Run, int Line)> runs)
    {
        StringBuilder html = new();
        var failed = runs.Count(run => run.Run.Status == "failed");
        var deleteAll = runs.Count == 0 ? string.Empty : " <button type=\"button\" class=\"uat-delete\" data-delete-suite title=\"Delete every scenario result of this suite, in every run\">Delete all results</button>";
        html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-count\">{runs.Count} scenarios with a run in {Encode(suite.Name)}, {failed} failed{deleteAll}</p>\n");
        foreach (var file in runs.GroupBy(run => run.File).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var uri = new Uri(file.Key).AbsoluteUri;
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"uat-group\"><h3>{Encode(Path.GetFileName(file.Key))} <button type=\"button\" class=\"uat-delete\" data-delete-file=\"{Encode(uri)}\" title=\"Delete this file's results, in every run\">Delete</button></h3>\n");
            foreach (var (path, run, line) in file.OrderBy(run => run.Line))
            {
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-entry\"{Open(new BddSourceLocation(path, line))}><span class=\"uat-result\" data-status=\"{Encode(run.Status)}\" title=\"{Encode(run.Status)}\"></span> {Encode(run.Scenario)}<span class=\"uat-id\">{run.DurationMs} ms · {run.At.LocalDateTime:g}</span></div>\n");
            }

            html.Append("</section>\n");
        }

        return html.ToString();
    }

    private static BddSourceLocation? Method(BddSuite suite, string commandId)
    {
        var dot = commandId.LastIndexOf('.');
        if (dot <= 0 || suite.FindType(commandId[..dot]) is not { } type)
        {
            return null;
        }

        return BddSourceLocator.Of(type, commandId[(dot + 1)..]);
    }

    private static string Parameters(BddCommandPattern pattern)
        => pattern.Parameters.Count == 0 ? string.Empty : $"<span class=\"uat-id\">{Encode(string.Join(", ", pattern.Parameters.Select(parameter => parameter.ToString())))}</span>";

    private static string TypeName(string dottedMember)
        => dottedMember.LastIndexOf('.') is > 0 and var dot ? dottedMember[..dot] : dottedMember;

    private static string Open(BddSourceLocation? at)
        => at is { FilePath: { } file } ? $" data-open=\"{Encode(new Uri(file).AbsoluteUri)}\" data-line=\"{at.LineNumber}\"" : string.Empty;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
