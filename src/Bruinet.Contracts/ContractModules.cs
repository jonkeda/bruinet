using System.Reflection;

namespace Bruinet.Contracts;

/// <summary>
/// One module's contracts in one list. <see cref="ContractModules.FromAssembly"/> finds every implementation,
/// so a module joins the offline mock test, the summary and the unregistered-endpoint check by implementing this.
/// </summary>
/// <remarks>Implementations need a public parameterless constructor.</remarks>
public interface IContractModule
{
    /// <summary>The module name used in reports, for example <c>Contacts</c>.</summary>
    string Name { get; }

    /// <summary>Every endpoint the module calls that has a contract, read and write.</summary>
    IReadOnlyList<DtoContract> Contracts { get; }

    /// <summary>The mock requests the offline test sends; each response must satisfy its contract.</summary>
    IReadOnlyList<MockContractRequest> MockRequests { get; }
}

/// <summary>
/// One request to the mock backend and the contract its response must satisfy. A request with a
/// non-GET contract sends <see cref="Form"/> as a URL-encoded body.
/// </summary>
public sealed record MockContractRequest(DtoContract Contract, string Path)
{
    public IReadOnlyDictionary<string, string> Form { get; init; } = new Dictionary<string, string>();
}

public static class ContractModules
{
    /// <summary>Every <see cref="IContractModule"/> in <paramref name="assembly"/>, ordered by name.</summary>
    public static IReadOnlyList<IContractModule> FromAssembly(Assembly assembly)
        => assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IContractModule).IsAssignableFrom(type))
            .Select(type => (IContractModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Every contract once; modules may share one, such as a lookup list.</summary>
    public static IReadOnlyList<DtoContract> AllContracts(IEnumerable<IContractModule> modules)
        => modules.SelectMany(module => module.Contracts).Distinct().ToList();
}
