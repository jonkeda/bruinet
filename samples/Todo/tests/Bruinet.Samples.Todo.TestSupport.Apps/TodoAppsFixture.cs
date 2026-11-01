using Bruinet.Core.Testing;
using Bruinet.Maui.Enums;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.Contracts;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.TestSupport;
using Bruinet.Samples.Todo.UITests;
using Bruinet.Samples.Todo.Web.UITests;
using Xunit;
using System.Globalization;

namespace Bruinet.Samples.Todo.TestSupport.Apps;

/// <summary>
/// Several Todo apps, of any head, on one WireMock backend and one test set: the apps of
/// <c>apps.md</c>, each started the first time a test asks for it (<c>.docs/contracts/uat-apps-host.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each app gets its own seeded database, its <c>ClientId</c> as <c>X-Todo-Client</c>, and its
/// clock at the test set's now plus its <c>ClockOffsetMinutes</c>. The first two Windows apps
/// are placed side by side, left and right.
/// </para>
/// <para>
/// Tests share the apps and the backend. Each test starts with <see cref="StartTestAsync"/>,
/// which puts the backend back to the test set and every started app back on its list, online.
/// The apps keep their local data, so a test works on todos of its own (<see cref="UniqueTitle"/>).
/// </para>
/// </remarks>
public class TodoAppsFixture : IAsyncLifetime
{
    /// <summary>The test set every app starts on.</summary>
    public const string TestDataName = "one-todo";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMilliseconds(TestConstants.PageTimeoutMs);

    private MockApiServer? _backend;
    private TestAppSet? _apps;
    private int _windows;

    /// <summary>The test set, as seeded.</summary>
    public TodoTestSet TestSet { get; private set; } = null!;

    /// <summary>The WireMock server every app talks to.</summary>
    public MockApiServer Backend => _backend ?? throw new InvalidOperationException("The fixture is not started.");

    /// <summary>The fake API's state.</summary>
    public TodoServerStub Server { get; private set; } = null!;

    /// <summary>The apps of <c>apps.md</c>.</summary>
    public TestAppSet Apps => _apps ?? throw new InvalidOperationException("The fixture is not started.");

    /// <summary>The file the apps come from, next to the test assembly.</summary>
    public static string AppsFile => Path.Combine(AppContext.BaseDirectory, "apps.md");

    /// <summary>The MAUI app called <paramref name="nameOrUser"/>, on Windows or Android.</summary>
    /// <param name="nameOrUser">An app's name, or a user with one app.</param>
    public TodoMauiDevice Maui(string nameOrUser) => Apps.Get<TodoMauiDevice>(nameOrUser);

    /// <summary>The web app called <paramref name="nameOrUser"/>.</summary>
    /// <param name="nameOrUser">An app's name, or a user with one app.</param>
    public TodoWebDevice Web(string nameOrUser) => Apps.Get<TodoWebDevice>(nameOrUser);

    /// <inheritdoc />
    public virtual Task InitializeAsync()
    {
        TestSet = TodoTestSet.Load(TestDataName);
        _backend = MockApiServer.Start();
        Server = TestSet.StubServer(_backend);
        _apps = new TestAppSet(TestAppDefinition.ReadMarkdownFile(AppsFile), StartAppAsync);
        return Task.CompletedTask;
    }

    /// <summary>The backend back to the test set, and every started app online and on its list.</summary>
    public async Task StartTestAsync()
    {
        ResetBackend();
        await Apps.ResetStartedAsync();
    }

    /// <summary>
    /// Runs a test's body, and when it fails, captures every started app before the failure is
    /// reported: the cause is often on another app's screen than the one whose step failed.
    /// </summary>
    /// <remarks>
    /// Explicit rather than through <see cref="ScreenshotTestAttribute.SetApps"/>: the fixture is
    /// set up in another async flow than the test runs in, so an ambient registration made here
    /// would not reach the test.
    /// </remarks>
    /// <param name="testClass">The test class, for the file names.</param>
    /// <param name="testName">The test, for the file names.</param>
    /// <param name="body">The test.</param>
    public void Run(string testClass, string testName, Action body)
    {
        try
        {
            body();
        }
        catch (Exception exception)
        {
            var captures = Apps.CaptureAll(testClass, testName, "failure");
            throw new AppsTestFailure(exception, captures);
        }
    }

