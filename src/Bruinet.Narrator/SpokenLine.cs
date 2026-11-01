using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Narrator;

/// <summary>How serious a problem in a line's markup is.</summary>
public enum SpokenProblemSeverity
{
    /// <summary>The line is read as far as it can be; the author should look.</summary>
    Warning,

    /// <summary>The markup cannot be rendered.</summary>
    Error,
}

/// <summary>A problem in a spoken line's markup.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Code">The script check's code (<c>VID020</c> to <c>VID024</c>).</param>
/// <param name="Message">What is wrong, and what to do.</param>
/// <param name="Position">The offset in the line where it is, from 0.</param>
public sealed record SpokenProblem(SpokenProblemSeverity Severity, string Code, string Message, int Position);

/// <summary>A node of a spoken line: text, or an SSML element with its attributes and content.</summary>
/// <param name="Name">The element's name (<c>break</c>, <c>mstts:express-as</c>); null for text.</param>
/// <param name="Text">The text; null for an element.</param>
/// <param name="Attributes">The element's attributes, in the order written.</param>
/// <param name="Children">The element's content.</param>
public sealed record SpokenNode(string? Name, string? Text, IReadOnlyList<KeyValuePair<string, string>> Attributes, IReadOnlyList<SpokenNode> Children)
{
    /// <summary>The attribute's value; null when it is not there.</summary>
    /// <param name="name">The attribute's name.</param>
    public string? Attribute(string name) => Attributes.FirstOrDefault(pair => pair.Key == name).Value;

    /// <summary>Where the element starts in the line, from 0.</summary>
    public int Position { get; init; }
}

/// <summary>
/// What a video script says at a step: plain text, or text with SSML elements inline (<c>and save.&lt;break time="400ms"/&gt;</c>).
/// Parsed once, rendered three ways: <see cref="Plain"/> for captions, the timeline and the preview;
/// <see cref="ToSsml"/> for a voice that speaks markup; <see cref="Reduced"/> for one that does not. A line is markup
/// when it holds a tag whose name is a known SSML element (<see cref="Elements"/>): a <c>&lt;</c> that starts no
/// such tag is plain text (<c>x &lt; 5</c>), and a tag-like word that is no element is a warning and is not spoken.
/// </summary>
public sealed class SpokenLine
{
    /// <summary>The elements a script may use.</summary>
    public static readonly IReadOnlySet<string> Elements = new HashSet<string>(StringComparer.Ordinal)
    {
        "break", "emphasis", "prosody", "say-as", "sub", "phoneme", "p", "s", "lang", "mstts:express-as", "mstts:silence",
    };

    // Elements that hold only text.
    private static readonly HashSet<string> TextOnly = new(StringComparer.Ordinal) { "say-as", "sub", "phoneme" };

