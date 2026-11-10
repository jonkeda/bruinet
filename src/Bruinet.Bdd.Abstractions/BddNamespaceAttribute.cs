using System.Reflection;

namespace Bruinet.Bdd;

/// <summary>
/// The UAT namespace of a phrase class or page object: a folder under the scenario root, with
/// <c>/</c> between folders (<c>Invoicing</c>, <c>Invoicing/Credit notes</c>). On an assembly, the
/// namespace of every phrase class and page in it that does not name its own. A scenario sees a
/// namespace's phrases and pages from that folder and below, or with <c>Using</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
public sealed class BddNamespaceAttribute : Attribute
{
    /// <param name="name">A folder under the scenario root, with <c>/</c> between folders.</param>
    public BddNamespaceAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The namespace.</summary>
    public string Name { get; }

    /// <summary>The namespace <paramref name="type"/> declares, else its assembly's; null for neither.</summary>
    public static string? Of(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetCustomAttribute<BddNamespaceAttribute>()?.Name ??
               type.Assembly.GetCustomAttribute<BddNamespaceAttribute>()?.Name;
    }
}
