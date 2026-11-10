using Bruinet.Core.Testing;
using Xunit;

namespace Bruinet.Html.Testing;

/// <summary>
/// A test that needs a browser (layout, style sheets, script, pixels): skipped on the bUnit head with the
/// reason, so the run still lists it (AD-030). The scenario's counterpart is <c>@browser</c>.
/// </summary>
public sealed class BrowserFactAttribute : FactAttribute
{
    /// <param name="reason">What the test needs the browser for, shown in the skip.</param>
    public BrowserFactAttribute(string reason = "it needs a browser")
    {
        if (WebHeads.IsBunit)
        {
            Skip = $"Browser only: {reason}; this run uses the bUnit head ({WebHeads.Variable}=bunit).";
        }
    }
}
