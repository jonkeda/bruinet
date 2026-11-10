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

/// <summary>A UI test against a mock backend. Writes <c>Pyramid=Hermetic UI</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class HermeticUiAttribute() : BruinetTraitAttribute(
    Trait(TraitKeys.Pyramid, TierNames.HermeticUi));

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

/// <summary>Owned by an app module. Writes <c>Module=name</c>.</summary>
[TraitDiscoverer(BruinetTraitDiscoverer.TypeName, BruinetTraitDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ModuleAttribute(string name) : BruinetTraitAttribute(
    Trait(TraitKeys.Module, Required(name, nameof(name))))
{
    /// <summary>The module name.</summary>
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
