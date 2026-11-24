using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Record;

namespace Bruinet.Scraper.Tests.Record;

public sealed class RecorderLinkTests
{
    [Fact]
    public async Task A_request_is_answered_over_the_pipe_and_a_waiting_one_does_not_hold_up_the_next()
    {
        var pipe = "bruinet-recorder-test-" + Guid.NewGuid().ToString("N");
        using var stop = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serving = RecorderLink.ServeAsync(async (request, _) =>
        {
            if (request.Do == "wait")
                await release.Task;
            return new RecorderStatus(Recording: request.Do == "start", request.Folder, request.Url, "2026-11-24-record", 3, 2, Values: false, Ended: request.Do == "wait");
        }, stop.Token, pipe);

        var waiting = RecorderLink.AskAsync(new RecorderRequest("wait", Minutes: 5), pipe: pipe);
        var status = await RecorderLink.AskAsync(new RecorderRequest("start", "C:\\site", "https://app.example.com/"), pipe: pipe);

        Assert.NotNull(status);
        Assert.Equal((true, "C:\\site", "https://app.example.com/", 3, 2), (status.Recording, status.Folder, status.Address, status.Pages, status.Presses));
        Assert.False(waiting.IsCompleted);
        release.SetResult();
        Assert.True((await waiting)!.Ended);

        await stop.CancelAsync();
        await serving;
    }

    [Fact]
    public async Task With_no_recorder_running_there_is_no_answer()
    {
        Assert.Null(await RecorderLink.AskAsync(new RecorderRequest("status"), pipe: "bruinet-recorder-test-" + Guid.NewGuid().ToString("N")));
    }
}

public sealed class RecorderSettingsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-recorder-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void An_opened_address_is_first_in_the_list_once_and_is_read_back_from_the_project()
    {
        var settings = RecorderSettings.Load(_folder)
            .Opened("https://app.example.com/projects")
            .Opened("https://app.example.com/contacts")
            .Opened("https://APP.example.com/projects") with { RecordValues = true };
        settings.Save(_folder);

        var read = RecorderSettings.Load(_folder);
        Assert.Equal(["https://APP.example.com/projects", "https://app.example.com/contacts"], read.Addresses);
        Assert.Equal("https://APP.example.com/projects", read.Address);
        Assert.True(read.RecordValues);
        Assert.True(File.Exists(Path.Combine(_folder, RecorderSettings.FileName)));
    }

    [Fact]
    public void The_list_keeps_twenty_addresses()
    {
        var settings = Enumerable.Range(1, 25).Aggregate(new RecorderSettings(), (all, i) => all.Opened($"https://app.example.com/{i}"));
        Assert.Equal(RecorderSettings.MaxAddresses, settings.Addresses.Count);
        Assert.Equal("https://app.example.com/25", settings.Addresses[0]);
    }

    [Fact]
    public void A_recording_starts_only_in_a_scraper_project_on_one_of_its_hosts()
    {
        Assert.Contains(ScraperConfig.FileName, Recorder.Refusal(_folder, "http://localhost:5000/"), StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://app.example.com/ |\n");
        Assert.Null(Recorder.Refusal(_folder, "http://localhost:5000/page"));
        Assert.Null(Recorder.Refusal(_folder, "https://app.example.com/projects"));
        Assert.Contains("Explore hosts", Recorder.Refusal(_folder, "https://other.example.com/"), StringComparison.Ordinal);
        Assert.NotNull(Recorder.Refusal(_folder, "about:blank"));
    }
}
