using Bruinet.Core.Composition;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Services;
using Bruinet.Core.Testing;
using Bruinet.Html.Interfaces;
using Bruinet.Samples.Todo.TestSupport.Uat;
using Bruinet.Samples.Todo.Web.Uat.Phrases;
using Bruinet.Samples.Todo.Web.UITests;
using Bruinet.Samples.Todo.Web.UITests.Pages;
using Bruinet.Uat;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Samples.Todo.Web.Uat.Runtime;

/// <summary>
/// The Todo web head, hermetic, presented to the UAT engine as its root: the web counterpart of
/// the MAUI suite's <c>TodoUatFixture</c>, running the same scenario files. It starts on the
/// <c>DefaultTestData</c> of <c>uat.config.md</c>; a scenario file that names another test set
/// switches to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Several apps.</b> The apps of the <c>Apps</c> table are more devices on the same fake backend,
/// each with its own database, host, browser context and <c>X-Todo-Client</c>. A step that names
/// Alice (<c>Alice taps Add</c>) starts Alice's device the first time and makes it current; a
/// scenario starts on the main device. See <c>.docs/contracts/uat-apps-host.md</c>.
/// </para>
/// <para>
/// Each device has its own composition, whose <see cref="IHtmlTestContext"/> is that device's. A
/// restart keeps a device's context on both heads, so a composition is never rebuilt.
/// </para>
/// </remarks>
[TestModuleScan(typeof(TodoListPage), NamespacePrefix = "Bruinet.Samples.Todo.Web.UITests.Pages")]
[TestModuleScan(typeof(AppLifecyclePhrases), NamespacePrefix = "Bruinet.Samples.Todo.Web.Uat.Phrases")]
[TestModuleScan(typeof(BackendPhrases), NamespacePrefix = "Bruinet.Samples.Todo.TestSupport.Uat")]
public sealed class TodoWebUatFixture : TodoWebFixture, IUatAppsHost, IUatScenarioLifecycle
{
    private TestComposition? _composition;
    private TestAppSet? _apps;

    /// <summary>The test set a scenario file runs on when its metadata names none: <c>DefaultTestData</c> in <c>uat.config.md</c>.</summary>
    public static string DefaultTestData { get; } = ReadConfig().Runtime.TryGetValue(BddConfigFields.DefaultTestData, out var name) && name.Length > 0
        ? name
        : throw new InvalidOperationException($"uat.config.md has no '{BddConfigFields.DefaultTestData}' in its Runtime table.");

    /// <inheritdoc />
    protected override string TestDataName => DefaultTestData;

    /// <inheritdoc />
    /// <remarks>Its <c>## Dependencies</c> say how the backend is answered: a stub, or recorded and replayed (AI11b).</remarks>
    protected override BddConfig? SuiteConfig => ReadConfig();

    /// <summary>The page graph and DI scope the UAT runtime resolves controls through: the main device's.</summary>
    public TestComposition Composition => _composition
        ?? throw new InvalidOperationException("The fixture is not started.");

    /// <summary>The devices of the <c>Apps</c> table, each started the first time a scenario uses it.</summary>
    public TestAppSet Apps => _apps
        ?? throw new InvalidOperationException("The fixture is not started.");

    /// <summary>Captures the current app's page when a scenario fails (UAT evidence).</summary>
    public IScreenshotService ScreenshotService => new ScreenshotService(Context, Context.Logger);

    /// <summary>The name <paramref name="user"/>'s app sends in <c>X-Todo-Client</c>.</summary>
    public string ClientIdOfUser(string user)
    {
        var app = Apps.Resolve(user);
        return Apps.Started.OfType<TodoWebDevice>().FirstOrDefault(device => device.Definition.Name == app.Name)?.ClientId
            ?? ClientIdFor(app);
    }

