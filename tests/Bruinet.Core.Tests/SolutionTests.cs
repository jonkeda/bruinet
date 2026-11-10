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
