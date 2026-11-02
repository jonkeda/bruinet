using Bruinet.Core.Recording;
using Bruinet.Html.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace Bruinet.Recording.Tests;

/// <summary>Runs where Playwright's Chromium is installed (<c>playwright.ps1 install chromium</c>).</summary>
public sealed class ChromiumFactAttribute : FactAttribute
{
    public ChromiumFactAttribute()
    {
        var browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        if (!Directory.Exists(browsers) || !Directory.EnumerateDirectories(browsers, "chromium*").Any())
        {
            Skip = "Playwright's Chromium is not installed: pwsh bin\\Debug\\<tfm>\\playwright.ps1 install chromium.";
        }
    }
}

public sealed class PlaywrightRecorderTests
{
    [ChromiumFact]
    public async Task A_headless_page_records_through_the_screencast()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 641, Height = 401 } });
        await page.SetContentAsync("<body style='margin:0;background:#c00'><h1 id='t'>0</h1><script>let n=0;setInterval(()=>document.getElementById('t').textContent=++n,50)</script></body>");
        var sink = new CountingSink();
        var recorder = new PlaywrightRecorder((_, size, _) => { Assert.Equal(new System.Drawing.Size(640, 400), size); return sink; });
        var target = new RecordingTarget.BrowserPage(page);

        Assert.True(recorder.CanRecord(target));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await using var recording = await recorder.StartAsync(target, new RecordingOptions("page.mp4", FramesPerSecond: 20));
        await Task.Delay(1000);
        var result = await recording.StopAsync();
        clock.Stop();

        // Frames are written at the rate until the stop, so a slow start or stop (a busy runner) adds its time.
        Assert.InRange(sink.Frames, 15, (int)Math.Ceiling(20 * clock.Elapsed.TotalSeconds) + 2);
        Assert.Equal([0xFF, 0xD8], sink.Last.Take(2));
        Assert.Equal(result.StartedAt, recording.StartedAt);
        Assert.Contains(result.Warnings, warning => warning.Contains("first frame arrived", StringComparison.Ordinal));
    }

    [Fact]
    public void A_window_is_not_a_page()
    {
        Assert.False(new PlaywrightRecorder().CanRecord(new RecordingTarget.Window(1)));
    }
}
