using System.Globalization;
using System.Text;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Code;

/// <summary>
/// A page object drafted from a recorded page by rules, with no model: a member per suggestion with a
/// stable locator on the page as it opens (a hidden dialog or menu in its document too), and listed in a
/// comment for a person: the unstable ones, and what only a click shows (a later state of the page). The draft is a <c>partial</c> class in <c>&lt;Name&gt;.scraped.cs</c>; a person's
/// additions go in the other part, <c>&lt;Name&gt;.cs</c>, which a draft never touches.
/// </summary>
public static class PageDrafter
{
    /// <summary>The ending of a drafted file.</summary>
    public const string Extension = ".scraped.cs";

    // Folders of a path that say nothing about the page.
    private static readonly HashSet<string> Folders = new(StringComparer.OrdinalIgnoreCase) { "pages", "page", "views", "app", "index" };

    /// <summary>
    /// A page's class name from its key: <c>/invoices/{id}/edit</c> is <c>InvoiceEditPage</c>, <c>/</c> is
    /// <c>HomePage</c>. A segment that is the <paramref name="title"/>'s words run together takes their
    /// breaks: <c>/userform</c> titled "User Form" is <c>UserFormPage</c>.
    /// </summary>
    public static string ClassName(string key, string? title = null)
    {
        var titleWords = string.IsNullOrEmpty(title) ? [] : DangerWordsSplit(title).ToArray();
        var path = key.Split('#')[^1];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var words = new List<string>();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = Path.GetFileNameWithoutExtension(segments[i]);
            if (segment == "{id}" || Folders.Contains(segment))
                continue;
            // The collection a record is in names one record: invoices/{id} is an invoice.
            if (i + 1 < segments.Length && segments[i + 1] == "{id}")
                segment = Singular(segment);
            foreach (var word in DangerWordsSplit(segment))
                words.AddRange(Broken(word, titleWords));
        }
        var name = string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        if (name.Length == 0)
            name = "Home";
        if (char.IsDigit(name[0]))
            name = "Page" + name;
        return name.EndsWith("Page", StringComparison.Ordinal) ? name : name + "Page";
    }

    /// <summary>
    /// The capture a page object is made from: in the session of the page's newest capture, the page as
    /// it opened (not a state a click showed, which Explore names), else its oldest capture there.
    /// </summary>
    public static PageCapture Basis(RecordedPage page)
    {
        var session = page.Captures.Where(c => c.Session == page.Latest.Session).Reverse().ToList();
        return session.FirstOrDefault(c => c.Event.Name is null) ?? session[0];
    }

    /// <summary>Drafts one page from its <see cref="Basis"/>; what only its other states show is listed, not drafted.</summary>
    public static PageDraft Draft(ScrapeProject project, RecordingIndex index, RecordedPage page, string className)
    {
        var basis = Basis(page);
        var blazor = project.Config.Platform == "blazor";
        static string Key(ControlSuggestion s) => $"{s.Scope}|{s.Locator}";
        var opened = ControlAdvisor.Suggest(index.Read(basis), project.Config.TestIds, blazor);
        var have = opened.Select(Key).ToHashSet(StringComparer.Ordinal);
        // Verify opens the page as it opens: a member only a click shows would not be found there.
        var afterClick = page.Captures.Where(c => c.Session == basis.Session && c != basis)
            .SelectMany(c => ControlAdvisor.Suggest(index.Read(c), project.Config.TestIds, blazor).Where(s => s.Stable).Select(s => (Capture: c, Suggestion: s)))
            .Where(x => !have.Contains(Key(x.Suggestion)))
            .DistinctBy(x => Key(x.Suggestion))
            .Select(x => new ShownAfter(x.Suggestion, x.Capture.Event.DisplayName))
            .ToArray();

        var members = Unique(opened.Where(m => m.Stable && !Containers.Contains(m.Control)).ToList(), className);
        var unstable = opened.Where(m => !m.Stable).ToArray();
        var containers = opened.Where(m => m.Stable && Containers.Contains(m.Control)).ToArray();
        var header = new ScrapedHeader { Page = page.Key, From = $"{basis.Session}/{basis.Event.Snapshot}", Hash = SnapshotFile.ContentHash(index.Read(basis)) };
        var (folder, ns) = project.PagesFor(page.Key);
        var path = Path.Combine(folder, className + Extension);
        return new PageDraft(page.Key, className, path, Code(ns, className, header, members, unstable, containers, afterClick, blazor), members, unstable) { AfterClick = afterClick };
    }

    private static string Code(string pageNamespace, string className, ScrapedHeader header, IReadOnlyList<ControlSuggestion> members, IReadOnlyList<ControlSuggestion> unstable, IReadOnlyList<ControlSuggestion> containers, IReadOnlyList<ShownAfter> afterClick, bool blazor)
    {
        var baseClass = blazor ? "BlazorPageObjectBase" : "PageObjectBase";
        var namespaces = new SortedSet<string>(StringComparer.Ordinal) { "Bruinet.Core.Locators", "Bruinet.Html.Interfaces", blazor ? "Bruinet.Blazor.Pages" : "Bruinet.Html.Pages" };
        foreach (var member in members)
            namespaces.Add(member.Namespace);

        // A member in a frame is on the frame's scope: FrameScope<Page>, FrameScope<FrameScope<Page>> for a frame in a frame.
        var byName = members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        string ParentType(ControlSuggestion m) =>
            m.Scope is { } scope && byName.TryGetValue(scope, out var frame) ? $"FrameScope<{ParentType(frame)}>" : className;

        var code = new StringBuilder();
        code.AppendLine(header.ToString());
        foreach (var ns in namespaces)
            code.AppendLine(CultureInfo.InvariantCulture, $"using {ns};");
        code.AppendLine();
        code.AppendLine(CultureInfo.InvariantCulture, $"namespace {pageNamespace};");
        code.AppendLine();
        code.AppendLine(CultureInfo.InvariantCulture, $"/// <summary>The page {Xml(header.Page!)}: drafted by the Scraper from a recording. Draft again to update; add to the other part, {className}.cs.</summary>");
        code.AppendLine(CultureInfo.InvariantCulture, $"public sealed partial class {className}(IHtmlTestContext context) : {baseClass}<{className}>(context)");
        code.AppendLine("{");
        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            var parent = m.Scope is { } scope && byName.ContainsKey(scope) ? scope : "this";
            if (i > 0)
                code.AppendLine();
            code.AppendLine(CultureInfo.InvariantCulture, $"    public {m.Control}<{ParentType(m)}> {m.Name} => new({parent}, {m.Locator});");
        }
        if (unstable.Count > 0)
        {
            code.AppendLine();
            code.AppendLine("    // Not drafted: no stable locator. Give the element a test id, or pick a locator by hand.");
            foreach (var m in unstable)
                code.AppendLine(CultureInfo.InvariantCulture, $"    // {m.Control} {m.Name}: {m.Locator} ({m.Why})");
        }
        if (containers.Count > 0)
        {
            code.AppendLine();
            code.AppendLine("    // Not drafted: a container is a control of its own. Make one deriving from it (the scrape-pages skill), then add the member.");
            foreach (var m in containers)
                code.AppendLine(CultureInfo.InvariantCulture, $"    // {m.Control}<TParent, TScope> {m.Name}: {m.Locator}");
        }
        if (afterClick.Count > 0)
        {
            code.AppendLine();
            code.AppendLine("    // Not drafted: shown only after a click (a state of the page, or a collection's other rows).");
            foreach (var a in afterClick)
                code.AppendLine(CultureInfo.InvariantCulture, $"    // {a.Suggestion.Control} {a.Suggestion.Name}: {a.Suggestion.Locator}, in \"{a.State}\"");
        }
        code.AppendLine("}");
        return code.ToString();
    }

    // Controls a page cannot use as they are: each is the base of a control made for its app.
    private static readonly HashSet<string> Containers = new(StringComparer.Ordinal) { "TabContainerControl" };

    // What a page object has already: a member of that name would hide it.
    private static readonly string[] Reserved =
    [
        "AssertIdle", "AssertLoaded", "AssertTitle", "Context", "DefaultLocatorStrategy", "FindElement", "FindElements", "GetTitle", "IsBusy",
        "IsLoaded", "IsReady", "Name", "Page", "ProbeReadiness", "Self", "TakeScreenshot", "TryFindElement", "Url", "WaitBusy", "WaitLoaded", "WaitReady", "WaitTitle",
    ];

    // Names are unique in the class, never the class's own and never a base member's.
    private static List<ControlSuggestion> Unique(List<ControlSuggestion> members, string className)
    {
        var used = new HashSet<string>(Reserved, StringComparer.Ordinal) { className };
        for (var i = 0; i < members.Count; i++)
        {
            var name = members[i].Name;
            var unique = name;
            for (var n = 2; !used.Add(unique); n++)
                unique = name + n.ToString(CultureInfo.InvariantCulture);
            members[i] = members[i] with { Name = unique };
        }
        return members;
    }

    private static string Singular(string word) =>
        word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) ? word[..^3] + "y"
        : word.EndsWith("ses", StringComparison.OrdinalIgnoreCase) ? word[..^2]
        : word.EndsWith('s') && !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ? word[..^1]
        : word;

    private static IEnumerable<string> DangerWordsSplit(string text) => Explore.DangerWords.Words(text);

    // A word that is two or more consecutive title words run together, as those words; else itself.
    private static IEnumerable<string> Broken(string word, string[] titleWords)
    {
        for (var i = 0; i < titleWords.Length; i++)
        {
            var joined = titleWords[i];
            for (var k = i + 1; k < titleWords.Length && joined.Length < word.Length; k++)
            {
                joined += titleWords[k];
                if (joined == word)
                    return titleWords[i..(k + 1)];
            }
        }
        return [word];
    }

    private static string Xml(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}

/// <summary>A drafted page object: the page, its class, where it goes, its code, its members and those left out.</summary>
public sealed record PageDraft(string Key, string ClassName, string Path, string Code, IReadOnlyList<ControlSuggestion> Members, IReadOnlyList<ControlSuggestion> Unstable)
{
    /// <summary>What only a state a click showed has: listed in the draft, not drafted.</summary>
    public IReadOnlyList<ShownAfter> AfterClick { get; init; } = [];
}

/// <summary>A suggestion only a later state of the page shows, and that state's name.</summary>
public sealed record ShownAfter(ControlSuggestion Suggestion, string State);