    /// <inheritdoc />
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _composition = ComposeFor(MainDevice);
        _apps = new TestAppSet(
            ReadConfig().Apps.Select(app => TestAppDefinition.FromCells(app.Values)),
            async (app, _) =>
            {
                var device = await StartDeviceAsync(app.Name.ToLowerInvariant(), ClientIdFor(app), app);
                device.OpenList().Filter.SelectByText("All");
                return device;
            });
    }

    /// <inheritdoc />
    public void AddAppServices(ITestApp app, IServiceCollection services)
        => AddContext(services, (TodoWebDevice)app);

    /// <inheritdoc />
    /// <remarks>
    /// The file's <c>TestData</c>, or <see cref="DefaultTestData"/>. Here, not in the test class, so a runner that drives
    /// the fixture without xunit (run_scenario, the preview's Run) starts it the same way.
    /// </remarks>
    public void BeforeFile(BddDocument document)
        => UseTestSet(document.Metadata.TryGetValue(BddMetadataFields.TestData, out var name) && name.Length > 0
            ? name
            : DefaultTestData);

    /// <inheritdoc />
    /// <remarks>From the list, online, with the backend reset, in the scenario's HTTP replay session.</remarks>
    public void BeforeScenario(BddBoundScenario scenario)
    {
        BeginReplay(scenario.Source.Name);
        StartTest();
    }

    /// <inheritdoc />
    /// <remarks>The scenario's HTTP replay session ended: a call it missed fails it.</remarks>
    public void AfterScenario(BddBoundScenario scenario) => EndReplay();

    /// <inheritdoc />
    /// <remarks>The fixture's own members (the network, a restart) act on the app a scenario is using.</remarks>
    public void OnAppUsed(ITestApp app) => Device = (TodoWebDevice)app;

    /// <inheritdoc />
    /// <remarks>Every scenario starts on the main device; the other apps are put online again.</remarks>
    public override TodoListPage StartTest()
    {
        Device = MainDevice;
        foreach (var app in Apps.Started.OfType<TodoWebDevice>())
        {
            app.Network.SetOnline();
        }

        return base.StartTest();
    }

    /// <inheritdoc />
    /// <remarks>The other apps hold the old test set's data, so they are stopped; a scenario starts them again.</remarks>
    public override TodoListPage UseTestSet(string name)
    {
        if (name != TestSet.Name)
        {
            Apps.StopAllAsync().GetAwaiter().GetResult();
        }

        Device = MainDevice;
        return base.UseTestSet(name);
    }

    /// <inheritdoc />
    public override async Task DisposeAsync()
    {
        if (_apps is not null)
        {
            await _apps.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    // The client id an app sends: its ClientId column, or web-<name>.
    private static string ClientIdFor(TestAppDefinition app)
        => app["ClientId"] ?? $"web-{app.Name.ToLowerInvariant()}";

    private TestComposition ComposeFor(TodoWebDevice device)
        => TestComposition.ForFixture(this, services => AddContext(services, device));

    // The device's context, one for its whole life on both heads (a restart keeps it): an instance, so the composition
    // never disposes it.
    private static void AddContext(IServiceCollection services, TodoWebDevice device)
        => services.AddSingleton<IHtmlTestContext>(device.Context);

    // Beside this assembly, wherever the suite runs (AD-025); the source's when a host runs it from there.
    private static BddConfig ReadConfig()
        => BddConfigParser.ParseFile(BddScenarioSource.GetConfigFilePath(BddScenarioSource.SuiteDirectory(Bruinet.Core.Composition.AssemblyFile.FolderOf(typeof(TodoWebUatFixture).Assembly))));
}

/// <summary>Binds every UAT scenario class to the one <see cref="TodoWebUatFixture"/> (one app per run).</summary>
[CollectionDefinition(CollectionName)]
public sealed class TodoWebUatCollection : ICollectionFixture<TodoWebUatFixture>
{
    /// <summary>The collection name shared by the UAT scenario classes.</summary>
    public const string CollectionName = "Todo web UAT";
}
