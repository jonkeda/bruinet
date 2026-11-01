using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Playwright;

namespace Bruinet.Scraper.Html;

/// <summary>
/// Checks a project's page objects against the running app: each page is opened where it was recorded,
/// and every member's locator (read from the source) is looked up as <c>Bruinet.Html.Playwright</c> looks
/// it up, a member in a frame inside its frame. Found once passes; not found, or found several times, fails.
/// </summary>
public static class PageVerifier
{
    /// <summary>Verifies every page object with a <c>page</c> header whose page is recorded.</summary>
    /// <param name="project">The project.</param>
    /// <param name="page">The browser page to look in; it is taken to each recorded page in turn.</param>
    /// <param name="settle">Waits for the page to settle after it is opened.</param>
    public static async Task<VerifyResult> RunAsync(ScrapeProject project, IPage page, Func<Task> settle)
    {
        var index = project.Index();
        var pages = new List<VerifiedPage>();
        var all = ProjectCode.Read(project);
        // The project's own controls: their constructor's arguments mean what the control says (a header's
        // text), not a selector, so only Bruinet's controls' arguments are looked up.
        var own = all.Where(c => c.Kind != CodeKind.Page).Select(c => BaseName(c.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var code in all.Where(c => c.Project == project.Name && c.Kind == CodeKind.Page && c.Header?.Page is not null))
        {
            var recorded = index.Page(code.Header!.Page!);
            if (recorded is null)
            {
                pages.Add(new VerifiedPage(code.Header.Page!, code.Name, null, [], "The page is not recorded: nothing says where it is."));
                continue;
            }
            var location = recorded.Latest.Event.Location;
            try
            {
                await page.GotoAsync(location, PlaywrightScrapeSource.Opening).ConfigureAwait(false);
                await settle().ConfigureAwait(false);
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException)
            {
                pages.Add(new VerifiedPage(recorded.Key, code.Name, location, [], $"Could not open it: {e.Message.ReplaceLineEndings(" ")}"));
                continue;
            }
            pages.Add(new VerifiedPage(recorded.Key, code.Name, location, await MembersAsync(page, code, own).ConfigureAwait(false), null));
        }
        return new VerifyResult(pages);
    }

    private static string BaseName(string type) => type.Split('<')[0].Split('.')[^1];

    private static async Task<IReadOnlyList<VerifiedMember>> MembersAsync(IPage page, CodeClass code, IReadOnlySet<string> own)
    {
        var byName = code.Members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        var found = new List<VerifiedMember>();
        foreach (var member in code.Members.Where(m => m.Locator is not null))
        {
            var lookup = own.Contains(BaseName(member.Type)) ? null : Lookup(page, member, byName, 0);
            if (lookup is null)
            {
                found.Add(new VerifiedMember(member.Name, member.Locator!, -1, MemberVerdict.Unreadable));
                continue;
            }
            int count;
            try
            {
                count = await lookup.CountAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                count = 0; // a selector the page cannot run: nothing it would find
            }
            found.Add(new VerifiedMember(member.Name, member.Locator!, count, count switch { 1 => MemberVerdict.Once, 0 => MemberVerdict.NotFound, _ => MemberVerdict.Several }));
        }
        return found;
    }

    // The member's element in the page: on the page, or inside the frame its scope names.
    private static ILocator? Lookup(IPage page, CodeMember member, IReadOnlyDictionary<string, CodeMember> byName, int depth)
    {
        if (depth > 5 || Parse(member.Locator) is not { } locator)
            return null;
        if (member.Scope is null or "this")
            return locator(page.Locator("*"));
        if (!byName.TryGetValue(member.Scope, out var frame) || !frame.Type.StartsWith("FrameScope<", StringComparison.Ordinal))
            return null;
        var host = Lookup(page, frame, byName, depth + 1);
        return host is null ? null : locator(host.First.ContentFrame.Locator("*"));
    }

    /// <summary>
    /// A locator as written in C# (<c>Locator.ByCss("…")</c>, <c>Locator.ByDataTestId("…")</c>, a plain CSS
    /// string) as a lookup under a root; null when it is not one of those. The same mapping as
    /// <c>Bruinet.Html.Playwright.LocatorExtensions</c>.
    /// </summary>
    public static Func<ILocator, ILocator>? Parse(string? text)
    {
        if (text is null)
            return null;
        var expression = SyntaxFactory.ParseExpression(text);
        if (expression is LiteralExpressionSyntax { Token.Value: string css })
            return root => root.Locator(css);
        if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call
            || access.Expression.ToString() is not ("Locator" or "Bruinet.Core.Locators.Locator")
            || call.ArgumentList.Arguments.Count != 1
            || call.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax { Token.Value: string value })
            return null;
        return access.Name.Identifier.Text switch
        {
            "ByCss" or "ByTagName" or "ByControlType" => root => root.Locator(value),
            "ById" => root => root.Locator($"#{EscapeCss(value)}"),
            "ByDataTestId" => root => root.Locator($"[data-testid='{Escape(value)}']"),
            "ByDataAutomationId" => root => root.Locator($"[data-automation-id='{Escape(value)}']"),
            "ByAutomationId" => root => root.Locator($"[data-automation-id='{Escape(value)}'], [id='{Escape(value)}']"),
            "ByName" => root => root.Locator($"[name='{Escape(value)}']"),
            "ByClassName" => root => root.Locator($".{EscapeCss(value)}"),
            "ByXPath" => root => root.Locator($"xpath={value}"),
            "ByText" => root => root.GetByText(value),
            "ByLinkText" or "ByPartialLinkText" => root => root.Locator($"a:has-text('{Escape(value)}')"),
            _ => null,
        };
    }

    private static string Escape(string value) => value.Replace("'", "\\'", StringComparison.Ordinal);

    private static string EscapeCss(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(".", "\\.", StringComparison.Ordinal).Replace("#", "\\#", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);
}

/// <summary>How a member was found.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MemberVerdict>))]
public enum MemberVerdict
{
    /// <summary>Exactly one element.</summary>
    Once,

    /// <summary>More than one: the locator does not say which.</summary>
    Several,

    /// <summary>None.</summary>
    NotFound,

    /// <summary>A locator Verify cannot read from the source (built at run time).</summary>
    Unreadable,
}

/// <summary>A member: its name, its locator as written, how many elements it found (-1: not looked up), and the verdict.</summary>
public sealed record VerifiedMember(string Name, string Locator, int Count, MemberVerdict Verdict);

/// <summary>A page object checked on its page: the page key, the class, where it was opened, its members, and why it could not be checked.</summary>
public sealed record VerifiedPage(string Key, string ClassName, string? Location, IReadOnlyList<VerifiedMember> Members, string? Problem);

/// <summary>A verify run.</summary>
public sealed record VerifyResult(IReadOnlyList<VerifiedPage> Pages)
{
    /// <summary>True when every page was checked and every member found once (a member Verify cannot read does not fail it).</summary>
    public bool Passed => Pages.All(p => p.Problem is null && p.Members.All(m => m.Verdict is MemberVerdict.Once or MemberVerdict.Unreadable));
}
