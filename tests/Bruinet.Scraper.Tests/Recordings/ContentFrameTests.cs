using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Recordings;

public sealed class ContentFrameTests
{
    private const string Shell = "https://app.example.com/docs/MenuPortal.aspx";

    // A page of 1200 by 900 with one frame of the given size and address.
    private static ScrapeSnapshot Page(string? frame, ScrapeBounds? frameBox, string? frameTitle = "Weekly entry") => new()
    {
        Platform = "html",
        Location = Shell,
        Title = "1 - Hours - A Person",
        Nodes =
        [
            new ScrapeNode { Number = 1, Parent = 0, Type = "html", Bounds = new ScrapeBounds(0, 0, 1200, 900) },
            new ScrapeNode { Number = 2, Parent = 1, Type = "iframe", Bounds = frameBox, Frame = frame, FrameTitle = frameTitle },
        ],
    };

    [Fact]
    public void A_frame_of_the_same_site_that_covers_most_of_the_page_names_the_capture()
    {
        var page = Page("https://app.example.com/docs/ProWeekTransactions.aspx?id=3", new ScrapeBounds(0, 60, 1200, 840));

        Assert.Equal(2, ContentFrame.Of(page)?.Number);
        Assert.Equal("https://app.example.com/docs/ProWeekTransactions.aspx?id=3", ContentFrame.Location(page));
        Assert.Equal("Weekly entry", ContentFrame.Title(page));
    }

    [Fact]
    public void A_frame_without_a_title_leaves_the_page_s_title()
    {
        var page = Page("https://app.example.com/docs/ProWeekTransactions.aspx", new ScrapeBounds(0, 60, 1200, 840), frameTitle: null);

        Assert.Equal("1 - Hours - A Person", ContentFrame.Title(page));
    }

    [Theory]
    // A widget: a payment box, an editor.
    [InlineData("https://app.example.com/frames/payment.html", 300, 200)]
    // Another site's page, however large: its content is not ours to read.
    [InlineData("https://pay.example.org/frame.html", 1200, 840)]
    // A frame that shows nothing has no box at all.
    [InlineData("https://app.example.com/docs/Empty.html", 0, 0)]
    [InlineData("about:blank", 1200, 840)]
    public void A_page_keeps_its_own_name_when_no_frame_is_what_it_shows(string frame, int width, int height)
    {
        var page = Page(frame, width == 0 ? null : new ScrapeBounds(0, 60, width, height));

        Assert.Null(ContentFrame.Of(page));
        Assert.Equal(Shell, ContentFrame.Location(page));
        Assert.Equal("1 - Hours - A Person", ContentFrame.Title(page));
    }
}
