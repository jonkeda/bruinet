using System.Runtime.CompilerServices;
using Bruinet.Core.Composition;
using Bruinet.Core.Testing;
using Xunit;

namespace Bruinet.Bdd.Workspace.Tests;

/// <summary>A suite loaded the way every tool loads it: from a shadow copy, unloadable (AD-018).</summary>
public sealed class BddSuiteTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "BddSuiteTests", Guid.NewGuid().ToString("N"));
    private readonly string _built;

    public BddSuiteTests()
    {
        // The suite's build output: this test assembly, alone in a bin folder of its own, so the
        // tests can overwrite it as a build would.
        var bin = Path.Combine(_folder, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(bin);
        _built = Path.Combine(bin, "Shelf.Steps.dll");
        File.Copy(typeof(BddSuiteTests).Assembly.Location, _built);
        File.WriteAllText(Path.Combine(_folder, "bdd.config.md"), $"""
            # BDD Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Fixture | {nameof(ShelfFixture)} |
            | Head | service |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Steps | bin/Debug/net10.0/Shelf.Steps.dll |
            """);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A context not collected yet; the temp folder is cleaned by the OS.
        }
    }

    [Fact]
    public void Catalogs_ComeFromTheFixture_InTheBuildOutput()
    {
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));

        Assert.True(suite.CanBind);
        Assert.Equal("service", suite.Head);
        Assert.Contains(suite.Catalogs.Default.Patterns, pattern => pattern.Phrase == "I shelve {title}");
    }

    [Fact]
    public void WorkingDirectory_IsTheConfigsFolder_ResolvedFromTheConfig_OrNullWhenItNamesNone()
    {
        var config = Path.Combine(_folder, "bdd.config.md");
        using (var suite = BddSuite.Open(config))
        {
            Assert.Null(suite.WorkingDirectory);
        }

        File.WriteAllText(config, File.ReadAllText(config).Replace("| Head | service |", "| Head | service |\n| WorkingDirectory | .. |"));
        using var moved = BddSuite.Open(config);

        Assert.Equal(Path.GetFullPath(Path.Combine(_folder, "..")), moved.WorkingDirectory);
    }

    [Fact]
    public void ASuiteWithout_UatScenarioTheory_SaysSo_OnceLoaded()
    {
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));
        Assert.False(suite.HasScenarioTheory);

        _ = suite.Catalogs;

        Assert.True(suite.IsLoaded);
        Assert.False(suite.HasScenarioTheory);
    }

    [Fact]
    public void RelativeToConfig_IsWhereTheBuildOutputHasTheFile()
    {
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));

        Assert.Equal("Features/Users/x.feature.md", suite.RelativeToConfig(Path.Combine(_folder, "Features", "Users", "x.feature.md")));
        Assert.Equal("Features/y.feature.md", suite.RelativeToConfig(Path.Combine(Path.GetTempPath(), "Elsewhere", "Features", "y.feature.md")));
    }

    [Fact]
    public void TheBuildOutput_CanBeOverwritten_WhileTheCatalogsAreHeld()
    {
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));
        var catalogs = suite.Catalogs;

        // What a build does to its output: replace the assembly.
        File.Copy(typeof(BddSuiteTests).Assembly.Location, _built, overwrite: true);
        File.Delete(_built);

        Assert.NotEmpty(catalogs.Default.Patterns);
    }

    [Fact]
    public void Reload_UnloadsTheAssemblies_AndTheNextCatalogsAreNew()
    {
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));
        var context = LoadAndForget(suite);

        suite.Reload();
        for (var attempt = 0; attempt < 10 && context.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(context.IsAlive, "The suite's load context was not collected after Reload.");
        Assert.Contains(suite.Catalogs.Default.Patterns, pattern => pattern.Phrase == "I shelve {title}");
    }

    [Fact]
    public void Catalogs_BeforeABuild_SayToBuildFirst()
    {
        File.Delete(_built);
        using var suite = BddSuite.Open(Path.Combine(_folder, "bdd.config.md"));

        Assert.Null(suite.TryGetCatalogs(out var reason));
        Assert.Contains("Build the UAT project first", reason);
    }

    // Kept out of the test method, so no local there keeps the context alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndForget(BddSuite suite)
    {
        _ = suite.Catalogs;
        return suite.LoadContext!;
    }
}

public sealed class ShelfFixture
{
    public TestComposition Composition => throw new NotSupportedException("The workspace reads the fixture's type only.");
}

[BddStepDefinitions]
public sealed class ShelfSteps : BddStepDefinitionsBase
{
    [BddWhen("I shelve {title}")]
    public void Shelve(string title)
    {
    }
}
