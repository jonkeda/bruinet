using Bruinet.Bdd.Preview;
using Bruinet.Bdd.Workspace;
using Bruinet.Presenter.Services;

namespace Bruinet.Presenter.Bdd.Tests.Services;

public sealed class BddWorkspaceServiceTests
{
    public static TheoryData<string> SupportedTargets => new()
    {
        "MAUI",
        "WPF",
        "WINFORMS",
        "BLAZOR",
        "HTML",
        "STRIDE"
    };

    [Theory]
    [MemberData(nameof(SupportedTargets))]
    public void LoadFolder_AcceptsSupportedDotNetTargets(string target)
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appPath = Path.Combine(root, $"{target}.exe");
            File.WriteAllText(appPath, string.Empty);

            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | {{target}} |
                | Fixture | SampleFixture |
                | AppPath | {{appPath}} |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | {{typeof(BddWorkspaceServiceTests).Assembly.Location}} |
                """);

            var result = new BddWorkspaceService().LoadFolder(root);

            // SampleFixture does not exist, which is its own error; what this asserts is that
            // every supported target string is accepted and reported back.
            Assert.DoesNotContain(
                result.Config.Diagnostics,
                line => line.Contains("Runtime Target", StringComparison.Ordinal));
            Assert.Equal(target, result.Config.Target);
            Assert.Contains(target, result.Config.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadFolder_ConfiguredAppPathThatDoesNotExist_IsStillAnError()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                """
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | MAUI |
                | Fixture | Appium |
                | AppPath | bin/Debug/net10.0-windows/win10-x64/Nope.exe |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | Missing.Pages.dll |
                """);

            var result = new BddWorkspaceService().LoadFolder(root);

            Assert.Contains("App missing", result.Config.Summary);
            Assert.Contains(
                result.Diagnostics,
                line => line.Contains("Runtime AppPath was not found", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A config without AppPath is complete: the fixture owns the app. Only a configured
    /// path that does not exist is an error.
    /// </summary>
    [Fact]
    public void LoadFolder_WithoutAppPath_ReportsNoAppError()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                """
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | MAUI |
                | Fixture | Appium |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | Missing.Pages.dll |
                """);

            var result = new BddWorkspaceService().LoadFolder(root);

            Assert.Contains("App from fixture", result.Config.Summary);
            Assert.DoesNotContain(
                result.Diagnostics,
                line => line.Contains("AppPath", StringComparison.OrdinalIgnoreCase));

            // The missing Pages assembly is still an error; only the app stopped being one.
            Assert.True(result.Config.HasErrors);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Each file is checked as the gate and the IDEs check it: the same diagnostics as
    /// <see cref="BddFileAnalysis"/> with the suite's catalogs, and a <c>## Step:</c> call from a
    /// step file bound to its steps (AD-022).
    /// </summary>
    [Fact]
    public void LoadFolder_ChecksEachFileAsTheLanguageServerDoes()
    {
        var folder = RepositoryPath("samples", "Todo", "tests", "Bruinet.Samples.Todo.Uat");
        using var service = new BddWorkspaceService();
        using var suite = BddSuite.Open(BddFileFormat.FindConfig(folder));
        var catalogs = suite.TryGetCatalogs(out var reason);
        Assert.True(catalogs is not null, $"Build Bruinet.Samples.Todo.Uat first: {reason}");

        var result = service.LoadFolder(folder);

        Assert.NotEmpty(result.Files);
        foreach (var file in result.Files)
        {
            var analysis = BddFileAnalysis.Analyze(File.ReadAllText(file.FilePath), file.FilePath, catalogs);
            Assert.Equal(
                analysis.Diagnostics.Where(analysis.IsHere).Select(diagnostic => diagnostic.Code),
                file.Diagnostics.Select(line => line.Split(' ')[2]));
            var expected = file.FilePath.Contains($"{Path.DirectorySeparatorChar}ExpectedFailures{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"{file.Name}: Bind: {(expected ? "expected failure" : "ok")}", result.Diagnostics);
        }

        var callStep = result.Scenarios.SelectMany(scenario => scenario.Steps).First(step => step.CallText is not null);
        Assert.NotEqual("(unbound)", callStep.CommandId);
    }

    [Fact]
    public void LoadFolder_ExpectedFailureFile_IsReportedButNotAnError()
    {
        var folder = RepositoryPath("tests", "Bruinet.Maui.UITests");

        using var service = new BddWorkspaceService();
        var result = service.LoadFolder(folder);

        Assert.Contains("main-page-missing-control.feature.md: Bind: expected failure", result.Diagnostics);
        Assert.DoesNotContain(
            result.Diagnostics,
            line => line.Contains("missing-control", StringComparison.Ordinal) && line.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadFolder_NotBuilt_ParsesOnlyAndSaysWhy()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Features"));

        try
        {
            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                """
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | MAUI |
                | Fixture | MauiFixture |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | Missing.Pages.dll |
                """);
            File.WriteAllText(
                Path.Combine(root, "Features", "one.feature.md"),
                """
                # Feature: One

                ## Scenario: Taps

                ```gherkin
                When I tap Save
                ```
                """);

            using var service = new BddWorkspaceService();
            var result = service.LoadFolder(root);

            Assert.Contains("one.feature.md: Bind: not checked", result.Diagnostics);
            Assert.Contains(result.Diagnostics, line => line.StartsWith("Error: the steps cannot be checked", StringComparison.Ordinal));
            Assert.Equal("(unbound)", Assert.Single(Assert.Single(result.Scenarios).Steps).CommandId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory?.FullName ?? throw new DirectoryNotFoundException("The Bruinet root was not found."), .. parts]);
    }
}
