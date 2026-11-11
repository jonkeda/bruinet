namespace Bruinet.Core.Tests;

public sealed class SolutionTests
{
    // Not product code: scratch projects of work in flight, and build output.
    private static readonly string[] SkippedFolders = [".git", ".my", ".vs", "bin", "obj", "node_modules", "TestResults"];

    // Products of their own that live in this repository for now, each with its own solution: Frainet
    // (Frainet/Frainet.slnx, its own global.json). Skipped at the root only.
    private static readonly string[] SeparateProducts = ["Frainet"];

    [Fact]
    public void EveryProjectInTheRepository_IsInBruinetSln()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);
        var solution = File.ReadAllText(Path.Combine(root, "Bruinet.sln")).Replace('\\', '/');

        var missing = Projects(root)
            .Where(path => !SeparateProducts.Any(product =>
                Path.GetRelativePath(root, path).StartsWith(product + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !solution.Contains($"\"{path}\"", StringComparison.OrdinalIgnoreCase))
            .Order()
            .ToList();

        Assert.True(missing.Count == 0, $"Add these projects to Bruinet.sln, in their area's folder: {string.Join(", ", missing)}");
    }

    // The projects under tests/ that no run takes, and why (review 08, finding 8).
    private static readonly Dictionary<string, string> NotRun = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bruinet.ControlContracts"] = "a library: the contracts, bound by the stacks' test projects",
        ["Bruinet.ControlContracts.Html"] = "a library: the Html bindings",
        ["Bruinet.ControlContracts.Html.Site"] = "the Html bindings' pages",
        ["Bruinet.Docs.Samples"] = "the docs' samples: compiled with the build, never run (DocsSamplesTests)",
        ["Bruinet.Uia.TestHost"] = "the app Bruinet.Uia.Tests drives",
        ["Bruinet.Scraper.TestSite"] = "the site Bruinet.Scraper.Tests scrapes",
        ["Bruinet.Maui.UITests"] = "a device suite: test-maui-headless.ps1 runs it on MauiHeadless",
        ["Bruinet.Maui.UITests.Mobile"] = "the device suite on Android or iOS: run-android-tests.ps1",
        ["Bruinet.NativeAndroid.UITests"] = "a device suite: a phone or emulator",
        ["Bruinet.Stride.UITests"] = "a game suite: the Stride sample running",
        ["Bruinet.Bdd.VisualStudio.UITests"] = "Visual Studio's own suite: an experimental instance",
        ["Bruinet.Presenter.Bdd.Tests"] = "drives the Presenter app; its cloud voice scenario reads this machine's secrets",
    };

    /// <summary>
    /// Every project under tests/ is run by coverage.ps1 (or its -Browser and -Desktop switches, or
    /// test-maui-headless.ps1), or named above with why not: a new project is not left out unnoticed (review 08,
    /// finding 8).
    /// </summary>
    [Fact]
    public void EveryTestProject_IsRun_OrNamedAsNotRun()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);
        var scripts = File.ReadAllText(Path.Combine(root, "tools", "Scripts", "coverage.ps1"))
                      + File.ReadAllText(Path.Combine(root, "tools", "Scripts", "test-maui-headless.ps1"));

        var unrun = Directory.GetDirectories(Path.Combine(root, "tests"))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => File.Exists(Path.Combine(root, "tests", name, name + ".csproj")))
            .Where(name => !NotRun.ContainsKey(name) && !scripts.Contains($"'{name}'", StringComparison.Ordinal))
            .Order()
            .ToList();

        Assert.True(unrun.Count == 0,
            $"No script runs these test projects: {string.Join(", ", unrun)}. Add each to coverage.ps1's list, or to NotRun with why.");
    }

    /// <summary>
    /// Every stack is on the call model (AD-010): the first model's project, <c>Bruinet.Core.Legacy</c>,
    /// was deleted when WPF, WinForms and Stride moved (2026-11-10), and nothing brings it back.
    /// </summary>
    [Fact]
    public void NoProject_UsesTheFirstModel()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);

        Assert.False(Directory.Exists(Path.Combine(root, "src", "Bruinet.Core.Legacy")), "Bruinet.Core.Legacy is back in src.");
        var offenders = new[] { "src", "tests", "tools", "samples" }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Where(project => File.ReadAllText(project).Contains("Bruinet.Core.Legacy", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, $"These projects reference Bruinet.Core.Legacy: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// MAUI's controls and containers ask the driver what the platform's tree is like
    /// (<c>IMauiDriver.OmitsOffscreenElements</c>, <c>ChildrenPath</c>, <c>AlertButtons</c> ...),
    /// as <c>ShellChrome</c> does; none branches on the platform or names a platform's element type.
    /// <c>ElementMatch</c> normalizes type names from every platform and is the one exception.
    /// </summary>
    [Fact]
    public void MauiControls_HoldNoPlatformBranchesOrPlatformNames()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);
        var pattern = new System.Text.RegularExpressions.Regex(@"MauiPlatform\.(Android|iOS|Windows)|XCUIElementType|android\.widget|android:id");
        string[] folders = ["src/Bruinet.Maui/Controls", "src/Bruinet.Maui/Containers", "src/Bruinet.Maui.CommunityToolkit/Controls"];

        var offenders = folders
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.EndsWith(".gen.cs", StringComparison.Ordinal) && Path.GetFileName(file) != "ElementMatch.cs")
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (file, line: line.Trim(), number: index + 1)))
            .Where(entry => !entry.line.StartsWith("//", StringComparison.Ordinal) && pattern.IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(root, entry.file)}:{entry.number}")
            .ToList();

        Assert.True(offenders.Count == 0, $"Ask the driver instead (IMauiDriver, 'What the platform's tree is like'): {string.Join(", ", offenders)}");
    }

    private static IEnumerable<string> Projects(string directory)
    {
        foreach (var project in Directory.EnumerateFiles(directory, "*.csproj"))
        {
            yield return project;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedFolders.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var project in Projects(child))
            {
                yield return project;
            }
        }
    }

    internal static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bruinet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
