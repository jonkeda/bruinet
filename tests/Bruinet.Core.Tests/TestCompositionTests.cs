using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Core.Tests;

public sealed class TestCompositionTests
{
    [Fact]
    public void ForFixture_DiscoversPagesAndScenarioServices()
    {
        var fixture = new CompositionFixture();

        Assert.Contains(fixture.Composition.Catalog.Pages, page =>
            page.Name == "Device Settings" &&
            page.PageType == typeof(DeviceSettingsPage));
        Assert.Contains(fixture.Composition.Catalog.Pages, page =>
            page.Name == "Dashboard" &&
            page.PageType == typeof(CompositionDashboardPage) &&
            page.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(fixture.Composition.Catalog.Services, service =>
            service.Type == typeof(CompositionFlow) &&
            service.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void CreateScope_ResolvesDiscoveredServicesWithFixtureDependencies()
    {
        var fixture = new CompositionFixture();

        using var scope = fixture.Composition.CreateScope();
        var page = scope.ServiceProvider.GetRequiredService<CompositionDashboardPage>();
        var flow = scope.ServiceProvider.GetRequiredService<CompositionFlow>();

        Assert.Same(fixture, page.Fixture);
        Assert.Same(page, flow.Page);
        Assert.Same(flow, scope.ServiceProvider.GetRequiredService<CompositionFlow>());
    }

    [TestModuleScan(typeof(TestCompositionTests), NamespacePrefix = "Bruinet.Core.Tests")]
    public sealed class CompositionFixture
    {
        public CompositionFixture()
        {
            Composition = TestComposition.ForFixture(this);
        }

        public TestComposition Composition { get; }
    }

    [TestPage("Dashboard")]
    public sealed class CompositionDashboardPage
    {
        public CompositionDashboardPage(CompositionFixture fixture)
        {
            Fixture = fixture;
        }

        public CompositionFixture Fixture { get; }
    }

    // A page found by Core's ITestPage alone, with no [TestPage]: as every stack's page base is.
    public sealed class DeviceSettingsPage : Bruinet.Core.Scopes.ITestPage
    {
        public string Name => nameof(DeviceSettingsPage);

        public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

        public Bruinet.Core.Scopes.ScopeReadiness ProbeReadiness() => Bruinet.Core.Scopes.ScopeReadiness.Ready(Name);

        public bool WaitReady(int? timeoutMs = null) => true;

        public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => 0;

        public bool IsLoaded() => true;

        public bool WaitLoaded(bool? expected, int? timeoutMs = null) => true;

        public void AssertLoaded(bool? expected, string? message = null, int? timeoutMs = null)
        {
        }

        public bool IsBusy() => false;

        public bool WaitBusy(bool? expected, int? timeoutMs = null) => expected != true;

        public void AssertIdle(string? message = null, int? timeoutMs = null)
        {
        }

        public string? GetTitle() => null;

        public bool WaitTitle(string? expected, int? timeoutMs = null) => true;

        public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
        {
        }

        public void TakeScreenshot(string? filename = null)
        {
        }
    }

    [TestScenarioService]
    public sealed class CompositionFlow : TestScenarioServiceBase
    {
        public CompositionFlow(CompositionDashboardPage page)
        {
            Page = page;
        }

        public CompositionDashboardPage Page { get; }
    }
}
