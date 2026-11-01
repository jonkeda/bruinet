using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Bruinet.Narrator;

/// <summary>A parsed video script and what is wrong with its form.</summary>
/// <param name="Script">The script; null when it has no <c># Video:</c> title.</param>
/// <param name="Diagnostics">Problems with its form, at their lines.</param>
public sealed record BddVideoScriptParse(BddVideoScript? Script, IReadOnlyList<BddDiagnostic> Diagnostics);

/// <summary>
/// Reads a video script: <c># Video: title</c>, an optional <c>Field | Value</c> table (Voice, Instructions, Beat, Pause, Say, Touches, TouchColor),
/// then <c>## Scene: name</c> sections, each with a <c>Scenario:</c> line, an intro paragraph, a
/// <c>Step | Say</c> table and an outro paragraph.
/// </summary>
public static class BddVideoScriptParser
{
    /// <summary>The video has no <c># Video:</c> title.</summary>
    public const string NoTitleCode = "VID001";

    /// <summary>A field the script does not know.</summary>
    public const string UnknownFieldCode = "VID002";

    /// <summary>A field's value cannot be read.</summary>
    public const string BadValueCode = "VID003";

    /// <summary>A scene has no <c>Scenario:</c> line.</summary>
    public const string NoScenarioCode = "VID004";

    /// <summary>A scene's table is not <c>Step | Say</c>.</summary>
    public const string BadTableCode = "VID005";

    private const string TitlePrefix = "# Video:";
    private const string ScenePrefix = "## Scene:";
    private const string ScenarioPrefix = "Scenario:";

