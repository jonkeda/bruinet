using Bruinet.Core.Composition;

namespace Bruinet.Bdd;

/// <summary>On a fixture, page or step-definition member: not a step, and not discovered.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class BddIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class BddStepDefinitionsAttribute : TestScenarioServiceAttribute
{
    /// <summary>
    /// The folder under the scenario root whose scenarios, and those below it, see this class's
    /// phrases (<c>Invoicing</c>, <c>Invoicing/Credit notes</c>). Null takes the class's or the
    /// assembly's <see cref="Bruinet.Bdd.BddNamespaceAttribute"/>, and without one every
    /// scenario sees them.
    /// </summary>
    public string? Namespace { get; init; }
}

public abstract class BddStepDefinitionsBase : TestScenarioServiceBase
{
}
