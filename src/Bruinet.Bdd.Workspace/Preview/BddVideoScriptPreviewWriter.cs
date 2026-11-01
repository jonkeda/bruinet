using System.Net;
using System.Text;
using Bruinet.Narrator;
using System.Globalization;

namespace Bruinet.Bdd.Preview;

// A video script as it will play: each scene's scenario with every step, what is said before it,
// and which steps run silently; the check's diagnostics at their lines.
internal sealed class BddVideoScriptPreviewWriter(string text, string filePath, BddPreviewOptions options)
{
    private readonly StringBuilder _html = new();
    private readonly HashSet<BddDiagnostic> _shown = [];
    private BddVideoScriptCheck _check = null!;

    public BddPreviewResult Write()
    {
        _check = BddVideoScriptCheck.Check(text, filePath);
        _html.Append(CultureInfo.InvariantCulture, $"<article class=\"uat-doc\" data-kind=\"video\" data-file=\"{Encode(Path.GetFileName(filePath))}\">\n");
        if (_check.Script is not { } script)
        {
            Diagnostics(_ => true, all: true);
            _html.Append("</article>\n");
            return new BddPreviewResult(_html.ToString(), _check.Diagnostics);
        }

        _html.Append(CultureInfo.InvariantCulture, $"<h1 class=\"uat-title\" data-line=\"1\"><span class=\"uat-kw\">Video</span> {Encode(script.Title)}{BddPreviewRecord.Button(options.Record, "data-record-file", "Record", "Record the video: run its scenes, with the voice")}</h1>\n");
        var fields = new List<(string Field, string Value)>();
        if (script.Voice is { } voice)
        {
            fields.Add(("Voice", voice));
        }

        if (script.Beat is { } beat)
        {
            fields.Add(("Beat", $"{beat.TotalMilliseconds:0} ms"));
        }

        if (fields.Count > 0)
        {
            _html.Append("<p class=\"uat-video-fields\" data-line=\"1\">");
            _html.AppendJoin(" · ", fields.Select(field => $"<span class=\"uat-kw\">{field.Field}</span> {Encode(field.Value)}"));
            _html.Append("</p>\n");
        }

        _html.Append(BddPreviewRecord.List(options.Recordings));
        var firstScene = script.Scenes.Count == 0 ? int.MaxValue : script.Scenes[0].Line;
        Diagnostics(line => line < firstScene);
        for (var index = 0; index < script.Scenes.Count; index++)
        {
            var scene = script.Scenes[index];
            var next = index + 1 < script.Scenes.Count ? script.Scenes[index + 1].Line : int.MaxValue;
            WriteScene(scene, index + 1, _check.Scenes.FirstOrDefault(plan => plan.Scene == scene));
            Diagnostics(line => line >= scene.Line && line < next);
        }

        Diagnostics(_ => true, all: true);
        _html.Append("</article>\n");
        return new BddPreviewResult(_html.ToString(), _check.Diagnostics);
    }

    private void WriteScene(BddVideoScene scene, int number, BddVideoScenePlan? plan)
    {
        _html.Append(CultureInfo.InvariantCulture, $"<h2 class=\"uat-scenario\" data-line=\"{scene.Line}\"><span class=\"uat-kw\">Scene {number}</span> {Encode(scene.Name)}</h2>\n");
        if (scene.Scenario is { } address)
        {
            var name = plan is null ? address : $"{plan.Scenario.Name} ({Path.GetFileName(plan.FeatureFile)})";
            _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-video-scenario\" data-line=\"{scene.ScenarioLine}\" title=\"{Encode(address)}\"><span class=\"uat-kw\">Scenario</span> {Encode(name)}</p>\n");
        }

        if (scene.Intro is { } intro)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-video-say\" data-line=\"{scene.ScenarioLine}\">{Said(intro)}</p>\n");
        }

        if (plan is not null)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<ol class=\"uat-steps\" data-line=\"{scene.ScenarioLine}\">\n");
            foreach (var step in plan.Steps)
            {
                var say = plan.Says.GetValueOrDefault(step.Source.LineNumber);
                var line = say?.Line ?? scene.ScenarioLine;
                var spoken = say is null
                    ? "<span class=\"uat-video-silent\">silent</span>"
                    : $"<span class=\"uat-video-say\">{Said(say.Say)}</span>";
                _html.Append(CultureInfo.InvariantCulture, $"<li class=\"uat-step\" data-line=\"{line}\"><span class=\"uat-kw\">{step.Keyword}</span><span class=\"uat-text\">{Encode(step.Text)}{spoken}</span></li>\n");
            }

            _html.Append("</ol>\n");
        }

        if (scene.Outro is { } outro)
        {
            _html.Append(CultureInfo.InvariantCulture, $"<p class=\"uat-video-say\" data-line=\"{scene.Line}\">{Said(outro)}</p>\n");
        }
    }

    private void Diagnostics(Func<int, bool> atLine, bool all = false)
    {
        foreach (var diagnostic in _check.Diagnostics.Where(d => !_shown.Contains(d) && (all || (IsOwn(d) && atLine(d.Location.LineNumber)))).ToList())
        {
            _shown.Add(diagnostic);
            var severity = diagnostic.Severity == BddDiagnosticSeverity.Error ? "error" : "warning";
            _html.Append(CultureInfo.InvariantCulture, $"<div class=\"uat-diag\" data-severity=\"{severity}\" data-code=\"{Encode(diagnostic.Code)}\" data-line=\"{diagnostic.Location.LineNumber}\">");
            _html.Append(CultureInfo.InvariantCulture, $"<span class=\"uat-diag-code\">{Encode(diagnostic.Code)}</span> {Encode(diagnostic.Message)}</div>\n");
        }
    }

    // What is said, as the captions show it (no tags), with a mark when the line has SSML.
    private static string Said(string text)
    {
        var line = Bruinet.Narrator.SpokenLine.Parse(text);
        return line.HasMarkup
            ? $"{Encode(line.Plain)} <span class=\"uat-video-markup\" title=\"SSML: {Encode(string.Join(", ", line.ElementsUsed))}\">SSML</span>"
            : Encode(line.Plain);
    }

    // A diagnostic of another file (pronunciations.md) has a line of its own: it is shown at the end, not at the script's line of that number.
    private bool IsOwn(BddDiagnostic diagnostic)
        => diagnostic.Location.FilePath is null || _check.Script is null || string.Equals(diagnostic.Location.FilePath, _check.Script.FilePath, StringComparison.OrdinalIgnoreCase);

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