    private static readonly Regex Tag = new(@"<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9:_-]*)(?<attributes>(?:\s+[^<>]*?)?)(?<empty>/)?>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Attribute = new(@"(?<name>[A-Za-z][A-Za-z0-9:_-]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Entity = new(@"&(?:(?<named>amp|lt|gt|quot|apos)|#(?<decimal>\d+)|#x(?<hex>[0-9A-Fa-f]+));", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private SpokenLine(string source, IReadOnlyList<SpokenNode> nodes, IReadOnlyList<SpokenProblem> problems, string? stripped = null)
    {
        Source = source;
        Nodes = nodes;
        Problems = problems;
        HasMarkup = nodes.Any(node => node.Name is not null);
        Plain = HasMarkup ? Normalize(Render(nodes, SsmlSupport.None, Mode.Plain)) : stripped ?? source;
        Reduced = HasMarkup ? Normalize(Render(nodes, SsmlSupport.None, Mode.Reduced)) : stripped ?? source;
    }

    /// <summary>The line as the author wrote it.</summary>
    public string Source { get; }

    /// <summary>The line's content: text and elements.</summary>
    public IReadOnlyList<SpokenNode> Nodes { get; }

    /// <summary>What is wrong with the markup; empty for a plain line.</summary>
    public IReadOnlyList<SpokenProblem> Problems { get; }

    /// <summary>Whether the line holds an SSML element.</summary>
    public bool HasMarkup { get; }

    /// <summary>The words without tags: a <c>sub</c> shows its content, a break nothing. What captions, the timeline, the review page and the preview show.</summary>
    public string Plain { get; }

    /// <summary>For a voice that cannot follow markup: plain text with each <c>sub</c> replaced by its alias.</summary>
    public string Reduced { get; }

    /// <summary>The names of the elements the line uses, each once, in order.</summary>
    public IReadOnlyList<string> ElementsUsed => [.. Walk(Nodes).Where(node => node.Name is not null).Select(node => node.Name!).Distinct()];

    /// <summary>
    /// The line's inner SSML, for a voice that supports <paramref name="supported"/>: those elements kept, the others dropped with their
    /// words kept (a <c>sub</c> a voice lacks becomes its alias); text is XML-escaped. The synthesizer adds the document around it.
    /// </summary>
    /// <param name="supported">The elements the voice follows.</param>
    public string ToSsml(IReadOnlySet<string> supported)
    {
        ArgumentNullException.ThrowIfNull(supported);
        return Render(Nodes, supported, Mode.Ssml);
    }

    /// <summary>Parses a line.</summary>
    /// <param name="text">What the script says.</param>
    public static SpokenLine Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.IndexOf('<', StringComparison.Ordinal) < 0 && text.IndexOf('&', StringComparison.Ordinal) < 0)
        {
            return new SpokenLine(text, [new SpokenNode(null, text, [], [])], []);
        }

        List<SpokenProblem> problems = [];
        var root = new Open(null, [], 0);
        var stack = new Stack<Open>();
        stack.Push(root);
        var position = 0;
        var anyElement = false;

        foreach (Match match in Tag.Matches(text))
        {
            var name = match.Groups["name"].Value;
            var close = match.Groups["close"].Success;
            var known = Elements.Contains(name);
            if (!known)
            {
                AddText(text[position..match.Index]);
                position = match.Index + match.Length;
                problems.Add(new SpokenProblem(SpokenProblemSeverity.Warning, "VID021", $"<{name}> is not an SSML element, so it is not spoken. Write &lt; to say a '<'.", match.Index));
                continue;
            }

            anyElement = true;
            AddText(text[position..match.Index]);
            position = match.Index + match.Length;
            if (close)
            {
                if (stack.Count > 1 && stack.Peek().Name == name)
                {
                    Close();
                }
                else
                {
                    problems.Add(new SpokenProblem(SpokenProblemSeverity.Error, "VID020", $"</{name}> closes nothing that is open.", match.Index));
                }

                continue;
            }

            var attributes = Attribute.Matches(match.Groups["attributes"].Value).Select(attribute => new KeyValuePair<string, string>(attribute.Groups["name"].Value, Decode(attribute.Groups["value"].Value))).ToList();
            if (stack.Count > 1 && TextOnly.Contains(stack.Peek().Name!))
            {
                problems.Add(new SpokenProblem(SpokenProblemSeverity.Error, "VID024", $"<{name}> cannot go inside <{stack.Peek().Name}>, which holds only text.", match.Index));
            }

            var element = new Open(name, attributes, match.Index);
            if (match.Groups["empty"].Success)
            {
                stack.Peek().Children.Add(new SpokenNode(name, null, attributes, []) { Position = match.Index });
            }
            else if (name == "break")
            {
                // <break time="1s"> with no end is read as an empty element, as most authors mean it.
                stack.Peek().Children.Add(new SpokenNode(name, null, attributes, []) { Position = match.Index });
            }
            else
            {
                stack.Push(element);
            }
        }

        AddText(text[position..]);
        while (stack.Count > 1)
        {
            var open = stack.Peek();
            problems.Add(new SpokenProblem(SpokenProblemSeverity.Error, "VID020", $"<{open.Name}> is never closed.", open.Position));
            Close();
        }

        if (!anyElement)
        {
            // Tags that are no element, or none: the line is plain text, minus what was dropped.
            var plain = string.Concat(root.Children.Select(child => child.Text));
            return new SpokenLine(text, [new SpokenNode(null, plain, [], [])], problems, plain);
        }

        foreach (var node in Walk(root.Children).Where(node => node.Name is not null))
        {
            if (Rules.Problem(node) is { } wrong)
            {
                problems.Add(new SpokenProblem(SpokenProblemSeverity.Error, "VID022", wrong, node.Position));
            }
        }

        problems.Sort((a, b) => a.Position.CompareTo(b.Position));
        return new SpokenLine(text, root.Children, problems);

        void AddText(string raw)
        {
            if (raw.Length > 0)
            {
                stack.Peek().Children.Add(new SpokenNode(null, Decode(raw), [], []));
            }
        }

        void Close()
        {
            var done = stack.Pop();
            stack.Peek().Children.Add(new SpokenNode(done.Name, null, done.Attributes, done.Children) { Position = done.Position });
        }
    }

    /// <summary>Maps the text of a line, leaving every tag as written and the text inside <c>sub</c>, <c>phoneme</c> and <c>say-as</c> alone.</summary>
    internal static string MapText(string source, Func<string, string> map)
    {
        var result = new StringBuilder();
        var position = 0;
        var inside = 0;
        foreach (Match match in Tag.Matches(source))
        {
            var text = source[position..match.Index];
            result.Append(inside == 0 ? map(text) : text).Append(match.Value);
            position = match.Index + match.Length;
            if (TextOnly.Contains(match.Groups["name"].Value))
            {
                if (match.Groups["close"].Success)
                {
                    inside = Math.Max(0, inside - 1);
                }
                else if (!match.Groups["empty"].Success)
                {
                    inside++;
                }
            }
        }

        var tail = source[position..];
        return result.Append(inside == 0 ? map(tail) : tail).ToString();
    }

    // Markup's text is read as the author means it: a bare & is an ampersand, an entity is its character.
    private static string Decode(string raw)
        => raw.IndexOf('&', StringComparison.Ordinal) < 0
            ? raw
            : Entity.Replace(raw, match =>
                match.Groups["named"].Success
                    ? match.Groups["named"].Value switch { "amp" => "&", "lt" => "<", "gt" => ">", "quot" => "\"", _ => "'" }
                    : char.ConvertFromUtf32(int.Parse(match.Groups["decimal"].Success ? match.Groups["decimal"].Value : match.Groups["hex"].Value, match.Groups["decimal"].Success ? NumberStyles.None : NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&apos;", StringComparison.Ordinal);

    private static string Normalize(string text) => string.Join(' ', text.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<SpokenNode> Walk(IEnumerable<SpokenNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Children))
            {
                yield return child;
            }
        }
    }

    private enum Mode
    {
        Plain,
        Reduced,
        Ssml,
    }

    // Plain: the words as written (a sub shows its content). Reduced: a sub's alias, no tags. Ssml: the elements in supported kept, a sub a voice lacks as its alias, text escaped.
    private static string Render(IEnumerable<SpokenNode> nodes, IReadOnlySet<string> supported, Mode mode)
    {
        var text = new StringBuilder();
        foreach (var node in nodes)
        {
            if (node.Name is null)
            {
                text.Append(mode == Mode.Ssml ? Escape(node.Text!) : node.Text);
                continue;
            }

            string Inner() => Render(node.Children, supported, mode);
            if (node.Name == "sub" && mode != Mode.Plain && (mode == Mode.Reduced || !supported.Contains("sub")))
            {
                var alias = node.Attribute("alias") ?? Inner();
                text.Append(mode == Mode.Ssml ? Escape(alias) : alias);
                continue;
            }

            if (mode != Mode.Ssml || !supported.Contains(node.Name))
            {
                // A paragraph or a sentence ends where the next begins: without its tags the words must not run together.
                text.Append(Inner());
                if (node.Name is "p" or "s")
                {
                    text.Append(' ');
                }

                continue;
            }

            text.Append('<').Append(node.Name);
            foreach (var (key, value) in node.Attributes)
            {
                text.Append(' ').Append(key).Append("=\"").Append(Escape(value)).Append('"');
            }

            text.Append(node.Children.Count == 0 ? "/>" : $">{Inner()}</{node.Name}>");
        }

        return text.ToString();
    }

    // What each element's attributes may be (VID022): the SSML 1.1 and Azure Speech values.
    private static class Rules
    {
        private static readonly HashSet<string> Strengths = new(StringComparer.Ordinal) { "none", "x-weak", "weak", "medium", "strong", "x-strong" };
        private static readonly HashSet<string> Levels = new(StringComparer.Ordinal) { "strong", "moderate", "reduced", "none" };
        private static readonly HashSet<string> SayAs = new(StringComparer.Ordinal)
        {
            "cardinal", "ordinal", "number", "digits", "characters", "spell-out", "date", "time", "telephone", "fraction", "address", "currency", "unit", "verbatim", "expletive",
        };

        private static readonly Regex Time = new(@"^(?<n>\d+(?:\.\d+)?)(?<unit>ms|s)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Rate = new(@"^(?:x-slow|slow|medium|fast|x-fast|default|[+-]?\d+(?:\.\d+)?%?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Pitch = new(@"^(?:x-low|low|medium|high|x-high|default|[+-]?\d+(?:\.\d+)?(?:%|Hz|st))$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Volume = new(@"^(?:silent|x-soft|soft|medium|loud|x-loud|default|[+-]?\d+(?:\.\d+)?(?:dB|%)?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
        public static string? Problem(SpokenNode node)
        {
            switch (node.Name)
            {
                case "break":
                    if (node.Attribute("time") is { } time)
                    {
                        var match = Time.Match(time);
                        if (!match.Success)
                        {
                            return $"<break time=\"{time}\"> is a time such as 500ms or 2s.";
                        }

                        var milliseconds = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) * (match.Groups["unit"].Value == "s" ? 1000 : 1);
                        if (milliseconds > 20000)
                        {
                            return $"<break time=\"{time}\"> is over the 20 s (20000 ms) a break may last.";
                        }
                    }

                    return node.Attribute("strength") is { } strength && !Strengths.Contains(strength)
                        ? $"<break strength=\"{strength}\"> is one of {string.Join(", ", Strengths)}."
                        : null;
                case "emphasis":
                    return node.Attribute("level") is { } level && !Levels.Contains(level) ? $"<emphasis level=\"{level}\"> is one of {string.Join(", ", Levels)}." : null;
                case "prosody":
                    if (node.Attribute("rate") is null && node.Attribute("pitch") is null && node.Attribute("volume") is null)
                    {
                        return "<prosody> needs a rate, a pitch or a volume.";
                    }

                    return Bad(node, "rate", Rate) ?? Bad(node, "pitch", Pitch) ?? Bad(node, "volume", Volume);
                case "say-as":
                    return node.Attribute("interpret-as") is not { } kind
                        ? "<say-as> needs interpret-as, such as cardinal or digits."
                        : SayAs.Contains(kind) ? null : $"<say-as interpret-as=\"{kind}\"> is not one the voices know: {string.Join(", ", SayAs)}.";
                case "sub":
                    return string.IsNullOrWhiteSpace(node.Attribute("alias")) ? "<sub> needs an alias: what is said instead of its text." : null;
                case "phoneme":
                    return string.IsNullOrWhiteSpace(node.Attribute("ph")) ? "<phoneme> needs ph, the pronunciation." : null;
                case "lang":
                    return string.IsNullOrWhiteSpace(node.Attribute("xml:lang")) ? "<lang> needs xml:lang, such as en-US." : null;
                case "mstts:express-as":
                    return string.IsNullOrWhiteSpace(node.Attribute("style")) ? "<mstts:express-as> needs a style, such as narrator." : null;
                case "mstts:silence":
                    return node.Attribute("type") is null || node.Attribute("value") is null ? "<mstts:silence> needs a type and a value." : null;
                default:
                    return null;
            }
        }

        private static string? Bad(SpokenNode node, string name, Regex pattern)
            => node.Attribute(name) is { } value && !pattern.IsMatch(value) ? $"<prosody {name}=\"{value}\"> is not a value the voices know." : null;
    }

    private sealed class Open(string? name, List<KeyValuePair<string, string>> attributes, int position)
    {
        public string? Name { get; } = name;

        public List<KeyValuePair<string, string>> Attributes { get; } = attributes;

        public int Position { get; } = position;

        public List<SpokenNode> Children { get; } = [];
    }
}

/// <summary>Which SSML elements a voice follows, by how a script names it.</summary>
public static class SsmlSupport
{
    /// <summary>A voice that follows no markup: its lines are <see cref="SpokenLine.Reduced"/>.</summary>
    public static readonly IReadOnlySet<string> None = new HashSet<string>();

    /// <summary>Windows' voices, measured (https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/voice/ssml-results.md): they pause, stress, change rate, say aliases and follow phonemes.</summary>
    public static readonly IReadOnlySet<string> Windows = new HashSet<string>(StringComparer.Ordinal) { "break", "emphasis", "prosody", "sub", "phoneme", "p", "s" };

    /// <summary>Azure Speech's neural voices: every element of v1.</summary>
    public static readonly IReadOnlySet<string> AzureSpeech = SpokenLine.Elements;

    /// <summary>
    /// Azure Speech's MAI voices (<c>en-GB-Emily:MAI-Voice-2.1</c>), measured (https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/voice/ssml-results.md): a <c>break</c> is not followed (use
    /// <c>p</c> or <c>s</c> for a pause) and a <c>phoneme</c> is spoken as text.
    /// </summary>
    public static readonly IReadOnlySet<string> AzureSpeechMai = new HashSet<string>(SpokenLine.Elements.Where(element => element is not ("break" or "phoneme")), StringComparer.Ordinal);

    /// <summary>
    /// The elements the voice a script names follows: Azure Speech's for <c>azure-speech-service:</c>, none for <c>azure:</c> (Azure OpenAI), Windows' for
    /// any other name. Null when no voice is named, so the settings decide at record time and the check cannot tell.
    /// </summary>
    /// <param name="voice">The voice as a script names it.</param>
    public static IReadOnlySet<string>? Of(string? voice)
        => string.IsNullOrWhiteSpace(voice)
            ? null
            : Bruinet.Narrator.AzureSpeech.Owns(voice)
                ? voice.Contains(":MAI-Voice", StringComparison.OrdinalIgnoreCase) ? AzureSpeechMai : AzureSpeech
                : AzureOpenAiSpeech.Owns(voice) ? None : Windows;
}
