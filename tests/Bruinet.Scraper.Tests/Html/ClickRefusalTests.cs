using Bruinet.Scraper.Html;

namespace Bruinet.Scraper.Tests.Html;

public sealed class ClickRefusalTests
{
    [Fact]
    public void Describe_WithACoveringElementInTheCallLog_NamesIt()
    {
        const string message = "Timeout 3000ms exceeded.\nCall log:\n  - waiting for element to be visible, enabled and stable\n  - element is visible, enabled and stable\n  - <div role=\"dialog\" class=\"v-dialog__content\">...</div> intercepts pointer events\n  - retrying click action";

        var described = ClickRefusal.Describe(message);

        Assert.Equal("Timeout 3000ms exceeded. (<div role=\"dialog\" class=\"v-dialog__content\">...</div> intercepts pointer events)", described);
    }

    [Fact]
    public void Describe_WithoutAReasonInTheCallLog_KeepsTheFirstLine()
    {
        Assert.Equal("Timeout 3000ms exceeded.", ClickRefusal.Describe("Timeout 3000ms exceeded.\nCall log:\n  - waiting for locator"));
        Assert.Equal("The element is gone.", ClickRefusal.Describe("The element is gone."));
    }

    [Theory]
    [InlineData("https://app.example/contact#/", "https://app.example/contact#/", true)]
    [InlineData("https://app.example/contact#/create", "https://app.example/contact#/", true)]
    [InlineData("https://app.example/contact?x=1#/", "https://app.example/contact?x=1#/a", true)]
    [InlineData("https://app.example/contact#/", "https://app.example/contact", false)]
    [InlineData("https://app.example/contact", "https://app.example/contact", false)]
    [InlineData("https://app.example/contact#/", "https://app.example/project#/", false)]
    [InlineData("about:blank", "https://app.example/contact#/", false)]
    public void StaysInDocument_OnlyForTheSameAddressWithAHashRoute(string current, string target, bool expected)
        => Assert.Equal(expected, ClickRefusal.StaysInDocument(current, target));
}
