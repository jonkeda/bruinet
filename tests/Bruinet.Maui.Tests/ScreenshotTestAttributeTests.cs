using Bruinet.Core.Configuration;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Testing;
using Bruinet.Maui.Testing;

namespace Bruinet.Maui.Tests;

/// <summary>[ScreenshotTest] lives in Bruinet.Maui, the stack that uses it, so Core takes no test framework (AD-021).</summary>
public sealed class ScreenshotTestAttributeTests
{
    private const string AppsMarkdown = """
        # Apps

        ## Apps

        | Name | User | Head | Platform |
        | --- | --- | --- | --- |
        | Daan's PC | Daan | maui | Windows |
        | Eva | | web | |
        """;

    [Fact]
    public async Task AFailedTest_WithAppsRegistered_CapturesEveryStartedApp()
    {
        await using var set = new TestAppSet(
            TestAppDefinition.ReadMarkdown(AppsMarkdown),
            (definition, _) => Task.FromResult<ITestApp>(new FakeApp(definition)));
        set.Get("Eva");
        set.Get("Daan's PC");
        ScreenshotTestAttribute.SetApps(set);
        try
        {
            var captured = ScreenshotTestAttribute.CaptureIfFailed(new InvalidOperationException("boom"));

            Assert.Contains("Eva/Unknown-Unknown-failure.png", captured);
            Assert.Contains("Daan's PC/Unknown-Unknown-failure.png", captured);
        }
        finally
        {
            ScreenshotTestAttribute.SetApps(null);
        }
    }

    private sealed class FakeApp(TestAppDefinition definition) : ITestApp
    {
        public TestAppDefinition Definition => definition;

        public IScreenshotService Screenshots { get; } = new FakeScreenshots(definition.Name);

        public ValueTask ResetAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeScreenshots(string app) : IScreenshotService
    {
        public ScreenshotSettings Settings { get; } = new();

        public string Capture(string? description = null) => throw new NotSupportedException();

        public string Capture(string testClass, string testMethod, string description) => $"{app}/{testClass}-{testMethod}-{description}.png";

        public string CaptureOnFailure(string testClass, string testMethod, Exception exception) => throw new NotSupportedException();
    }
}
