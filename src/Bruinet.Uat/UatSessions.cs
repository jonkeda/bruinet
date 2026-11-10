using Bruinet.Bdd;
using System.Runtime.CompilerServices;
using Bruinet.Core.Composition;
using Bruinet.Core.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Uat;

/// <summary>
/// A fixture whose apps are a <see cref="TestAppSet"/>, shared with its UI tests: sessions, subject
/// resolution and a composition per head come from the set (<c>.docs/contracts/uat-apps-host.md</c>).
/// </summary>
/// <remarks>
/// Each app gets one composition and one scope, built the first time a scenario uses it, with the
/// app's own services (<see cref="AddAppServices"/>) on top of its head's modules.
/// </remarks>
public interface IUatAppsHost : IBddSessionHost
{
    /// <summary>The apps; their names are the <c>Apps</c> table's.</summary>
    TestAppSet Apps { get; }

    /// <summary>The modules whose pages and phrases <paramref name="app"/>'s head uses; null for the fixture's own.</summary>
    /// <param name="app">The app.</param>
    TestCompositionDiscoveryOptions? ModulesFor(TestAppDefinition app) => null;

    /// <summary>Adds what <paramref name="app"/>'s page objects need, such as its test context.</summary>
    /// <param name="app">The started app.</param>
    /// <param name="services">Its composition's services.</param>
    void AddAppServices(ITestApp app, IServiceCollection services);

    /// <summary>Called each time <paramref name="app"/> becomes current, for a fixture that tracks it.</summary>
    /// <param name="app">The app.</param>
    void OnAppUsed(ITestApp app)
    {
    }

    IServiceProvider IBddSessionHost.UseSession(string name) => UatAppSessions.Use(this, name);

    TestComposition? IBddSessionHost.CompositionFor(string name) => UatAppSessions.CompositionFor(this, name);
}

internal static class UatAppSessions
{
    // Keyed by the app object: a stopped and restarted app is a new object with a new scope.
    private static readonly ConditionalWeakTable<ITestApp, IServiceScope> Scopes = new();
    private static readonly ConditionalWeakTable<IUatAppsHost, Dictionary<string, TestComposition>> Heads = new();

    public static IServiceProvider Use(IUatAppsHost host, string name)
    {
        var app = host.Apps.Get(name);
        var scope = Scopes.GetValue(app, started => TestComposition
            .ForFixture(host, Modules(host, started.Definition), services => host.AddAppServices(started, services))
            .CreateScope());
        host.OnAppUsed(app);
        return scope.ServiceProvider;
    }

    // For discovery only: the head's page and phrase types, with no app started.
    public static TestComposition? CompositionFor(IUatAppsHost host, string name)
    {
        var definition = host.Apps.Resolve(name);
        if (host.ModulesFor(definition) is not { } modules)
        {
            return null;
        }

        var heads = Heads.GetOrCreateValue(host);
        lock (heads)
        {
            var head = definition.Head ?? string.Empty;
            if (!heads.TryGetValue(head, out var composition))
            {
                composition = TestComposition.ForFixture(host, modules);
                heads[head] = composition;
            }

            return composition;
        }
    }

    private static TestCompositionDiscoveryOptions Modules(IUatAppsHost host, TestAppDefinition app)
        => host.ModulesFor(app) ?? TestCompositionDiscoveryOptions.FromFixture(host.GetType());
}

internal static class UatSessionSteps
{
    public const string CommandId = "Builtin.Session.Use";

    public const string Phrase = "{app} uses the app";

    /// <summary>Registers <c>{app} uses the app</c>, as a Given and a When, for a fixture that hosts sessions.</summary>
    public static void Register(BddCommandCatalog catalog, IBddSessionHost? host, BddConfig config)
    {
        BddCommandHandler handler = (context, invocation, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requested = invocation.Arguments["app"];
            var app = config.Apps.FirstOrDefault(candidate => string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase));
            if (config.Apps.Count > 0 && app is null)
            {
                return Task.FromResult(BddStepResult.Failed(
                    invocation,
                    $"No app '{requested}' in the Apps table of uat.config.md. It has: {string.Join(", ", config.Apps.Select(candidate => $"'{candidate.Name}'"))}."));
            }

            context.SessionHost ??= host;
            context.SwitchApp(app?.Name ?? requested);
            return Task.FromResult(BddStepResult.Passed(invocation));
        };

        catalog.Register(BddEffectiveStepKeyword.Given, Phrase, CommandId, allowsTable: false, handler: handler);
        catalog.Register(BddEffectiveStepKeyword.When, Phrase, CommandId, allowsTable: false, handler: handler);
    }
}
