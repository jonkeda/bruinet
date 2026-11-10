using Bruinet.Core.Testing;
using Bruinet.Html.Testing;

namespace Bruinet.Html.Tests;

public sealed class BrowserFactAttributeTests
{
    // Run with BRUINET_HEAD=bunit, this test shows as skipped with its reason; without, it runs.
    [BrowserFact("it checks that the attribute skips on the bUnit head")]
    public void BrowserFact_RunsOnlyOnTheBrowserHead()
    {
        Assert.False(WebHeads.IsBunit);
        Assert.Null(new BrowserFactAttribute().Skip);
    }
}
