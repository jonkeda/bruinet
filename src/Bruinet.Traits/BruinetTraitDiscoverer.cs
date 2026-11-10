using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Traits;

/// <summary>The trait keys the Bruinet trait attributes write.</summary>
public static class TraitKeys
{
    /// <summary>The test tier: <see cref="TierNames"/>.</summary>
    public const string Pyramid = "Pyramid";

    /// <summary>The run set (Smoke, UAT, Contract), and the tier again for Unit, Integration and Video.</summary>
    public const string Category = "Category";

    /// <summary>The app module that owns the test.</summary>
    public const string Module = "Module";

    /// <summary>The shared component that owns the test.</summary>
    public const string Component = "Component";

    /// <summary>The shared infrastructure area that owns the test.</summary>
    public const string CommonArea = "CommonArea";

    /// <summary>One user journey the test proves.</summary>
    public const string Journey = "Journey";
}

/// <summary>The values of the <see cref="TraitKeys.Pyramid"/> trait.</summary>
public static class TierNames
{
    /// <summary>A unit test.</summary>
    public const string Unit = "Unit";

    /// <summary>An integration test.</summary>
    public const string Integration = "Integration";

    /// <summary>A UI test against a mock backend.</summary>
    public const string HermeticUi = "Hermetic UI";

    /// <summary>A test against a real backend.</summary>
    public const string Live = "Live";

    /// <summary>A narrated video recording.</summary>
    public const string Video = "Video";

    /// <summary>Every tier.</summary>
    public static IReadOnlyList<string> All { get; } = [Unit, Integration, HermeticUi, Live, Video];
}

/// <summary>
/// Reads the traits a Bruinet trait attribute writes. One discoverer serves every attribute: it
/// reads the attribute's constructor arguments as alternating key and value pairs.
/// </summary>
public sealed class BruinetTraitDiscoverer : ITraitDiscoverer
{
    /// <summary>The discoverer's type name, as <see cref="TraitDiscovererAttribute"/> needs it.</summary>
    public const string TypeName = "Bruinet.Traits.BruinetTraitDiscoverer";

    /// <summary>The discoverer's assembly name.</summary>
    public const string AssemblyName = "Bruinet.Traits";

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        ArgumentNullException.ThrowIfNull(traitAttribute);

        // Reflection-based discovery, which every xUnit 2 runner uses, hands over the attribute itself.
        return traitAttribute is IReflectionAttributeInfo { Attribute: BruinetTraitAttribute attribute }
            ? attribute.Traits
            : [];
    }
}

/// <summary>Base of the Bruinet trait attributes.</summary>
/// <remarks>
/// xUnit reads <see cref="TraitDiscovererAttribute"/> from the attribute's own type, not its base,
/// so every concrete attribute repeats it.
/// </remarks>
public abstract class BruinetTraitAttribute : Attribute, ITraitAttribute
{
    private protected BruinetTraitAttribute(params KeyValuePair<string, string>[] traits)
    {
        Traits = traits;
    }

    /// <summary>The traits this attribute writes.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Traits { get; }

    private protected static KeyValuePair<string, string> Trait(string key, string value) => new(key, value);

    private protected static string Required(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }
}
