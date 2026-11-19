using System.Text.RegularExpressions;
using Bruinet.Scraper.Code;

namespace Bruinet.Scraper.Factory;

/// <summary>Why the factory did not keep something, as a thing that can be counted: the same code three times is one fault of the process.</summary>
/// <param name="Code">What kind of reason: one of <see cref="ReasonCodes"/>.</param>
/// <param name="Station">The station it came from: walk, draft, build, test.</param>
/// <param name="Place">The place it is about; empty when it is about the loop.</param>
/// <param name="Class">The class it is about; empty when none.</param>
/// <param name="Evidence">What was said: the compiler's message, the test's, the walk's.</param>
/// <param name="FixAt">Where a fix belongs: one of <see cref="FixPlaces"/>.</param>
public sealed record Reason(string Code, string Station, string Place, string Class, string Evidence, string FixAt);

/// <summary>The codes a reason has.</summary>
public static class ReasonCodes
{
    /// <summary>A drafted class or test does not build.</summary>
    public const string DoesNotBuild = "does-not-build";

    /// <summary>A place's page has no class, so what opens on it cannot be drafted.</summary>
    public const string NoPageClass = "no-page-class";

    /// <summary>A page of one record cannot be opened by its address: it needs a record and the way from its list.</summary>
    public const string NeedsRecord = "needs-record";

    /// <summary>A test did not find a label its class declares on the page.</summary>
    public const string LabelNotFound = "label-not-found";

    /// <summary>A test's page or part did not open: nothing to press, or the press opened nothing.</summary>
    public const string DidNotOpen = "did-not-open";

    /// <summary>A test waited longer than it may.</summary>
    public const string Timeout = "timeout";

    /// <summary>The app refused the page to the account.</summary>
    public const string Refused = "refused";

    /// <summary>A test failed, then passed when it ran again: a wait is missing.</summary>
    public const string PassedSecondTime = "passed-second-time";

    /// <summary>The project has no templates, or a template the draft needs is not there.</summary>
    public const string NoTemplate = "no-template";

    /// <summary>The saved sign-in ran out and could not be renewed.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>The suite does not build without what the loop wrote.</summary>
    public const string SuiteDoesNotBuild = "suite-does-not-build";

    /// <summary>The loop took longer than it may.</summary>
    public const string LoopTooLong = "loop-too-long";

    /// <summary>A place shows nothing a test could check: it is listed, and no class is drafted for it.</summary>
    public const string NothingToCheck = "nothing-to-check";

    /// <summary>A test that passed before the loop does not pass after it: what the loop wrote is put back.</summary>
    public const string BrokeAPassingTest = "broke-a-passing-test";

    /// <summary>No rule fits: the evidence says what it was.</summary>
    public const string Unknown = "unknown";
}

/// <summary>Where a fix belongs.</summary>
public static class FixPlaces
{
    /// <summary>The app's signs, holders and names.</summary>
    public const string Profile = "profile";

    /// <summary>The suite's draft templates.</summary>
    public const string Templates = "templates";

    /// <summary>The Scraper's reading of a page.</summary>
    public const string Reader = "reader";

    /// <summary>A draft kind the factory does not have yet.</summary>
    public const string DraftKind = "draft kind";

    /// <summary>A record the factory has to make first.</summary>
    public const string Records = "records";

    /// <summary>The environment: it was slow, or the sign-in ran out.</summary>
    public const string Environment = "environment";

    /// <summary>A person: something only they decide or can look at.</summary>
    public const string Person = "person";
}

/// <summary>Says what kind of reason a compiler's message, a test's failure or a refused draft is, by rules.</summary>
public static partial class ReasonClassifier
{
    /// <summary>A drafted file that does not build.</summary>
    public static Reason OfBuild(CodeProblem problem, string place, string className)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new Reason(ReasonCodes.DoesNotBuild, "build", place, className, $"{problem.Code}: {Short(problem.Message)}", FixPlaces.Templates);
    }

    /// <summary>A place that could not be drafted, from what the drafter said.</summary>
    public static Reason OfDraft(string message, string place)
    {
        ArgumentNullException.ThrowIfNull(message);
        var (code, fix) = message switch
        {
            _ when message.StartsWith("No page class", StringComparison.Ordinal) => (ReasonCodes.NoPageClass, FixPlaces.DraftKind),
            _ when message.Contains("draft-templates", StringComparison.OrdinalIgnoreCase) || message.Contains("template", StringComparison.OrdinalIgnoreCase) => (ReasonCodes.NoTemplate, FixPlaces.Templates),
            _ => (ReasonCodes.Unknown, FixPlaces.Person),
        };
        return new Reason(code, "draft", place, "", Short(message), fix);
    }

    /// <summary>A test that failed, from its message.</summary>
    /// <param name="message">The failure's message.</param>
    /// <param name="place">The place the test is of.</param>
    /// <param name="className">The test's class.</param>
    /// <param name="record">True when the place is the page of one record.</param>
    public static Reason OfTest(string message, string place, string className, bool record = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        var (code, fix) = message switch
        {
            _ when record => (ReasonCodes.NeedsRecord, FixPlaces.Records),
            _ when NotShown().IsMatch(message) => (ReasonCodes.LabelNotFound, FixPlaces.Reader),
            _ when DidNotOpen().IsMatch(message) => (ReasonCodes.DidNotOpen, FixPlaces.DraftKind),
            _ when message.Contains("access", StringComparison.OrdinalIgnoreCase) && message.Contains("denied", StringComparison.OrdinalIgnoreCase) => (ReasonCodes.Refused, FixPlaces.Person),
            _ when message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) || message.Contains("timed out", StringComparison.OrdinalIgnoreCase) => (ReasonCodes.Timeout, FixPlaces.Environment),
            _ => (ReasonCodes.Unknown, FixPlaces.Person),
        };
        return new Reason(code, "test", place, className, Short(message), fix);
    }

    private static string Short(string text)
    {
        var one = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return one.Length > 240 ? one[..240] + "…" : one;
    }

    [GeneratedRegex(@"does not show|not (found|shown) on|no (field|row|label) |labels? .* (missing|not)", RegexOptions.IgnoreCase)]
    private static partial Regex NotShown();

    [GeneratedRegex(@"did not open|is not (loaded|at its address|open)|could not be opened|not visible", RegexOptions.IgnoreCase)]
    private static partial Regex DidNotOpen();
}
