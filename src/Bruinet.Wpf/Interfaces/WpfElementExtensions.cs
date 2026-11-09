namespace Bruinet.Wpf.Interfaces;

/// <summary>Helpers over <see cref="IWpfElement"/>.</summary>
public static class WpfElementExtensions
{
    // An extension, not an interface member, so a mocked element needs only TryFindElement set up
    // (the same choice as MAUI's, Html's and native Android's).

    /// <summary>The first matching descendant. One attempt; never waits.</summary>
    /// <exception cref="ElementNotFoundException">Nothing matches.</exception>
    public static IWpfElement FindElement(this IWpfElement element, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.TryFindElement(locator) ?? throw new ElementNotFoundException(locator);
    }
}
