using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Sdk;

namespace Bruinet.Samples.Todo.JourneyCoverage;

/// <summary>One test method and the traits it carries, its class's included.</summary>
/// <param name="Name">The class and method, <c>ListJourneyTests.Row_ShowsTitleDueDateAndStatus</c>.</param>
/// <param name="Pyramid">The <c>Pyramid</c> trait, or null when it has none.</param>
/// <param name="Gates">The <c>Gate</c> traits: <c>Smoke</c>, <c>Review</c>.</param>
/// <param name="Journeys">The <c>Journey</c> traits: subtask ids.</param>
/// <param name="Cases">How many cases it runs: 1 for a fact, one per data row for a theory.</param>
public sealed record TestMethod(
    string Name,
    string? Pyramid,
    IReadOnlyList<string> Gates,
    IReadOnlyList<string> Journeys,
    int Cases)
{
    /// <summary>Whether this test counts for <paramref name="tier"/>.</summary>
    public bool Covers(Tier tier) => tier switch
    {
        Tier.Unit => Pyramid == "Unit",
        Tier.Integration => Pyramid == "Integration",
        Tier.Contract => Pyramid == "Contract",
        Tier.UiHermetic => Pyramid == "UiHermetic",
        Tier.UiLive => Pyramid == "UiLive",
        Tier.Smoke => Gates.Contains("Smoke"),
        Tier.Review => Gates.Contains("Review"),
        _ => false,
    };
}

/// <summary>
/// The tests of the sample's test assemblies, read by reflection: nothing is run and no app is launched.
/// </summary>
/// <remarks>
/// Unit and integration tests are loaded, so a theory's <c>MemberData</c> can be counted. The UI
/// tests are read as metadata from their own build output: two of them target
/// <c>net10.0-windows</c>, which this <c>net10.0</c> project cannot load.
/// </remarks>
public static class TestCatalog
{
    private const string FactAttribute = "Xunit.FactAttribute";
    private const string TraitAttribute = "Xunit.TraitAttribute";
    private const string DataAttribute = "Xunit.Sdk.DataAttribute";

    /// <summary>The loaded assemblies whose tests the report compares with <c>journeys.md</c>.</summary>
    public static readonly string[] AssemblyNames =
    [
        "Bruinet.Samples.Todo.UnitTests",
        "Bruinet.Samples.Todo.IntegrationTests",
    ];

    /// <summary>The UI test projects, read from their output folders, and the framework each builds for.</summary>
    public static readonly (string Project, string Framework)[] UiProjects =
    [
        ("Bruinet.Samples.Todo.UITests", "net10.0-windows"),
        ("Bruinet.Samples.Todo.Web.UITests", "net10.0"),
        ("Bruinet.Samples.Todo.Mixed.UITests", "net10.0-windows"),
    ];

    /// <summary>Every test method in <see cref="AssemblyNames"/> and <see cref="UiProjects"/>, and every scenario (<see cref="ScenarioCatalog"/>).</summary>
    public static IReadOnlyList<TestMethod> Load()
        => [.. AssemblyNames.Select(Assembly.Load).SelectMany(Read), .. UiProjects.SelectMany(ReadMetadata), .. ScenarioCatalog.Load()];

    private static IEnumerable<TestMethod> Read(Assembly assembly)
        => from type in assembly.GetTypes()
           where type is { IsClass: true, IsAbstract: false }
           from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
           where IsTest(method)
           select Describe(type, method, CountCases(method));

    // Materialised inside the context: its types are gone once it is disposed.
    private static IReadOnlyList<TestMethod> ReadMetadata((string Project, string Framework) ui)
    {
        var path = OutputOf(ui.Project, ui.Framework);
        var assemblies = Directory.GetFiles(Path.GetDirectoryName(path)!, "*.dll")
            .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(assemblies));

        return [.. from type in context.LoadFromAssemblyPath(path).GetTypes()
                   where type is { IsClass: true, IsAbstract: false }
                   from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                   where IsTest(method)
                   select Describe(type, method, CountDataAttributes(method))];
    }

    // tests/<project>/bin/<configuration>/<framework>/<project>.dll, in this build's configuration.
    private static string OutputOf(string project, string framework)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var tests = new DirectoryInfo(AppContext.BaseDirectory);
        while (tests is not null && !Directory.Exists(Path.Combine(tests.FullName, project)))
        {
            tests = tests.Parent;
        }

        var path = tests is null
            ? null
            : Path.Combine(tests.FullName, project, "bin", configuration, framework, project + ".dll");
        return path is not null && File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                $"{project} is not built for {framework} ({configuration}). Building this project builds it; so does dotnet build on it.",
                path ?? project);
    }

    private static TestMethod Describe(Type type, MethodInfo method, int cases)
    {
        // A class's traits apply to each of its tests, as xUnit applies them.
        var traits = Traits(method).Concat(ClassHierarchy(type).SelectMany(Traits)).ToList();

        return new TestMethod(
            $"{type.Name}.{method.Name}",
            traits.FirstOrDefault(t => t.Name == "Pyramid").Value,
            [.. traits.Where(t => t.Name == "Gate").Select(t => t.Value).Distinct()],
            [.. traits.Where(t => t.Name == "Journey").Select(t => t.Value).Distinct()],
            cases);
    }

    // By name, not by type: a metadata-only assembly has its own Xunit types.
    private static bool IsTest(MethodInfo method)
        => method.GetCustomAttributesData().Any(a => DerivesFrom(a.AttributeType, FactAttribute));

    private static bool DerivesFrom(Type? type, string fullName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> ClassHierarchy(Type type)
    {
        for (var current = type; current != null && current.FullName != "System.Object"; current = current.BaseType)
        {
            yield return current;
        }
    }

    // TraitAttribute keeps its name and value only as constructor arguments.
    private static IEnumerable<(string Name, string Value)> Traits(MemberInfo member)
        => member.GetCustomAttributesData()
            .Where(a => a.AttributeType.FullName == TraitAttribute)
            .Select(a => ((string)a.ConstructorArguments[0].Value!, (string)a.ConstructorArguments[1].Value!));

    private static int CountCases(MethodInfo method)
    {
        var data = method.GetCustomAttributes<DataAttribute>(inherit: true).ToList();
        if (data.Count == 0)
            return 1;

        try
        {
            // MemberData runs a static member of the test class: rule tables, no side effects.
            return Math.Max(1, data.Sum(attribute => attribute.GetData(method).Count()));
        }
        catch (Exception)
        {
            return data.Count;
        }
    }

    // Metadata cannot run MemberData: each data attribute counts once, an InlineData row exactly.
    private static int CountDataAttributes(MethodInfo method)
        => Math.Max(1, method.GetCustomAttributesData().Count(a => DerivesFrom(a.AttributeType, DataAttribute)));
}