    /// <summary>Parses <paramref name="text"/>, the content of <paramref name="filePath"/>.</summary>
    /// <param name="text">The script's text, which may be unsaved.</param>
    /// <param name="filePath">The script's path, for the diagnostics.</param>
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    public static BddVideoScriptParse Parse(string text, string filePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var diagnostics = new List<BddDiagnostic>();
        void Report(BddDiagnosticSeverity severity, string code, string message, int line)
            => diagnostics.Add(new BddDiagnostic(severity, code, message, new BddSourceLocation(filePath, line)));

        string? title = null;
        string? voice = null;
        string? instructions = null;
        var instructionsLine = 1;
        TimeSpan? beat = null;
        BddTouchMode? touches = null;
        string? touchColor = null;
        TimeSpan? pause = null;
        BddSayMode? say = null;
        var scenes = new List<BddVideoScene>();
        SceneBuilder? scene = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index].Trim();
            if (line.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase) && title is null)
            {
                title = line[TitlePrefix.Length..].Trim();
                continue;
            }

            if (line.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase))
            {
                Finish();
                scene = new SceneBuilder(line[ScenePrefix.Length..].Trim(), number);
                continue;
            }

            if (line.StartsWith('#'))
            {
                // Any other heading ends the scene: notes about the video are not narrated.
                Finish();
                continue;
            }

            if (line.StartsWith('|'))
            {
                var cells = Cells(line);
                if (cells.All(cell => cell.Length > 0 && cell.All(character => character is '-' or ':')))
                {
                    continue;
                }

                if (scene is null)
                {
                    ReadField(cells, number);
                }
                else
                {
                    scene.Row(cells, number, Report);
                }

                continue;
            }

            if (scene is null || line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(ScenarioPrefix, StringComparison.OrdinalIgnoreCase) && scene.Scenario is null)
            {
                scene.Scenario = line[ScenarioPrefix.Length..].Trim();
                scene.ScenarioLine = number;
                continue;
            }

            scene.Text(line);
        }

        Finish();
        if (title is null)
        {
            Report(BddDiagnosticSeverity.Error, NoTitleCode, "A video script starts with '# Video: <title>'.", 1);
            return new BddVideoScriptParse(null, diagnostics);
        }

        return new BddVideoScriptParse(new BddVideoScript(filePath, title, voice, beat, scenes) { Instructions = instructions, InstructionsLine = instructionsLine, Touches = touches, TouchColor = touchColor, Pause = pause, Say = say }, diagnostics);

        void Finish()
        {
            if (scene is null)
            {
                return;
            }

            if (scene.Scenario is null)
            {
                Report(BddDiagnosticSeverity.Error, NoScenarioCode, $"The scene '{scene.Name}' has no 'Scenario: <address>' line, so nothing runs in it.", scene.Line);
            }

            scenes.Add(scene.Build());
            scene = null;
        }

        void ReadField(IReadOnlyList<string> cells, int number)
        {
            if (cells.Count < 2 || cells[0].Equals("Field", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            switch (cells[0].ToLowerInvariant())
            {
                case "voice":
                    voice = cells[1].Length > 0 ? cells[1] : null;
                    break;
                case "instructions":
                    instructions = cells[1].Length > 0 ? cells[1] : null;
                    instructionsLine = number;
                    break;
                case "beat" when int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds):
                    beat = TimeSpan.FromMilliseconds(milliseconds);
                    break;
                case "beat":
                    Report(BddDiagnosticSeverity.Error, BadValueCode, $"Beat is the milliseconds to wait after a step, a whole number: not '{cells[1]}'.", number);
                    break;
                case "touches" when BddTouchMarks.ParseMode(cells[1]) is { } mode:
                    touches = mode;
                    break;
                case "touches":
                    Report(BddDiagnosticSeverity.Error, BadValueCode, $"Touches is on, off or outline-only: not '{cells[1]}'.", number);
                    break;
                case "touchcolor" when BddTouchMarks.IsColor(cells[1]):
                    touchColor = cells[1].Trim().ToUpperInvariant();
                    break;
                case "touchcolor":
                    Report(BddDiagnosticSeverity.Error, BadValueCode, $"TouchColor is a colour as #RRGGBB, e.g. {BddTouchMarks.DefaultColor}: not '{cells[1]}'.", number);
                    break;
                case "pause" when int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds <= BddScenePacing.MaxPauseMs:
                    pause = TimeSpan.FromMilliseconds(milliseconds);
                    break;
                case "pause":
                    Report(BddDiagnosticSeverity.Error, BadValueCode, $"Pause is the least milliseconds an action takes in the video, a whole number up to {BddScenePacing.MaxPauseMs}: not '{cells[1]}'.", number);
                    break;
                case "say" when Enum.TryParse<BddSayMode>(cells[1], ignoreCase: true, out var mode) && cells[1].All(char.IsLetter):
                    say = mode;
                    break;
                case "say":
                    Report(BddDiagnosticSeverity.Error, BadValueCode, $"Say is while (a step's line plays while it runs) or before (the step waits for its line): not '{cells[1]}'.", number);
                    break;
                default:
                    Report(BddDiagnosticSeverity.Warning, UnknownFieldCode, $"'{cells[0]}' is not a video field: Voice, Instructions, Beat, Pause, Say, Touches and TouchColor are.", number);
                    break;
            }
        }
    }

    private static List<string> Cells(string row)
    {
        var inner = row.Trim();
        inner = inner.StartsWith('|') ? inner[1..] : inner;
        inner = inner.EndsWith('|') ? inner[..^1] : inner;
        return [.. inner.Split('|').Select(cell => cell.Trim())];
    }

    private sealed class SceneBuilder(string name, int line)
    {
        private readonly List<BddVideoLine> _lines = [];
        private readonly List<string> _intro = [];
        private readonly List<string> _outro = [];
        private bool _tableSeen;
        private bool _header;

        public string Name { get; } = name;

        public int Line { get; } = line;

        public string? Scenario { get; set; }

        public int ScenarioLine { get; set; }

        public void Row(IReadOnlyList<string> cells, int number, Action<BddDiagnosticSeverity, string, string, int> report)
        {
            _tableSeen = true;
            if (!_header)
            {
                _header = true;
                if (cells.Count < 2 || !cells[0].Equals("Step", StringComparison.OrdinalIgnoreCase) || !cells[1].Equals("Say", StringComparison.OrdinalIgnoreCase))
                {
                    report(BddDiagnosticSeverity.Error, BadTableCode, "A scene's table has the columns 'Step | Say'.", number);
                }

                return;
            }

            if (cells.Count >= 2 && cells[0].Length > 0 && cells[1].Length > 0)
            {
                _lines.Add(new BddVideoLine(cells[0], cells[1], number));
            }
        }

        public void Text(string text) => (_tableSeen ? _outro : _intro).Add(text);

        public BddVideoScene Build() => new(Name, Scenario, Join(_intro), Join(_outro), _lines, Line, Scenario is null ? Line : ScenarioLine);

        private static string? Join(List<string> parts) => parts.Count == 0 ? null : string.Join(' ', parts);
    }
}
