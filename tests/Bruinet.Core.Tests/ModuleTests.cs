using System.Xml.Linq;

namespace Bruinet.Core.Tests;

/// <summary>
/// Bruinet is one product in four modules: Test, Spec, Develop and Migrate (AD-044). A module is
/// known by its projects' names under src/: <c>Bruinet.Spec*</c>, <c>Bruinet.Develop*</c>, <c>Bruinet.Migrate*</c>, and
/// every other project is Test. References between modules point one way, so the code has no cycle and Spec can be
/// taken without Test's drivers.
/// </summary>
public sealed class ModuleTests
{
    private enum Module
    {
        Test,
        Spec,
        Develop,
        Migrate,
    }

    // The one project of Spec that Test may reference: reading a spec into a model, with no checks.
    private const string SpecFormat = "Bruinet.Spec.Format";

    [Fact]
    public void ReferencesBetweenModules_PointOneWay()
    {
        var root = SolutionTests.FindRepositoryRoot();
        Assert.NotNull(root);

        var wrong = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => References(path).Select(reference => (from: Path.GetFileNameWithoutExtension(path), to: reference)))
            .Where(pair => !Allowed(pair.from, pair.to))
            .Select(pair => $"{pair.from} → {pair.to}")
            .Order()
            .ToList();

        Assert.True(wrong.Count == 0,
            "These references go the wrong way between modules (Spec references only Spec; Test reaches Spec only through "
            + $"{SpecFormat}; nothing below references Develop or Migrate; Develop does not reference Migrate): {string.Join(", ", wrong)}");
    }

    [Theory]
    [InlineData("Bruinet.Spec", "Bruinet.Spec.Format", true)]
    [InlineData("Bruinet.Spec", "Bruinet.Core", false)]
    [InlineData("Bruinet.Bdd", "Bruinet.Spec.Format", true)]
    [InlineData("Bruinet.Bdd", "Bruinet.Spec", false)]
    [InlineData("Bruinet.Bdd", "Bruinet.Develop", false)]
    [InlineData("Bruinet.Develop", "Bruinet.Spec", true)]
    [InlineData("Bruinet.Develop", "Bruinet.Html", true)]
    [InlineData("Bruinet.Develop", "Bruinet.Migrate", false)]
    [InlineData("Bruinet.Migrate", "Bruinet.Develop", true)]
    [InlineData("Bruinet.Spectrum", "Bruinet.Core", true)]
    public void Allowed_FollowsTheModuleOrder(string from, string to, bool allowed) =>
        Assert.Equal(allowed, Allowed(from, to));

    private static bool Allowed(string from, string to) => (ModuleOf(from), ModuleOf(to)) switch
    {
        (var a, var b) when a == b => true,
        (Module.Spec, _) => false,
        (Module.Test, Module.Spec) => to == SpecFormat,
        (Module.Test, _) => false,
        (Module.Develop, Module.Migrate) => false,
        _ => true,
    };

    private static Module ModuleOf(string project) =>
        IsIn(project, "Bruinet.Spec") ? Module.Spec
        : IsIn(project, "Bruinet.Develop") ? Module.Develop
        : IsIn(project, "Bruinet.Migrate") ? Module.Migrate
        : Module.Test;

    private static bool IsIn(string project, string module) =>
        project == module || project.StartsWith(module + ".", StringComparison.Ordinal);

    private static IEnumerable<string> References(string project) => XDocument.Load(project)
        .Descendants("ProjectReference")
        .Select(reference => (string?)reference.Attribute("Include"))
        .OfType<string>()
        .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)));
}
