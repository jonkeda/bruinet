using Bruinet.Scraper.Places;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Host;

/// <summary>
/// Reads from a place's capture what the signs and holders of a profile cannot say: a widget of the app's own.
/// It gives the place back with what it read added, or as it was.
/// </summary>
public interface IPlaceReader
{
    /// <summary>The place as read by the engine, with this reader's fields and buttons added.</summary>
    Place Read(Place place, ScrapeSnapshot snapshot);
}
