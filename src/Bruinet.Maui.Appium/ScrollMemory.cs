using Bruinet.Core.Locators;


namespace Bruinet.Maui.Appium;

/// <summary>
/// Where views were last seen along the vertical scroll: a short log of sightings and moving
/// vertical swipes. A search for a view the page has since moved past goes back toward it first (D12).
/// </summary>
/// <remarks>
/// Moves are not told apart by scroller: UiAutomator2 element ids are not stable across finds, and
/// a page rarely has two vertical scrollers. The memory only orders the directions; both are searched.
/// </remarks>
internal sealed class ScrollMemory
{
    private const int Capacity = 256;

    private readonly List<(string? View, int Step)> _events = [];

    public void Seen(Locator view) => Add((Key(view), 0));

    /// <param name="direction">"down" or "up"; anything else is not a vertical move.</param>
    public void Moved(string direction)
    {
        if (direction is "down" or "up")
        {
            Add((null, direction == "down" ? 1 : -1));
        }
    }

    /// <summary>Up then down when the page has moved down since the view was last seen, else down then up.</summary>
    public IReadOnlyList<string> Directions(Locator view)
    {
        var key = Key(view);
        var net = 0;
        for (var i = _events.Count - 1; i >= 0; i--)
        {
            var (seen, step) = _events[i];
            if (seen == key)
            {
                return net > 0 ? UpFirst : DownFirst;
            }

            net += step;
        }

        return DownFirst;
    }

    private static string Key(Locator view) => $"{view.Strategy}:{view.Value}";

    private void Add((string?, int) entry)
    {
        if (_events.Count == Capacity)
        {
            _events.RemoveAt(0);
        }

        _events.Add(entry);
    }

    private static readonly string[] DownFirst = ["down", "up"];
    private static readonly string[] UpFirst = ["up", "down"];
}
