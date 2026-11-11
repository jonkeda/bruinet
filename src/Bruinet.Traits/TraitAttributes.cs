using Xunit.Sdk;

namespace Bruinet.Traits;

/// <summary>A unit test. Writes <c>Pyramid=Unit</c> and <c>Category=Unit</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class UnitAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Unit), Trait(TraitKeys.Category, TierNames.Unit));

/// <summary>An integration test. Writes <c>Pyramid=Integration</c> and <c>Category=Integration</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class IntegrationAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Integration), Trait(TraitKeys.Category, TierNames.Integration));

/// <summary>
/// A UI test against a mock backend. Writes <c>Pyramid=UI</c>, and the old <c>Pyramid=Hermetic UI</c>
/// so filters on the old name keep finding it during the move.
/// </summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class UiAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Ui), Trait(TraitKeys.Pyramid, TierNames.HermeticUi));

/// <summary>The old name of <see cref="UiAttribute"/>; writes the same traits.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class HermeticUiAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Ui), Trait(TraitKeys.Pyramid, TierNames.HermeticUi));

/// <summary>A test against a real backend. Writes <c>Pyramid=Live</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Live));

/// <summary>A narrated video recording. Writes <c>Pyramid=Video</c> and <c>Category=Video</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class VideoAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.Video), Trait(TraitKeys.Category, TierNames.Video));

/// <summary>In the smoke run set. Writes <c>Category=Smoke</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SmokeAttribute() : BruinetTraitAttribute(Trait(TraitKeys.Category, "Smoke"));

/// <summary>In the user-acceptance run set. Writes <c>Category=UAT</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class UatAttribute() : BruinetTraitAttribute(Trait(TraitKeys.Category, "UAT"));

/// <summary>In the API contract run set. Writes <c>Category=Contract</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ContractAttribute() : BruinetTraitAttribute(Trait(TraitKeys.Category, "Contract"));

/// <summary>In the architecture run set. Writes <c>Category=Architecture</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ArchitectureAttribute() : BruinetTraitAttribute(Trait(TraitKeys.Category, "Architecture"));

/// <summary>
/// In the review run set: accessibility, localization and screenshot checks a person reads.
/// Writes <c>Category=Review</c>.
/// </summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ReviewAttribute() : BruinetTraitAttribute(Trait(TraitKeys.Category, "Review"));

/// <summary>Owned by an app module. Writes <c>Module=name</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ModuleAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.Module, Required(name, nameof(name))))
{
    /// <summary>The module name.</summary>
    public string Name { get; } = name;
}

/// <summary>
/// Owned by a feature. Writes <c>Feature=name</c>. Use it instead of <see cref="ModuleAttribute"/> when
/// the app calls its parts features, or beside it for a feature that spans modules.
/// </summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class FeatureAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.Feature, Required(name, nameof(name))))
{
    /// <summary>The feature name.</summary>
    public string Name { get; } = name;
}

/// <summary>Owned by a shared component. Writes <c>Component=name</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ComponentAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.Component, Required(name, nameof(name))))
{
    /// <summary>The component name.</summary>
    public string Name { get; } = name;
}

/// <summary>Owned by a shared infrastructure area. Writes <c>CommonArea=name</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CommonAreaAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.CommonArea, Required(name, nameof(name))))
{
    /// <summary>The area name.</summary>
    public string Name { get; } = name;
}

/// <summary>Proves one user journey. Writes <c>Journey=id</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class JourneyAttribute(string id) : BruinetTraitAttribute(
    Trait(TraitKeys.Journey, Required(id, nameof(id))))
{
    /// <summary>The journey id.</summary>
    public string Id { get; } = id;
}

/// <summary>
/// Names a coverage row, check, finding or decision the test proves. Writes <c>Covers=id</c>.
/// </summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CoversAttribute(string id) : BruinetTraitAttribute(
    Trait(TraitKeys.Covers, Required(id, nameof(id))))
{
    /// <summary>The id of the row, check, finding or decision.</summary>
    public string Id { get; } = id;
}

/// <summary>
/// Needs one head to run, named as in <c>UatPlatforms</c> (for example <c>Android</c>). Writes <c>Head=name</c>.
/// </summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HeadAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.Head, Required(name, nameof(name))))
{
    /// <summary>The head name.</summary>
    public string Name { get; } = name;
}
