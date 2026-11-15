namespace Bruinet.Narrator;

/// <summary>A scene as it will run: its scenario, found, and what is said at which of its steps.</summary>
/// <param name="Scene">The scene, as written.</param>
/// <param name="FeatureFile">The scenario's feature file, full path.</param>
/// <param name="Scenario">The scenario, as parsed.</param>
/// <param name="Steps">The steps it runs, as written: the Background's, then its own.</param>
/// <param name="Says">What is said before a step, by the step's line in the feature file.</param>
public sealed record BddVideoScenePlan(BddVideoScene Scene, string FeatureFile, BddScenario Scenario, IReadOnlyList<BddStep> Steps, IReadOnlyDictionary<int, BddVideoLine> Says)
{
    /// <summary>How the suite's names are said (its <c>pronunciations.md</c>); null when it has none. Applied to each line before it is spoken.</summary>
    public Pronunciations? Pronunciations { get; init; }

    /// <summary>The scenario's address.</summary>
    public string Address => Scene.Scenario!;
}

/// <summary>
/// A video script checked like a feature file (AD-022's one checker): its form, and each scene
/// against the feature files. The editor, the preview and the gate show these diagnostics.
/// </summary>
/// <param name="Script">The script; null when it could not be read.</param>
/// <param name="Diagnostics">What is wrong, at its lines in the script.</param>
/// <param name="Scenes">The scenes whose scenario was found, in order.</param>
/// <param name="ConfigDirectory">The suite's folder, which addresses are relative to; null when none was found.</param>
public sealed record BddVideoScriptCheck(
    BddVideoScript? Script,
    IReadOnlyList<BddDiagnostic> Diagnostics,
    IReadOnlyList<BddVideoScenePlan> Scenes,
    string? ConfigDirectory)
{
    /// <summary>No suite config above the script, so no address can be found.</summary>
    public const string NoConfigCode = "VID010";

    /// <summary>A scene's address names no feature file.</summary>
    public const string NoFileCode = "VID011";

    /// <summary>A scene's address names no scenario of its file.</summary>
    public const string NoScenarioCode = "VID012";

    /// <summary>A Say row names no step of the scene's scenario.</summary>
    public const string NoStepCode = "VID013";

    /// <summary>A Say row is out of step order.</summary>
    public const string OrderCode = "VID014";

    /// <summary>A line's markup is not well-formed.</summary>
    public const string MarkupCode = "VID020";

    /// <summary>A tag-like word in a line is no SSML element, so it is not spoken.</summary>
    public const string UnknownTagCode = "VID021";

    /// <summary>An element's attribute has a value the voices do not know.</summary>
    public const string BadAttributeCode = "VID022";

    /// <summary>The script's voice does not follow an element a line uses; it is dropped, its words kept.</summary>
    public const string UnsupportedElementCode = "VID023";

    /// <summary>An element is where it may not go: inside <c>say-as</c>, <c>sub</c> or <c>phoneme</c>.</summary>
    public const string MisplacedElementCode = "VID024";

    /// <summary><c>pronunciations.md</c> lists a term twice, or has a row with nothing to say; reported in that file.</summary>
    public const string PronunciationsCode = Narrator.Pronunciations.ProblemCode;

    /// <summary>The script's style (<c>Instructions</c>) is not one the Azure Speech voice lists.</summary>
    public const string UnknownStyleCode = "VID030";

    /// <summary>Whether nothing is wrong enough to stop a recording.</summary>
    public bool Success => Script is not null && Diagnostics.All(diagnostic => diagnostic.Severity != BddDiagnosticSeverity.Error);

    /// <summary>Reads and checks a video script.</summary>
    /// <param name="text">The script's text, which may be unsaved.</param>
    /// <param name="filePath">The script's path.</param>
    /// <param name="configDirectory">The suite's folder; null to look for it above the script.</param>
    /// <param name="read">Reads an environment variable for Azure Speech's settings; the process's by default.</param>
    /// <param name="home">The user's folder, where those settings are looked for last; <c>%USERPROFILE%</c> by default.</param>
    /// <param name="speechCache">Where Azure Speech's voice list is kept; the speech cache by default.</param>
    public static BddVideoScriptCheck Check(string text, string filePath, string? configDirectory = null, Func<string, string?>? read = null, string? home = null, string? speechCache = null)
    {
        var parse = BddVideoScriptParser.Parse(text, filePath);
        var diagnostics = new List<BddDiagnostic>(parse.Diagnostics);
        var scenes = new List<BddVideoScenePlan>();
        configDirectory ??= FindConfigDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)));
        if (parse.Script is not { } script)
        {
            return new BddVideoScriptCheck(null, diagnostics, scenes, configDirectory);
        }

        void Report(BddDiagnosticSeverity severity, string code, string message, int line)
            => diagnostics.Add(new BddDiagnostic(severity, code, message, new BddSourceLocation(filePath, line)));

        foreach (var (spoken, line, scene) in script.Scenes.SelectMany(scene => Spoken(scene)))
        {
            Markup(spoken, line, script.Voice, Report);
        }

        if (UnknownStyle(script, configDirectory, read, home, speechCache) is { } unknown)
        {
            Report(BddDiagnosticSeverity.Warning, UnknownStyleCode, unknown, script.InstructionsLine);
        }

        if (configDirectory is null && script.Scenes.Any(scene => scene.Scenario is not null))
        {
            Report(BddDiagnosticSeverity.Error, NoConfigCode,
                $"No {BddFileFormat.ConfigFileName} above the script, so its scenario addresses cannot be found.", 1);
            return new BddVideoScriptCheck(script, diagnostics, scenes, null);
        }

        var pronunciations = Pronunciations.Load(configDirectory);
        foreach (var (problemLine, message) in pronunciations?.Problems ?? [])
        {
            diagnostics.Add(new BddDiagnostic(BddDiagnosticSeverity.Error, PronunciationsCode, message, new BddSourceLocation(pronunciations!.Path, problemLine)));
        }

        var files = new Dictionary<string, BddParseResult?>(StringComparer.OrdinalIgnoreCase);
        foreach (var scene in script.Scenes.Where(scene => scene.Scenario is not null))
        {
            if (Find(scene, configDirectory!, files, Report) is { } found)
            {
                // The binder runs the Background's steps first, so a scene can narrate them too.
                var steps = found.Background.Concat(found.Scenario.Steps).ToList();
                scenes.Add(new BddVideoScenePlan(scene, found.File, found.Scenario, steps, Says(scene, found.Scenario.Name, steps, Report)) { Pronunciations = pronunciations });
            }
        }

        diagnostics.Sort((a, b) => a.Location.LineNumber.CompareTo(b.Location.LineNumber));
        return new BddVideoScriptCheck(script, diagnostics, scenes, configDirectory);
    }

    // What a scene says: its intro and outro (at the scene's heading) and each row, with the line to report at.
    private static IEnumerable<(string Text, int Line, BddVideoScene Scene)> Spoken(BddVideoScene scene)
    {
        if (scene.Intro is { } intro)
        {
            yield return (intro, scene.Line, scene);
        }

        foreach (var say in scene.Lines)
        {
            yield return (say.Say, say.Line, scene);
        }

        if (scene.Outro is { } outro)
        {
            yield return (outro, scene.Line, scene);
        }
    }

    // A line's markup: well-formed, its attributes known, and what the script's voice follows (null voice: the settings decide at record time).
    private static void Markup(string text, int line, string? voice, Action<BddDiagnosticSeverity, string, string, int> report)
    {
        var spoken = SpokenLine.Parse(text);
        foreach (var problem in spoken.Problems)
        {
            report(problem.Severity == SpokenProblemSeverity.Error ? BddDiagnosticSeverity.Error : BddDiagnosticSeverity.Warning, problem.Code,
                $"{problem.Message} (character {problem.Position + 1} of the line)", line);
        }

        if (SsmlSupport.Of(voice) is not { } supported)
        {
            return;
        }

        foreach (var element in spoken.ElementsUsed.Where(element => !supported.Contains(element)))
        {
            report(BddDiagnosticSeverity.Warning, UnsupportedElementCode,
                supported.Count == 0
                    ? $"The voice '{voice}' cannot follow markup, so <{element}> is dropped and the words are kept (a <sub> says its alias)."
                    : $"The voice '{voice}' does not follow <{element}>: it is dropped and the words are kept."
                        + (element == "break" ? " A MAI voice pauses at the end of a <p> or an <s>." : string.Empty),
                line);
        }
    }

    // An Azure Speech voice takes a style, not free text: once its voice list is kept, a style the voice lacks is said so.
    private static string? UnknownStyle(BddVideoScript script, string? configDirectory, Func<string, string?>? read, string? home, string? speechCache)
    {
        if (!AzureSpeech.Owns(script.Voice) || string.IsNullOrWhiteSpace(script.Instructions))
        {
            return null;
        }

        try
        {
            var voice = AzureSpeech.VoiceOf(script.Voice!);
            var style = script.Instructions.Trim();
            if (SpeechSecrets.Find(configDirectory, read, home) is not { } secrets
                || AzureSpeechVoices.Kept(secrets, speechCache) is not { } kept
                || AzureSpeechVoices.Find(kept, voice) is not { } found
                || found.Styles.Contains(style, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            return found.Styles.Count == 0
                ? $"'{voice}' has no styles, so the instructions '{style}' are ignored: Azure Speech takes a style name (narrator, educational), not free text."
                : $"'{voice}' has no style '{style}'. Its styles: {string.Join(", ", found.Styles)}.";
        }
        catch (InvalidDataException)
        {
            // Broken settings are reported where the voice is used; the check stays about the script.
            return null;
        }
    }

    /// <summary>The folder with the suite's config at or above <paramref name="directory"/>; null when none.</summary>
    /// <param name="directory">Where to start looking.</param>
    public static string? FindConfigDirectory(string? directory)
    {
        for (var folder = directory is null ? null : new DirectoryInfo(directory); folder is not null; folder = folder.Parent)
        {
            if (BddFileFormat.HasConfig(folder.FullName))
            {
                return folder.FullName;
            }
        }

        return null;
    }

    /// <summary>A step as the scenario writes it: its keyword and text.</summary>
    /// <param name="step">A scenario's step.</param>
    public static string Written(BddStep step) => $"{step.Keyword} {step.Text}";

    private static (string File, BddScenario Scenario, IReadOnlyList<BddStep> Background)? Find(
        BddVideoScene scene,
        string configDirectory,
        Dictionary<string, BddParseResult?> files,
        Action<BddDiagnosticSeverity, string, string, int> report)
    {
        var address = scene.Scenario!;
        var hash = address.IndexOf('#', StringComparison.Ordinal);
        var relative = hash < 0 ? address : address[..hash];
        var path = Path.GetFullPath(Path.Combine(configDirectory, relative));
        if (!files.TryGetValue(path, out var parse))
        {
            parse = File.Exists(path) ? BddMarkdownParser.ParseFile(path) : null;
            files[path] = parse;
        }

        if (parse?.Document is not { } document)
        {
            report(BddDiagnosticSeverity.Error, NoFileCode, $"No feature file '{relative}' in {configDirectory}.", scene.ScenarioLine);
            return null;
        }

        var scenario = document.Scenarios.FirstOrDefault(candidate => Matches(relative, candidate.Name, address))
            ?? document.Scenarios.FirstOrDefault(candidate => candidate.OutlineName is { } outline && Matches(relative, outline, address));
        if (scenario is null)
        {
            var names = document.Scenarios.Select(candidate => BddScenarioAddress.Slug(candidate.OutlineName ?? candidate.Name)).Distinct().Take(6);
            report(BddDiagnosticSeverity.Error, NoScenarioCode,
                $"'{relative}' has no scenario '{(hash < 0 ? string.Empty : address[(hash + 1)..])}'. Its scenarios: {string.Join(", ", names)}.", scene.ScenarioLine);
            return null;
        }

        return (path, scenario, document.Background);
    }

    private static bool Matches(string relative, string name, string address)
        => string.Equals(BddScenarioAddress.Of(relative, name), address.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    // Rows go in step order: each matches the first step with its text after the previous row's.
    private static Dictionary<int, BddVideoLine> Says(BddVideoScene scene, string scenario, List<BddStep> steps, Action<BddDiagnosticSeverity, string, string, int> report)
    {
        var says = new Dictionary<int, BddVideoLine>();
        var after = -1;
        foreach (var line in scene.Lines)
        {
            var wanted = Normalize(line.Step);
            var index = IndexOf(after + 1);
            if (index < 0 && IndexOf(0) is var earlier and >= 0)
            {
                report(BddDiagnosticSeverity.Warning, OrderCode, $"'{line.Step}' comes before the previous row's step: rows go in step order.", line.Line);
                index = earlier;
            }

            if (index < 0)
            {
                report(BddDiagnosticSeverity.Error, NoStepCode,
                    $"'{line.Step}' is not a step of {scenario}. Write the step as the scenario does, keyword and text.", line.Line);
                continue;
            }

            says[steps[index].Source.LineNumber] = line;
            after = Math.Max(after, index);

            int IndexOf(int from)
            {
                for (var candidate = from; candidate < steps.Count; candidate++)
                {
                    if (string.Equals(Normalize(Written(steps[candidate])), wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                return -1;
            }
        }

        return says;
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
}
