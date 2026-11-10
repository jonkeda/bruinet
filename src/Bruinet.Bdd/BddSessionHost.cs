using Bruinet.Core.Composition;

namespace Bruinet.Bdd;

/// <summary>A fixture with a pinned clock: relative dates in steps (<c>today</c>, <c>+2d</c>) resolve against it.</summary>
public interface IBddClock
{
    /// <summary>The time the scenario's test data calls now.</summary>
    DateTimeOffset Now { get; }
}

/// <summary>
/// A fixture that runs several apps in one scenario: two devices on one backend, two users. Each
/// app is a session with its own test context and page objects; <c>{app} uses the app</c> makes
/// one current. See <c>.docs/contracts/uat-apps-host.md</c>.
/// </summary>
/// <remarks>
/// Apps of one head share the suite's page and phrase types: a session changes which app a page
/// object drives, not which page objects there are. An app of another head brings its own
/// (<see cref="CompositionFor"/>). The app names are the <c>Apps</c> table's, which the engine
/// checks before it asks the fixture.
/// </remarks>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public interface IBddSessionHost
{
    /// <summary>
    /// Makes <paramref name="name"/> the current app, starting it when it is not running yet.
    /// </summary>
    /// <param name="name">The app's name, as in the <c>Apps</c> table.</param>
    /// <returns>The services the app's page objects and phrases resolve through, for the rest of the scenario.</returns>
    IServiceProvider UseSession(string name);

    /// <summary>
    /// The composition whose pages and phrases <paramref name="name"/>'s head uses, when it is
    /// not the fixture's own: an app of another head in a mixed suite.
    /// </summary>
    /// <param name="name">The app's name, as in the <c>Apps</c> table.</param>
    /// <returns>The composition, or null for the fixture's own.</returns>
    TestComposition? CompositionFor(string name) => null;
}
