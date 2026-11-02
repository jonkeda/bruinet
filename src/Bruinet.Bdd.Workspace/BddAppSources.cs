using System.Text.RegularExpressions;

namespace Bruinet.Bdd.Workspace;

/// <summary>A place in an app's sources that renders an element a locator finds.</summary>
/// <param name="File">The file.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Text">The line, trimmed and capped.</param>
/// <param name="IsAttribute">Whether the value is a test id there (<c>data-testid</c>, <c>AutomationId</c>, <c>x:Name</c>, <c>id</c>), not only a string.</param>
public sealed record BddAppSourceHit(string File, int Line, string Text, bool IsAttribute);

/// <summary>A locator's value as a page object writes it: the whole value, or the fixed start of one built at run time.</summary>
/// <param name="Value">The value, or its start.</param>
/// <param name="IsPrefix">Whether the rest is made at run time (<c>$"Row_{id}"</c>).</param>
public sealed record BddLocatorValue(string Value, bool IsPrefix);

/// <summary>What tells apart the places of one id in several apps of a solution (a web and a MAUI app with the same ids).</summary>
/// <param name="PageObjectFile">The page object's file: apps whose project name shares more words with its test project's come first.</param>
/// <param name="PageName">The page object's type name: files whose name shares words with it come first (<c>ButtonsView.xaml</c> for <c>ButtonsTestPage</c>).</param>
/// <param name="Xaml">Whether the control is a XAML one (MAUI, Avalonia, WPF): true puts XAML first, false web markup, null neither.</param>
public sealed record BddAppSourceHint(string? PageObjectFile, string? PageName, bool? Xaml);

/// <summary>
/// Where in an app's sources an element a page object finds is made (AI7): the locator's value searched in the markup and
/// code of the app, not its tests. No build step: the test id a page object finds by is a literal in the app's
/// <c>.razor</c>, <c>.cshtml</c> or XAML (or its code), or starts with one when the app builds it at run time.
/// </summary>
public sealed partial class BddAppSources
{
    private static readonly string[] Extensions = [".razor", ".cshtml", ".html", ".htm", ".xaml", ".axaml", ".cs"];
    private static readonly HashSet<string> SkippedFolders = new(
        ["bin", "obj", "node_modules", ".git", ".vs", ".bruinet", "TestResults", "artifacts"],
        StringComparer.OrdinalIgnoreCase);

    private readonly Lazy<IReadOnlyList<string>> _files;
    private readonly Dictionary<string, HashSet<string>> _projectWords = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="root">The solution's folder.</param>
    public BddAppSources(string root)
    {
        Root = root;
        _files = new Lazy<IReadOnlyList<string>>(() => [.. Files(root)]);
    }

    /// <summary>The solution's folder searched.</summary>
    public string Root { get; }

    /// <summary>The value of a locator as a failure gives it (<c>DataTestId:TodoList_Sync</c>); null for one with none.</summary>
    public static BddLocatorValue? ValueOf(string? locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
        {
            return null;
        }

        // A nested locator (DataTestId:List > DataTestId:Row_3) finds by its last part.
        var last = locator.LastIndexOf(" > ", StringComparison.Ordinal);
        locator = last >= 0 ? locator[(last + 3)..] : locator;
        var colon = locator.IndexOf(':', StringComparison.Ordinal);
        var value = Unwrapped((colon >= 0 ? locator[(colon + 1)..] : locator).Trim());
        return value is null ? null : new BddLocatorValue(value, IsPrefix: false);
    }

