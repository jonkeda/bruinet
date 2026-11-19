using Bruinet.Scraper.Code;

namespace Bruinet.Scraper.Host;

/// <summary>
/// What a host adds to the Scraper's commands: <c>bruinet</c> gives its control template generator and its
/// reading of a suite; an app's own Scraper project gives the app's records and readers.
/// </summary>
public sealed record HostServices
{
    /// <summary>Generates a project's control templates before it builds; null to build only.</summary>
    public ITemplateGenerator? Templates { get; init; }

    /// <summary>The addresses a suite's page objects open at, by the suite's folder; null when the host cannot read a suite.</summary>
    public Func<string, IReadOnlyList<string>>? SuiteAddresses { get; init; }

    /// <summary>The app's kinds of record: how each is made, found and deleted.</summary>
    public IReadOnlyList<IRecordKind> Records { get; init; } = [];

    /// <summary>The app's own readers, for what a sign in a table cannot say.</summary>
    public IReadOnlyList<IPlaceReader> Readers { get; init; } = [];
}
