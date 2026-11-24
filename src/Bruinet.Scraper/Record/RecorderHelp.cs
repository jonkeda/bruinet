namespace Bruinet.Scraper.Record;

/// <summary>What the Recorder's column says a person can tell the AI, for the state the Recorder is in.</summary>
/// <param name="Text">The sentence.</param>
/// <param name="ForTheChat">True when it is a sentence to paste into the chat; false when it only says what happens.</param>
public sealed record RecorderHelp(string Text, bool ForTheChat)
{
    /// <summary>The help for a state.</summary>
    /// <param name="folder">The Scraper project; null when none is chosen.</param>
    /// <param name="address">Where the page is.</param>
    /// <param name="recording">A recording is on.</param>
    /// <param name="session">The last session written; null when none was.</param>
    /// <param name="pages">The pages in it.</param>
    /// <param name="chatWaits">A chat is waiting for the recording to end.</param>
    public static RecorderHelp For(string? folder, string? address, bool recording, string? session, int pages, bool chatWaits)
    {
        if (recording)
            return new RecorderHelp(chatWaits ? "Press Stop when you have seen the pages you want. The AI goes on then." : "Press Stop when you have seen the pages you want.", ForTheChat: false);
        if (string.IsNullOrWhiteSpace(folder))
            return new RecorderHelp($"Set up a Scraper project for {(string.IsNullOrWhiteSpace(address) ? "<the site's address>" : address)}", ForTheChat: true);
        if (session is null)
            return new RecorderHelp($"Open the recorder on {folder} and wait until I stop recording", ForTheChat: true);
        if (chatWaits)
            return new RecorderHelp($"The AI has been told. It goes on with {pages} {(pages == 1 ? "page" : "pages")}.", ForTheChat: false);
        return new RecorderHelp($"Read the recording {session} in {folder} and go on with its pages: seed the places, draft the page objects, build", ForTheChat: true);
    }
}
