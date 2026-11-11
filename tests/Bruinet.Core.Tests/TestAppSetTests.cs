using Bruinet.Core.Configuration;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

public sealed class TestAppSetTests
{
    private const string AppsMarkdown = """
        # Apps

        ## Apps

        | Name | User | Head | Platform |
        | --- | --- | --- | --- |
        | Daan's PC | Daan | maui | Windows |
        | Daan's phone | Daan | maui | Android |
        | Eva | | web | |
        """;

    [Fact]
    public void ReadMarkdown_ReadsTheAppsTable_UserDefaultingToTheName()
    {
        var apps = TestAppDefinition.ReadMarkdown(AppsMarkdown);

        Assert.Equal(["Daan's PC", "Daan's phone", "Eva"], apps.Select(app => app.Name));
        Assert.Equal("Eva", apps[2].User);
        Assert.Equal("web", apps[2].Head);
        Assert.Equal("Android", apps[1]["Platform"]);
        Assert.Null(apps[2]["Platform"]);
    }

    [Fact]
    public async Task AnApp_StartsOnFirstUse_Once()
    {
        var log = new List<string>();
        await using var set = CreateSet(log);

        Assert.Empty(set.Started);
        var first = set.Get("Eva");
        var second = set.Get("Eva");

        Assert.Same(first, second);
        Assert.Equal(["start Eva"], log);
    }

    [Fact]
    public async Task AUser_WithOneApp_FindsIt()
    {
        await using var set = CreateSet([]);

        Assert.Equal("Eva", set.Resolve("Eva").Name);
    }

    [Fact]
    public async Task AUser_WithSeveralApps_NeedsTheAppNamed()
    {
        await using var set = CreateSet([]);

        var error = Assert.Throws<InvalidOperationException>(() => set.Resolve("Daan"));

        Assert.Contains("Daan has several apps (Daan's PC, Daan's phone)", error.Message);
        Assert.Equal("Daan's phone", set.Resolve("Daan", lastUsed: ["Eva", "Daan's phone"]).Name);
    }

    [Fact]
    public async Task AnUnknownName_FailsListingTheApps()
    {
        await using var set = CreateSet([]);

        var error = Assert.Throws<InvalidOperationException>(() => set.Get("Dan"));

        Assert.Contains("No app or user 'Dan'. Apps: Daan's PC (Daan), Daan's phone (Daan), Eva.", error.Message);
    }

    [Fact]
    public async Task GetOfTheWrongKind_NamesTheAppsHead()
    {
        await using var set = CreateSet([]);

        var error = Assert.Throws<InvalidOperationException>(() => set.Get<OtherApp>("Eva"));

        Assert.Contains("Eva is a web-head app (FakeApp), not a OtherApp", error.Message);
    }

    [Fact]
    public async Task ResetStarted_ResetsOnlyTheAppsThatStarted()
    {
        var log = new List<string>();
        await using var set = CreateSet(log);
        set.Get("Eva");

        await set.ResetStartedAsync();

        Assert.Equal(["start Eva", "reset Eva"], log);
    }

    [Fact]
    public async Task StopAll_StopsTheLastStartedFirst_AndTheyCanStartAgain()
    {
        var log = new List<string>();
        var set = CreateSet(log);
        set.Get("Eva");
        set.Get("Daan's PC");

        await set.StopAllAsync();
        set.Get("Eva");

        Assert.Equal(["start Eva", "start Daan's PC", "stop Daan's PC", "stop Eva", "start Eva"], log);
        await set.DisposeAsync();
    }

    [Fact]
    public async Task CaptureAll_CapturesEveryStartedApp_AndSaysWhichCouldNot()
    {
        await using var set = CreateSet([], failCapture: "Daan's PC");
        set.Get("Eva");
        set.Get("Daan's PC");

        var captures = set.CaptureAll("Tests", "Sync", "failure");

        Assert.Equal("Eva/Tests-Sync-failure.png", captures[0].Path);
        Assert.Null(captures[1].Path);
        Assert.Equal("no window", captures[1].Error);
    }

    [Fact]
    public void TwoAppsWithOneName_AreRefused()
    {
        var apps = TestAppDefinition.ReadMarkdown(AppsMarkdown).Append(TestAppDefinition.FromCells(new Dictionary<string, string> { ["Name"] = "eva" }));

        Assert.Throws<ArgumentException>(() => new TestAppSet(apps, (_, _) => throw new InvalidOperationException()));
    }

    private static TestAppSet CreateSet(List<string> log, string? failCapture = null)
        => new(
            TestAppDefinition.ReadMarkdown(AppsMarkdown),
            (definition, _) =>
            {
                log.Add($"start {definition.Name}");
                return Task.FromResult<ITestApp>(new FakeApp(definition, log, definition.Name == failCapture));
            });

    private sealed class FakeApp(TestAppDefinition definition, List<string> log, bool failCapture) : ITestApp
    {
        public TestAppDefinition Definition => definition;

        public IScreenshotService Screenshots { get; } = new FakeScreenshots(definition.Name, failCapture);

        public ValueTask ResetAsync(CancellationToken cancellationToken = default)
        {
            log.Add($"reset {definition.Name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            log.Add($"stop {definition.Name}");
            return ValueTask.CompletedTask;
        }
    }

    private abstract class OtherApp : ITestApp
    {
        public abstract TestAppDefinition Definition { get; }

        public abstract IScreenshotService Screenshots { get; }

        public abstract ValueTask ResetAsync(CancellationToken cancellationToken = default);

        public abstract ValueTask DisposeAsync();
    }

    private sealed class FakeScreenshots(string app, bool fail) : IScreenshotService
    {
        public ScreenshotSettings Settings { get; } = new();

        public string Capture(string? description = null) => throw new NotSupportedException();

        public string Capture(string testClass, string testMethod, string description)
            => fail ? throw new InvalidOperationException("no window") : $"{app}/{testClass}-{testMethod}-{description}.png";

        public string CaptureOnFailure(string testClass, string testMethod, Exception exception) => throw new NotSupportedException();
    }
}
