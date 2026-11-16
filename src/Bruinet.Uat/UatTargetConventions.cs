namespace Bruinet.Uat;

/// <summary>
/// Which target a fixture is, read from the fixture base it derives from.
/// </summary>
/// <remarks>
/// A fixture base owns one platform head, so the base type answers "which target" without
/// anyone writing it down. This is the single source of that mapping: the runtime checks it
/// against a live fixture, Presenter against metadata it has not loaded.
/// </remarks>
public static class UatTargetConventions
{
    private static readonly Dictionary<string, string> TargetsByFixtureBase = new(StringComparer.Ordinal)
    {
        ["MauiTestFixtureBase"] = "MAUI",
        ["WpfTestFixtureBase"] = "WPF",
        ["WinFormsTestFixtureBase"] = "WINFORMS",
        ["StrideTestFixtureBase"] = "STRIDE",
        ["BlazorTestFixtureBase"] = "BLAZOR",
        ["HtmlTestFixtureBase"] = "HTML",
        ["NativeAndroidTestFixtureBase"] = "NATIVEANDROID",
        ["NativeIosTestFixtureBase"] = "NATIVEIOS"
    };

    /// <summary>The fixture base names that identify a target.</summary>
    public static IReadOnlyCollection<string> FixtureBaseNames => TargetsByFixtureBase.Keys;

    /// <summary>Derives the target from a live fixture's base types.</summary>
    /// <param name="fixture">The fixture instance.</param>
    /// <returns>The target, or null when it derives from no known fixture base.</returns>
    public static string? FromFixture(object fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        for (var type = fixture.GetType().BaseType; type is not null; type = type.BaseType)
        {
            if (TargetsByFixtureBase.TryGetValue(type.Name, out var target))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Derives the target from base type names, as metadata-only inspection reads them.</summary>
    /// <param name="baseTypeNames">The base chain, nearest first.</param>
    /// <returns>The target, or null when none of them is a known fixture base.</returns>
    public static string? FromBaseTypeNames(IEnumerable<string> baseTypeNames)
    {
        ArgumentNullException.ThrowIfNull(baseTypeNames);

        foreach (var name in baseTypeNames)
        {
            if (TargetsByFixtureBase.TryGetValue(name, out var target))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Whether a type name is one of the fixture bases.</summary>
    /// <param name="typeName">The type name to test.</param>
    /// <returns>Whether it identifies a target.</returns>
    public static bool IsFixtureBase(string typeName) => TargetsByFixtureBase.ContainsKey(typeName);
}