    /// <summary>The fake server back to the test set's server half, with nothing arranged on it.</summary>
    public void ResetBackend()
    {
        Backend.Reset();
        Server = TestSet.StubServer(Backend);
    }

    /// <summary>A title no other test uses: the apps keep their local data between tests.</summary>
    /// <param name="title">The title's start.</param>
    public static string UniqueTitle(string title) => $"{title} {Guid.NewGuid().ToString("N")[..6]}";

    /// <summary>Waits until the backend has received a PUT of the todo <paramref name="title"/> from <paramref name="app"/>.</summary>
    /// <param name="app">The app that should have sent it.</param>
    /// <param name="title">The todo's title, as sent.</param>
    public void WaitForTodoFrom(ITestApp app, string title)
    {
        var clientId = ClientIdOf(app.Definition);
        Backend.WaitForRequest(
            "PUT",
            TodoApi.TodosPath + "/*",
            request => request.BodyAs<TodoDto>(TodoApi.JsonOptions)?.Title == title
                && request.Headers.TryGetValue(TodoApi.ClientHeader, out var sender)
                && sender == clientId,
            RequestTimeout);
    }

    /// <summary>What <paramref name="app"/> sends in <c>X-Todo-Client</c>: its <c>ClientId</c> column, or one made from its name.</summary>
    /// <param name="app">The app.</param>
    public static string ClientIdOf(TestAppDefinition app)
        => app["ClientId"] ?? $"{app.Head ?? "app"}-{string.Concat(app.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant()}";

    /// <inheritdoc />
    public virtual async Task DisposeAsync()
    {
        if (_apps is not null)
        {
            await _apps.DisposeAsync();
        }

        _backend?.Dispose();
    }

    private async Task<ITestApp> StartAppAsync(TestAppDefinition app, CancellationToken cancellationToken)
    {
        LaunchSettings Adjust(LaunchSettings settings)
            => app["ClockOffsetMinutes"] is { } minutes
                ? settings with { FixedNow = settings.FixedNow?.AddMinutes(int.Parse(minutes, CultureInfo.InvariantCulture)) }
                : settings;

        switch (app.Head)
        {
            case "web":
                var web = await TodoWebDevice.StartAsync(FolderName(app), TestSet, Backend.BaseUrl, ClientIdOf(app), Adjust, app);
                await web.ResetAsync(cancellationToken);
                return web;

            case "maui":
                var placement = TodoMauiDevice.PlatformOf(app) == MauiPlatform.Windows
                    ? _windows++ switch
                    {
                        0 => "left-half",
                        1 => "right-half",
                        _ => null,
                    }
                    : null;
                return TodoMauiDevice.Start(app, TestSet, Backend.BaseUrl, ClientIdOf(app), placement, Adjust);

            default:
                throw new NotSupportedException($"{app.Name} has head '{app.Head}'. The Todo apps are 'maui' or 'web'.");
        }
    }

    private static string FolderName(TestAppDefinition app)
        => string.Concat(app.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}

/// <summary>A test's failure, with where each started app's screenshot went.</summary>
public sealed class AppsTestFailure(Exception failure, IReadOnlyList<TestAppCapture> captures)
    : Exception(Describe(failure, captures), failure)
{
    /// <summary>One screenshot per started app.</summary>
    public IReadOnlyList<TestAppCapture> Captures { get; } = captures;

    private static string Describe(Exception failure, IReadOnlyList<TestAppCapture> captures)
        => failure.Message + Environment.NewLine + "Screenshots:" + string.Concat(captures.Select(capture =>
            $"{Environment.NewLine}- {capture.App}: {capture.Path ?? "not captured: " + capture.Error}"));
}
