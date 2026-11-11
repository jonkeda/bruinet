using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Service;

/// <summary>
/// A test recording saves each action in order on a queue of its own. An action that cannot be saved
/// is reported through <see cref="ScraperService.Changed"/>; it never shortens the recording without a
/// word (review 05, finding 6).
/// </summary>
public sealed class RecordedActionSaveTests : IAsyncDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeSource _source = new();
    private readonly ScraperService _service;
    private readonly List<ScraperChange> _changes = [];

    public RecordedActionSaveTests()
    {
        var folder = Path.Combine(_solution, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(
            Path.Combine(folder, ScraperConfig.FileName),
            "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | https://shop.example/menu |\n");
        _service = new ScraperService(_solution, _ => _source);
        _service.Changed += (_, change) =>
        {
            lock (_changes)
                _changes.Add(change);
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    [Fact]
    public async Task An_Action_IsSaved_AndReported()
    {
        await _service.StartRecordingAsync(null, "orders", autoCapture: false, actions: true);

        _source.Act(Navigate("https://shop.example/orders"));

        var change = await ChangeAsync(ScraperChangeKind.Acted);
        Assert.Equal(ScrapeActionKind.Navigate, change.Captured?.Kind);
    }

    [Fact]
    public async Task An_Action_ThatCannotBeSaved_IsReported_AsFailed()
    {
        var state = await _service.StartRecordingAsync(null, "orders", autoCapture: false, actions: true);
        Directory.Delete(state.Folder, recursive: true);

        _source.Act(Navigate("https://shop.example/orders"));

        var change = await ChangeAsync(ScraperChangeKind.Failed);
        Assert.Contains("navigate", change.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task After_A_Failed_Save_TheNextAction_IsStillSaved()
    {
        var state = await _service.StartRecordingAsync(null, "orders", autoCapture: false, actions: true);
        _source.ShowCountFails = true;

        _source.Act(Navigate("https://shop.example/orders"));
        await ChangeAsync(ScraperChangeKind.Failed);
        _source.ShowCountFails = false;
        _source.Act(Navigate("https://shop.example/customers"));

        await ChangeAsync(ScraperChangeKind.Acted, count: 2);
        Assert.True(Directory.Exists(state.Folder));
    }

    private static ScrapeAction Navigate(string location) =>
        new() { Kind = ScrapeActionKind.Navigate, At = DateTimeOffset.Now, Location = location };

    // The `count`th change of `kind`, once it has been raised.
    private async Task<ScraperChange> ChangeAsync(ScraperChangeKind kind, int count = 1)
    {
        var deadline = DateTime.UtcNow + Limit;
        while (DateTime.UtcNow < deadline)
        {
            lock (_changes)
            {
                var found = _changes.Where(c => c.Kind == kind).ToList();
                if (found.Count >= count)
                    return found[count - 1];
            }

            await Task.Delay(10); // polling the handler's list, at a named cadence
        }

        lock (_changes)
            throw new TimeoutException($"No {kind} change #{count}; saw {string.Join(", ", _changes.Select(c => $"{c.Kind} {c.Message}"))}.");
    }

    /// <summary>A recording browser that raises the actions a test gives it.</summary>
    private sealed class FakeSource : IScrapeSource
    {
        public string Platform => "html";

        public bool ShowCountFails { get; set; }

        public event EventHandler<ScrapeTransition>? Transitioned;

        public event EventHandler<ScrapeAction>? Acted;

        public event EventHandler<string>? Commanded;

        public void Act(ScrapeAction action) => Acted?.Invoke(this, action);

        public Task StartActionsAsync(ScrapeActionOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void StopActions()
        {
        }

        public Task<ScrapeAssert?> PickAssertAsync(CancellationToken cancellationToken = default) => Task.FromResult<ScrapeAssert?>(null);

        public Task ShowCountAsync(int actions, CancellationToken cancellationToken = default)
            => ShowCountFails ? throw new IOException("the recording browser's page is gone") : Task.CompletedTask;

        public Task StartAsync(ScrapeStart start, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ScrapeSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("no capture in these tests");

        public Task<byte[]?> ScreenshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);

        public Task HighlightAsync(int? number, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int?> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

        public ValueTask DisposeAsync()
        {
            _ = Transitioned;
            _ = Commanded;
            return ValueTask.CompletedTask;
        }
    }
}