    /// <summary>
    /// The locator value a page object member declares, from its source: the first string on its declaration (up to the
    /// <c>;</c>, six lines at most); an interpolated one's start before its first <c>{</c>. Null when it has none (a
    /// constant, a <c>nameof</c>).
    /// </summary>
    public static BddLocatorValue? ValueAt(string file, int line)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        var text = string.Join(" ", File.ReadLines(file).Skip(line - 1).Take(6).TakeWhile((current, index) => index == 0 || !current.TrimStart().StartsWith("///", StringComparison.Ordinal)));
        var end = text.IndexOf(';', StringComparison.Ordinal);
        var declaration = end >= 0 ? text[..end] : text;
        foreach (Match literal in Literal().Matches(declaration))
        {
            var value = literal.Groups["value"].Value;
            if (!literal.Groups["interpolated"].Success)
            {
                if (Unwrapped(value) is { } plain)
                {
                    return new BddLocatorValue(plain, IsPrefix: false);
                }

                continue;
            }

            var open = value.IndexOf('{', StringComparison.Ordinal);
            var start = open < 0 ? value : value[..open];
            if (start.Length > 0)
            {
                return new BddLocatorValue(start, IsPrefix: open >= 0);
            }
        }

        return null;
    }

    /// <summary>
    /// The places in the app's sources with <paramref name="value"/>: where it is a test id first, then markup, then code;
    /// at most <paramref name="max"/>. A test project's files are not the app's and are left out.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="max">The most places given.</param>
    /// <param name="hint">What puts the page object's own app first when the id is in several.</param>
    public IReadOnlyList<BddAppSourceHit> Find(BddLocatorValue value, int max = 5, BddAppSourceHint? hint = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var escaped = Regex.Escape(value.Value);
        // Exactly the value, quoted; or, for a start, the value followed by what the app adds ({id}, @id, + id).
        var quoted = value.IsPrefix
            ? new Regex(@"(?<![\w-])" + escaped + @"(?=\{|@|""\s*\+)", RegexOptions.CultureInvariant)
            : new Regex(@"(?<![\w-])" + escaped + @"(?=[""'])", RegexOptions.CultureInvariant);
        // A test id when the value is that attribute's own: data-testid="value", AutomationId="value", id="@value…".
        var attribute = new Regex(TestIdAttributes + @"\s*=\s*[""']@?" + escaped, RegexOptions.CultureInvariant);
        var hits = new List<BddAppSourceHit>();
        foreach (var file in _files.Value)
        {
            if (hits.Count >= MostHits)
            {
                break;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var index = 0; index < lines.Length && hits.Count < MostHits; index++)
            {
                var line = lines[index];
                if (!line.Contains(value.Value, StringComparison.Ordinal) || !quoted.IsMatch(line))
                {
                    continue;
                }

                var trimmed = line.Trim();
                hits.Add(new BddAppSourceHit(file, index + 1, trimmed.Length > 160 ? trimmed[..160] + "…" : trimmed, attribute.IsMatch(line)));
            }
        }

        return [.. hits
            .OrderByDescending(hit => hit.IsAttribute)
            .ThenByDescending(hit => hint?.Xaml is { } xaml && Family(hit.File) == xaml)
            .ThenByDescending(hit => hint?.PageName is { } page ? Shared(Words(page), Words(Path.GetFileNameWithoutExtension(hit.File))) : 0)
            .ThenByDescending(hit => hint?.PageObjectFile is { } pageObject ? Shared(ProjectWords(pageObject), ProjectWords(hit.File)) : 0)
            .ThenBy(hit => hit.File.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ThenBy(hit => hit.File, StringComparer.Ordinal)
            .ThenBy(hit => hit.Line)
            .Take(max)];
    }

    // The app's markup and code: no build output, no test project, no other checkout (a worktree, a submodule: a folder
    // with its own .git), no dot-folder, no link.
    private static IEnumerable<string> Files(string folder)
    {
        IEnumerable<string> files;
        IEnumerable<string> folders;
        try
        {
            files = Directory.EnumerateFiles(folder).Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToList();
            folders = Directory.EnumerateDirectories(folder)
                .Where(below => !File.GetAttributes(below).HasFlag(FileAttributes.ReparsePoint))
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }

        foreach (var below in folders)
        {
            var name = Path.GetFileName(below);
            if (SkippedFolders.Contains(name) || name.StartsWith('.') || IsTestFolder(name) || IsOtherCheckout(below) || IsTestProject(below))
            {
                continue;
            }

            foreach (var file in Files(below))
            {
                yield return file;
            }
        }
    }

    // True for XAML, false for web markup, null for code.
    private static bool? Family(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".xaml" or ".axaml" => true,
        ".razor" or ".cshtml" or ".html" or ".htm" => false,
        _ => null,
    };

    private static int Shared(HashSet<string> left, HashSet<string> right) => left.Count(right.Contains);

    // A name's words, lower case, without the ones every page or view has.
    private static HashSet<string> Words(string name)
        => [.. NameWord().Matches(name).Select(word => word.Value.ToLowerInvariant()).Where(word => !CommonWords.Contains(word))];

    // The words of the project a file is in (the folder with its .csproj), without the test ones; once per folder.
    private HashSet<string> ProjectWords(string file)
    {
        var start = Path.GetDirectoryName(Path.GetFullPath(file)) ?? string.Empty;
        if (_projectWords.TryGetValue(start, out var known))
        {
            return known;
        }

        HashSet<string> words = [];
        for (var folder = start; folder.Length > 0; folder = Path.GetDirectoryName(folder) ?? string.Empty)
        {
            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.csproj").Any())
            {
                words = [.. Path.GetFileName(folder).Split('.').Select(word => word.ToLowerInvariant()).Where(word => !CommonWords.Contains(word))];
                break;
            }
        }

        _projectWords[start] = words;
        return words;
    }

    private static bool IsOtherCheckout(string folder) => Path.Exists(Path.Combine(folder, ".git"));

    // A project that references a test framework, whatever its name (an app beside the tests is searched).
    private static bool IsTestProject(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.csproj").Any(project => TestFramework().IsMatch(File.ReadAllText(project)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static readonly HashSet<string> CommonWords = new(
        ["page", "view", "test", "tests", "uitests", "uat", "testsupport", "app", "control", "components", "pages"],
        StringComparer.Ordinal);

    // The test id a locator's text names: a CSS selector's attribute value ([data-testid='x']) or id (#x), or the text
    // itself; null for none, or for a tag name (h1, button), which no source line marks.
    private static string? Unwrapped(string text)
    {
        if (SelectorValue().Match(text) is { Success: true } attribute)
        {
            return attribute.Groups["value"].Value;
        }

        if (SelectorId().Match(text) is { Success: true } id)
        {
            return id.Groups["value"].Value;
        }

        return text.Length == 0 || TagNames.Contains(text) ? null : text;
    }

    private static readonly HashSet<string> TagNames = new(
        ["a", "button", "div", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "footer", "img", "input", "label", "li", "main", "nav", "ol", "p", "section", "select", "span", "table", "td", "textarea", "th", "tr", "ul"],
        StringComparer.OrdinalIgnoreCase);

    private const int MostHits = 200;
    private const string TestIdAttributes = @"(?:data-testid|data-test-id|data-test|AutomationProperties\.AutomationId|AutomationId|x:Name|\bid)";

    private static bool IsTestFolder(string name)
        => name.EndsWith("Tests", StringComparison.Ordinal)
           || name.EndsWith("UITest", StringComparison.Ordinal)
           || name.EndsWith(".Specs", StringComparison.Ordinal)
           || name.EndsWith(".E2E", StringComparison.Ordinal)
           || name.EndsWith(".Uat", StringComparison.Ordinal)
           || name.EndsWith(".TestSupport", StringComparison.Ordinal)
           || name.Contains(".TestSupport.", StringComparison.Ordinal);

    [GeneratedRegex(@"(?<interpolated>\$)?@?""(?<value>(?:[^""\\]|\\.)*)""", RegexOptions.CultureInvariant)]
    private static partial Regex Literal();

    [GeneratedRegex(@"\[@?[\w:-]+\s*=\s*['""]?(?<value>[^'""\]]+)['""]?\]", RegexOptions.CultureInvariant)]
    private static partial Regex SelectorValue();

    [GeneratedRegex(@"^#(?<value>[\w-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SelectorId();

    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NameWord();

    [GeneratedRegex(@"<PackageReference\s+Include=""(?:xunit|xunit\.v3|NUnit|MSTest\.TestFramework|MSTest|Microsoft\.NET\.Test\.Sdk)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TestFramework();
}
