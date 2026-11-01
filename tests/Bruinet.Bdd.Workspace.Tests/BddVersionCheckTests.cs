using System.Reflection;
using Xunit;

namespace Bruinet.Bdd.Workspace.Tests;

public sealed class BddVersionCheckTests
{
    private static readonly Dictionary<string, Version> Own = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bruinet.Uat"] = new(1, 4, 2),
        ["Bruinet.Bdd"] = new(1, 4, 2),
    };

    [Fact]
    public void APatchApart_IsTheSame()
    {
        Assert.Empty(BddVersionCheck.Mismatches([Named("Bruinet.Uat", "1.4.0")], Own));
    }

    [Fact]
    public void AMinorOrMajorApart_IsSaid_PerAssembly()
    {
        var mismatches = BddVersionCheck.Mismatches([Named("Bruinet.Uat", "1.5.0"), Named("Bruinet.Bdd", "2.0.1"), Named("Newtonsoft.Json", "9.0.0")], Own);

        Assert.Equal(
            ["the suite is built against Bruinet.Bdd 2.0.1, and the extension runs 1.4.2", "the suite is built against Bruinet.Uat 1.5.0, and the extension runs 1.4.2"],
            mismatches);
    }

    [Fact]
    public void AnAssemblyTheToolDoesNotRun_IsNotCompared()
    {
        Assert.Empty(BddVersionCheck.Mismatches([Named("Bruinet.Maui", "9.9.9")], Own));
    }

    private static AssemblyName Named(string name, string version) => new(name) { Version = Version.Parse(version) };
}
