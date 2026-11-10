using Bruinet.Core.Scopes;

namespace Bruinet.Core.Composition;

/// <summary>What counts as a page object when a suite's assemblies are scanned.</summary>
public static class TestPages
{
    /// <summary>
    /// Whether <paramref name="type"/> is a page object: it implements Core's <see cref="ITestPage"/>,
    /// as every stack's page base does (AD-010).
    /// </summary>
    /// <param name="type">A type found in a suite's assembly.</param>
    public static bool IsPage(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return typeof(ITestPage).IsAssignableFrom(type);
    }
}
