namespace Bruinet.Scraper.Host;

/// <summary>
/// One kind of record of an app (a contact, a project) that the Scraper may make for itself: through the app's own
/// API or the suite's page objects, never through the Scraper's browser. A record carries the run's marker in its
/// name, and only a record that does is deleted.
/// </summary>
public interface IRecordKind
{
    /// <summary>The kind's name, as the app's records table writes it.</summary>
    string Name { get; }

    /// <summary>Makes a record whose name starts with <paramref name="marker"/>; returns the address of its own page.</summary>
    Task<string> MakeAsync(string marker, CancellationToken cancellationToken = default);

    /// <summary>The addresses of the records whose name starts with <paramref name="marker"/>.</summary>
    Task<IReadOnlyList<string>> FindAsync(string marker, CancellationToken cancellationToken = default);

    /// <summary>Deletes every record whose name starts with <paramref name="marker"/>; returns how many.</summary>
    Task<int> DeleteAsync(string marker, CancellationToken cancellationToken = default);
}
