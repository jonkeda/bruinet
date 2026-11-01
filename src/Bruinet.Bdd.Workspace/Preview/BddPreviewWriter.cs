using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using System.Globalization;

namespace Bruinet.Bdd.Preview;

// One render: the engine's parse for the diagnostics, Markdig's for the blocks. Gherkin fences,
// feature, scenario and step headings, and tag lines are drawn here; everything else is Markdig's.
internal sealed partial class BddPreviewWriter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UsePreciseSourceLocation()
        .DisableHtml()
        .Build();

    private readonly string _text;
    private readonly string _filePath;
    private readonly BddPreviewOptions _options;
    private readonly string[] _lines;
    private readonly bool _isSteps;
    private readonly List<BddDiagnostic> _diagnostics = [];
    private readonly HashSet<BddDiagnostic> _shown = [];
    private readonly StringBuilder _html = new();
    private readonly BddPreviewBinding _binding = new();
    private string? _nextTableClass;
    private BddScenarioRun? _run;

    public BddPreviewWriter(string text, string filePath, BddPreviewOptions options)
    {
        _text = text;
        _filePath = filePath;
        _options = options;
        _lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        _isSteps = BddFileAnalysis.IsStepFile(filePath);
    }

    public BddPreviewResult Write()
    {
        Parse();
        var name = Path.GetFileName(_filePath);
        _html.Append(CultureInfo.InvariantCulture, $"<article class=\"uat-doc\" data-kind=\"{(_isSteps ? "steps" : "feature")}\" data-file=\"{Encode(name)}\">\n");
        if (_options.ParseOnlyReason is { } reason)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-banner\" data-mode=\"parse-only\">Parsed only, not bound: {Encode(reason)}</p>\n");
        }

        var document = Markdown.Parse(_text, Pipeline);
        var blocks = document.ToList();
        WriteDiagnostics(line => blocks.Count == 0 || line < blocks[0].Line + 1);
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var first = block.Line + 1;
            var last = i + 1 < blocks.Count ? blocks[i + 1].Line : _lines.Length;
            WriteBlock(block);
            WriteDiagnostics(line => line >= first && line <= last);
        }

        WriteDiagnostics(_ => true);
        _html.Append("</article>\n");
        return new BddPreviewResult(_html.ToString(), _diagnostics);
    }

    private void Parse()
    {
        var analysis = BddFileAnalysis.Analyze(_text, _filePath, _options.Catalogs);
        _diagnostics.AddRange(analysis.Diagnostics);
        if (_options.Catalogs is { } catalogs)
        {
            _binding.Add(analysis.Scenarios, catalogs);
        }

        // Only this file's lines are placed; another file's problem (a step file it sees) goes at the top.
        _diagnostics.Sort((a, b) => Line(a).CompareTo(Line(b)));
        int Line(BddDiagnostic diagnostic) => IsHere(diagnostic) ? diagnostic.Location.LineNumber : 0;
    }

    private bool IsHere(BddDiagnostic diagnostic)
        => diagnostic.Location.FilePath is not { } file || string.Equals(Path.GetFullPath(file), Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase);

    private void WriteBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                WriteHeading(heading);
                break;
            case FencedCodeBlock fence when string.Equals(fence.Info, "gherkin", StringComparison.OrdinalIgnoreCase):
                WriteSteps(fence);
                break;
            case ParagraphBlock paragraph when TagsOf(paragraph) is { } tags:
                _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-tags\" data-line=\"{paragraph.Line + 1}\">");
                _html.AppendJoin(' ', tags.Select(tag => $"<span class=\"uat-tag\">{Encode(tag)}</span>"));
                _html.Append("</p>\n");
                break;
            case Table table:
                if (_nextTableClass is { } tableClass)
                {
                    table.GetAttributes().AddClass(tableClass);
                }

                _nextTableClass = null;
                WriteMarkdig(table);
                break;
            default:
                WriteMarkdig(block);
                break;
        }
    }

    private void WriteHeading(HeadingBlock heading)
    {
        var line = heading.Line + 1;
        var raw = _lines[heading.Line].Trim();
        var text = (BddFileFormat.NewHeading(raw) ?? raw).TrimStart('#').Trim();
        _nextTableClass = null;
        if (heading.Level <= 2)
        {
            _binding.Leave();
            _run = null;
        }

        switch (heading.Level)
        {
            case 1 when TrySplit(text, "Feature:", out var title) || TrySplit(text, "Steps:", out title):
                var keyword = text[..text.IndexOf(':', StringComparison.Ordinal)];
                _html.Append(CultureInfo.InvariantCulture, $"<h1 class=\"uat-title\" data-line=\"{line}\"><span class=\"uat-kw\">{keyword}</span> {Encode(title)}{(_isSteps ? string.Empty : RunButtons("data-run-file", "Run file", "Run every scenario of the file"))}</h1>\n");
                _html.Append(BddPreviewRecord.List(_options.Recordings));
                return;
            case 2 when TrySplit(text, "Scenario Outline:", out var outline):
                _binding.Enter(line);
                _run = _options.Runs.GetValueOrDefault($"{outline} [1]");
                _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-scenario\" data-line=\"{line}\" data-scenario=\"{Encode(outline)}\"><span class=\"uat-kw\">Scenario Outline</span> {Encode(outline)}{RunBadge(_run, line)}{RunButtons($"data-run=\"{line}\"", "Run", "Run every row")}</h2>\n");
                WriteNotOnHead(line);
                return;
            case 2 when TrySplit(text, "Scenario:", out var scenario):
                _binding.Enter(line);
                _run = _options.Runs.GetValueOrDefault(scenario);
                _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-scenario\" data-line=\"{line}\" data-scenario=\"{Encode(scenario)}\"><span class=\"uat-kw\">Scenario</span> {Encode(scenario)}{RunBadge(_run, line)}{RunButtons($"data-run=\"{line}\"", "Run", "Run this scenario")}</h2>\n");
                WriteNotOnHead(line);
                return;
            case 2 when text == "Background":
                _binding.EnterBackground();
                _run = _options.Runs.Values.OrderBy(run => run.Scenario, StringComparer.Ordinal).FirstOrDefault();
                _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-background\" data-line=\"{line}\"><span class=\"uat-kw\">Background</span></h2>\n");
                return;
            case 2 when TrySplit(text, "Step for each row:", out var eachRow):
                _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-callable\" data-line=\"{line}\" data-kind=\"each-row\"><span class=\"uat-kw\">Step for each row</span> {Mark(eachRow)}</h2>\n");
                _nextTableClass = "uat-parameters";
                return;
            case 2 when TrySplit(text, "Step:", out var step):
                _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-callable\" data-line=\"{line}\"><span class=\"uat-kw\">Step</span> {Mark(step)}</h2>\n");
                _nextTableClass = "uat-parameters";
                return;
            case 2 when text == "Metadata":
                _nextTableClass = "uat-metadata";
                break;
            case 3 when text == "Examples" || text.StartsWith("Examples:", StringComparison.Ordinal):
                _nextTableClass = "uat-examples";
                break;
        }

        WriteMarkdig(heading);
    }

    // A fence of steps: one row per step, the keyword in a gutter, a step's table or doc string
    // under it. Lines that are not steps are shown as they are; the parser's diagnostic says why.
    private void WriteSteps(FencedCodeBlock fence)
    {
        _html.Append(CultureInfo.InvariantCulture, $"<ol class=\"uat-steps\" data-line=\"{fence.Line + 1}\">\n");
        var lines = fence.Lines.Lines.Take(fence.Lines.Count).Select(line => (Number: line.Line + 1, Text: line.Slice.ToString())).ToList();
        var open = false;
        BddPreviewCall? call = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var (number, text) = lines[i];
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith('|'))
            {
                var rows = new List<(int Number, string Text)>();
                for (; i < lines.Count && lines[i].Text.Trim().StartsWith('|'); i++)
                {
                    rows.Add((lines[i].Number, lines[i].Text.Trim()));
                }

                i--;
                WriteStepTable(rows);
                continue;
            }

            if (trimmed.StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                var body = new List<string>();
                for (i++; i < lines.Count && !lines[i].Text.Trim().StartsWith("\"\"\"", StringComparison.Ordinal); i++)
                {
                    body.Add(lines[i].Text);
                }

                _html.Append(CultureInfo.InvariantCulture, $"<pre class=\"uat-docstring\" data-line=\"{number}\">{Encode(string.Join('\n', Dedent(body)))}</pre>\n");
                continue;
            }

            if (trimmed is "<table>" or "<doc string>")
            {
                _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-passes\" data-line=\"{number}\">passes the call's {(trimmed == "<table>" ? "table" : "doc string")}</p>\n");
                continue;
            }

            if (open)
            {
                CloseStep(call);
            }

            open = true;
            var step = StepRegex().Match(trimmed);
            var bound = _binding.At(number);
            var attributes = bound is null ? string.Empty : $" data-binds=\"{Encode(bound.Describe())}\" title=\"{Encode(bound.Describe())}\"";
            _html.Append(step.Success
                ? $"<li class=\"uat-step{(bound is null ? string.Empty : " uat-bound")}\" data-line=\"{number}\"{attributes}><span class=\"uat-kw\">{step.Groups["keyword"].Value}</span><span class=\"uat-text\">{StepStatus(number)}{Mark(step.Groups["text"].Value)}{PageLabel(bound)}</span>\n"
                : $"<li class=\"uat-step uat-not-a-step\" data-line=\"{number}\"><span class=\"uat-kw\"></span><span class=\"uat-text\">{Encode(trimmed)}</span>\n");
            WriteDiagnostics(line => line == number);
            WriteFailure(number);
            call = bound?.Call;
        }

        if (open)
        {
            CloseStep(call);
        }

        _html.Append("</ol>\n");
        WriteOutlineRows(fence);
    }

    // The run's status of a written step: a callable call's is its steps' worst; "not run" after a failure.
    private string StepStatus(int line)
    {
        if (_run is null)
        {
            return string.Empty;
        }

        var steps = _run.Steps.Where(step => step.WrittenLine == line).ToList();
        var status = steps.Count == 0 ? "NotRun"
            : steps.FirstOrDefault(step => step.Status != "Passed")?.Status ?? "Passed";
        var (icon, label) = status switch
        {
            "Passed" => ("✓", "Passed"),
            "Failed" => ("✗", "Failed"),
            "Skipped" => ("–", "Skipped"),
            "Canceled" => ("⊘", "Canceled"),
            _ => ("○", "Not run"),
        };
        return $"<span class=\"uat-status\" data-status=\"{status.ToLowerInvariant()}\" role=\"img\" aria-label=\"{label}\" title=\"{label}\">{icon}</span> ";
    }

    // Under a failed step: its message, and the screenshot taken then, which opens full size.
    private void WriteFailure(int line)
    {
        if (_run?.Steps.FirstOrDefault(step => step.WrittenLine == line && step.Status == "Failed") is not { } failed)
        {
            return;
        }

        _html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-failure\" data-line=\"{line}\">{Encode(failed.Message ?? "The step failed.")}");
        if (_run.EvidencePath is { } evidence && File.Exists(evidence) && _options.ImageUrl(evidence) is { } url)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<a class=\"uat-shot\" href=\"#\" data-open=\"{Encode(evidence)}\" title=\"Open the screenshot\"><img src=\"{Encode(url)}\" alt=\"The screen when the step failed\"></a>");
        }

        _html.Append("</div>\n");
    }

    // A button the page turns into a run message; disabled, with the reason, when the suite cannot run by address.
    private string RunButtons(string attribute, string label, string title)
    {
        if (_options.Run is not { } run)
        {
            return string.Empty;
        }

        var reason = Encode(run.Reason ?? "This scenario cannot be run from here.");
        var runState = run.CanRun ? $"title=\"{Encode(title)} in the suite's runner, from the source: no build when only scenario files changed\"" : $"disabled title=\"{reason}\"";
        var testState = run.CanRun ? $"title=\"{Encode(title)} as a test (dotnet test), as CI runs it\"" : $"disabled title=\"{reason}\"";
        var debugState = run.CanRun ? $"title=\"{Encode(title.Replace("Run", "Debug", StringComparison.Ordinal))}, stopping at step breakpoints\"" : $"disabled title=\"{reason}\"";
        // A scenario records on its own too (the narrator), silently, with its beats.
        var scenario = attribute.StartsWith("data-run=", StringComparison.Ordinal);
        var record = scenario
            ? BddPreviewRecord.Button(_options.Record, attribute.Replace("data-run=", "data-record=", StringComparison.Ordinal), "Record", "Record this scenario on video")
            : string.Empty;

        // Step through: one scenario, in the suite's runner, paused before each step.
        var stepState = run.CanRun ? "title=\"Step through this scenario, paused before each step\"" : $"disabled title=\"{reason}\"";
        var step = scenario
            ? $" <button type=\"button\" class=\"uat-run\" {attribute.Replace("data-run=", "data-step=", StringComparison.Ordinal)} {stepState}>\u23ED Step</button>"
            : string.Empty;
        // Run (the runner), Run as test (dotnet test, the gate), Debug, Step: AD-022.
        return $" <span class=\"uat-actions\"><button type=\"button\" class=\"uat-run\" {attribute} {runState}>\u25B6 {label}</button>"
            + $" <button type=\"button\" class=\"uat-run\" data-test {attribute} {testState}>\u25B7 {label} as test</button>"
            + $" <button type=\"button\" class=\"uat-run\" data-debug {attribute} {debugState}>\u25CF {label.Replace("Run", "Debug", StringComparison.Ordinal)}</button>{step}{record}</span>";
    }

    // The last run's result; in a host, on a heading, with a button that deletes the scenario's results
    // in every run (an outline's, every row's: a row has none of its own).
    private string RunBadge(BddScenarioRun? run, int? line = null)
    {
        if (run is null)
        {
            return string.Empty;
        }

        var seconds = (run.DurationMs / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var delete = _options.Run is null || line is null
            ? string.Empty
            : $"<button type=\"button\" class=\"uat-run uat-delete\" data-delete=\"{line}\" title=\"Delete this scenario's results, in every run\">✕</button>";
        return $" <span class=\"uat-result\" data-status=\"{Encode(run.Status)}\" title=\"Last run {run.At.LocalDateTime:g}\">{Encode(run.Status)} · {seconds} s</span>{delete}";
    }

    // After the step's own table or doc string: the call it makes, unfolded.
    private void CloseStep(BddPreviewCall? call)
    {
        if (call is not null)
        {
            WriteCall(call);
        }

        _html.Append("</li>\n");
    }

    private static string PageLabel(BddPreviewStep? step)
        => step?.PageShown is { } page ? $" <span class=\"uat-page\" title=\"The page this step runs on\">{Encode(page)}</span>" : string.Empty;

    // A call to a callable, unfolded: its body's steps with the values filled in, bound where the call is.
    private void WriteCall(BddPreviewCall call)
    {
        _html.Append(CultureInfo.InvariantCulture, $"<details class=\"uat-call\"><summary>{Encode(call.Heading)} ({call.Steps.Count} step{(call.Steps.Count == 1 ? string.Empty : "s")})</summary>\n<ol class=\"uat-call-steps\">\n");
        foreach (var step in call.Steps)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<li class=\"uat-step uat-bound\" data-source=\"{Encode(step.Source)}\" data-depth=\"{step.Depth}\" title=\"{Encode(step.Binds)}\"><span class=\"uat-kw\">{step.Keyword}</span><span class=\"uat-text\">{(step.Row is { } row ? $"<span class=\"uat-row\">row {row}</span> " : string.Empty)}{Mark(step.Text)}</span></li>\n");
        }

        _html.Append("</ol>\n</details>\n");
    }

    // An outline's first row is drawn bound in the steps above; every row can be opened with its values.
    private void WriteOutlineRows(FencedCodeBlock fence)
    {
        var rows = _binding.RowsOf(fence.Line + 1);
        if (rows.Count < 2)
        {
            return;
        }

        foreach (var row in rows)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<details class=\"uat-outline-row\" data-row=\"{row.Index}\"><summary>{Encode(row.Name)}{RunBadge(_options.Runs.GetValueOrDefault(row.Name))}</summary>\n<ol class=\"uat-call-steps\">\n");
            foreach (var step in row.Steps)
            {
                _html.Append(CultureInfo.InvariantCulture, $"<li class=\"uat-step uat-bound\" data-source=\"{Encode(step.Source)}\" title=\"{Encode(step.Binds)}\"><span class=\"uat-kw\">{step.Keyword}</span><span class=\"uat-text\">{Mark(step.Text)}</span></li>\n");
            }

            _html.Append("</ol>\n</details>\n");
        }
    }

    private void WriteNotOnHead(int line)
    {
        if (_binding.WhyNotOnHead(line) is { } reason)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-not-on-head\" data-line=\"{line}\">Not run on this suite: {Encode(reason)}</p>\n");
        }
    }

    private void WriteStepTable(List<(int Number, string Text)> rows)
    {
        var cells = rows.Where(row => !SeparatorRegex().IsMatch(row.Text)).Select(row => (row.Number, Cells: Cells(row.Text))).ToList();
        if (cells.Count == 0)
        {
            return;
        }

        _html.Append(CultureInfo.InvariantCulture, $"<table class=\"uat-table\" data-line=\"{cells[0].Number}\">\n<thead><tr>");
        _html.AppendJoin(string.Empty, cells[0].Cells.Select(cell => $"<th>{Mark(cell)}</th>"));
        _html.Append("</tr></thead>\n<tbody>\n");
        foreach (var (number, row) in cells.Skip(1))
        {
            _html.Append(CultureInfo.InvariantCulture, $"<tr data-line=\"{number}\">");
            _html.AppendJoin(string.Empty, row.Select(cell => $"<td>{Mark(cell)}</td>"));
            _html.Append("</tr>\n");
        }

        _html.Append("</tbody>\n</table>\n");
        foreach (var (number, _) in cells)
        {
            WriteDiagnostics(line => line == number);
        }
    }

    private void WriteMarkdig(Block block)
    {
        block.GetAttributes().AddProperty("data-line", (block.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(block);
        writer.Flush();
        _html.Append(writer.ToString());
    }

    private void WriteDiagnostics(Func<int, bool> atLine)
    {
        foreach (var diagnostic in _diagnostics.Where(d => !_shown.Contains(d) && atLine(IsHere(d) ? d.Location.LineNumber : 0)).ToList())
        {
            _shown.Add(diagnostic);
            var severity = diagnostic.Severity == BddDiagnosticSeverity.Error ? "error" : "warning";
            var line = IsHere(diagnostic) ? $" data-line=\"{diagnostic.Location.LineNumber}\"" : string.Empty;
            var elsewhere = IsHere(diagnostic) ? string.Empty : $" <span class=\"uat-diag-where\">{Encode(Where(diagnostic.Location))}</span>";
            var within = diagnostic.Within.Count == 0
                ? string.Empty
                : $" <span class=\"uat-diag-where\">in {Encode(string.Join(" → ", diagnostic.Within.Select(Where)))}</span>";
            _html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-diag\" data-severity=\"{severity}\" data-code=\"{Encode(diagnostic.Code)}\"{line}>");
            _html.Append(CultureInfo.InvariantCulture, $"<span class=\"uat-diag-code\">{Encode(diagnostic.Code)}</span> {Encode(diagnostic.Message)}{within}{elsewhere}</div>\n");
        }
    }

    private IReadOnlyList<string>? TagsOf(ParagraphBlock paragraph)
    {
        var raw = _text.Substring(paragraph.Span.Start, paragraph.Span.Length);
        var tags = raw.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return tags.Length > 0 && tags.All(tag => tag.StartsWith('@') && tag.Length > 1) ? tags : null;
    }

    private static bool TrySplit(string text, string prefix, out string rest)
    {
        rest = text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..].Trim() : string.Empty;
        return text.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Cells(string row)
    {
        var trimmed = row.Trim();
        trimmed = trimmed.StartsWith('|') ? trimmed[1..] : trimmed;
        trimmed = trimmed.EndsWith('|') ? trimmed[..^1] : trimmed;
        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static IEnumerable<string> Dedent(List<string> lines)
    {
        var indent = lines.Where(line => line.Trim().Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        return lines.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart());
    }

    // Quoted values, {x} arguments and <x> placeholders, marked; everything encoded.
    internal static string Mark(string text)
    {
        var builder = new StringBuilder();
        var at = 0;
        foreach (Match match in TokenRegex().Matches(text))
        {
            builder.Append(Encode(text[at..match.Index]));
            var kind = match.Groups["quoted"].Success ? "uat-value" : match.Groups["argument"].Success ? "uat-argument" : "uat-placeholder";
            builder.Append(CultureInfo.InvariantCulture, $"<span class=\"{kind}\">{Encode(match.Value)}</span>");
            at = match.Index + match.Length;
        }

        builder.Append(Encode(text[at..]));
        return builder.ToString();
    }

    internal static string Encode(string text) => WebUtility.HtmlEncode(text);

    internal static string Where(BddSourceLocation location)
        => location.FilePath is { } file ? $"{Path.GetFileName(file)}:{location.LineNumber}" : $"line {location.LineNumber}";

    [GeneratedRegex(@"^(?<keyword>Given|When|Then|And|But)\s+(?<text>.*)$")]
    private static partial Regex StepRegex();

    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?$")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"(?<quoted>""(?:[^""\\]|\\.)*"")|(?<argument>\{[^{}\s]+\})|(?<placeholder><[^<>\s][^<>]*>)")]
    private static partial Regex TokenRegex();
}
