using Bruinet.Html.Testing;
using Bruinet.Core.Testing;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Pages;

/// <summary>Controls declared on a <c>FrameScope</c> are found in the frame, not on the page around it (?A6).</summary>
public sealed class FramesPageTests : BlazorSampleTestBase
{
    [BrowserFact("an iframe is its own document, which only a browser loads")]
    public void AControlInTheFrame_IsFoundInTheFrame_AndActsThere()
    {
        NavigateToPage("/frames");
        var page = new FramesPage(Context);

        page.InnerStatus.AssertText("Not clicked");
        page.InnerButton.Click();

        page.InnerStatus.AssertText("Clicked in the frame");
        page.OuterStatus.AssertText("Outer page status");
    }
}
