namespace Bruinet.Maui.Interfaces;

/// <summary>
/// A scroller's vertical position as a scroll pattern reports it: no app declaration needed, unlike
/// <see cref="ScrollPosition"/>.
/// </summary>
/// <param name="Vertical">The position, 0 at the top and 100 at the end.</param>
/// <param name="VerticalViewSize">How much of the content the viewport shows, in percent; 100 when it does not scroll.</param>
public readonly record struct ScrollPercent(double Vertical, double VerticalViewSize)
{
    /// <summary>Whether the content is taller than the viewport.</summary>
    public bool Scrolls => VerticalViewSize < 100;
}
