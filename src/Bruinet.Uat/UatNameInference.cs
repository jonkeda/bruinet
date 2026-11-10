using Bruinet.Bdd;
using System.Reflection;
using Bruinet.Core.Testing;

namespace Bruinet.Uat;

public static class UatNameInference
{
    public static string FromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return FromIdentifier(type.Name);
    }

    public static string FromMember(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return FromIdentifier(member.Name);
    }

    public static string FromIdentifier(string identifier) => StepNames.FromIdentifier(identifier);

    public static bool HasKnownSuffix(string identifier) => StepNames.HasKnownSuffix(identifier);
}
