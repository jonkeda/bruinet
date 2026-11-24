using Bruinet.Scraper.Record;

namespace Bruinet.Scraper.Tests.Record;

public sealed class RecorderHelpTests
{
    [Fact]
    public void Stopped_with_no_chat_waiting_the_sentence_tells_the_AI_to_go_on_with_the_recording()
    {
        var help = RecorderHelp.For("C:\\site\\Site.Scraper", "https://app.example.com/", recording: false, "2026-11-24-record", pages: 4, chatWaits: false);

        Assert.True(help.ForTheChat);
        Assert.Equal("Read the recording 2026-11-24-record in C:\\site\\Site.Scraper and go on with its pages: seed the places, draft the page objects, build", help.Text);
    }

    [Theory]
    [InlineData(null, false, null, false, true, "Set up a Scraper project for https://app.example.com/")]
    [InlineData("C:\\site", false, null, false, true, "Open the recorder on C:\\site and wait until I stop recording")]
    [InlineData("C:\\site", true, "s", false, false, "Press Stop when you have seen the pages you want.")]
    [InlineData("C:\\site", true, "s", true, false, "Press Stop when you have seen the pages you want. The AI goes on then.")]
    [InlineData("C:\\site", false, "s", true, false, "The AI has been told. It goes on with 4 pages.")]
    public void Each_state_has_its_sentence_and_only_one_for_the_chat_can_be_copied(string? folder, bool recording, string? session, bool chatWaits, bool forTheChat, string text)
    {
        var help = RecorderHelp.For(folder, "https://app.example.com/", recording, session, pages: 4, chatWaits);

        Assert.Equal((text, forTheChat), (help.Text, help.ForTheChat));
    }
}
