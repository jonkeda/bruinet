using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bruinet.Core.Tests;

/// <summary>
/// The invariants (.docs/invariants.md) that the projects and sources show without running
/// anything, checked on every build (AD-033). A reference is followed through the project
/// references it brings, since a type can only be used where its assembly is referenced.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Regex PlatformPackage = new(
        @"^(FlaUI|Appium|Microsoft\.Playwright|Microsoft\.Maui|bunit|Selenium|Stride|AngleSharp|Xamarin|CommunityToolkit\.Maui)",
        RegexOptions.IgnoreCase);

    private static readonly Regex UiProject = new(
        @"^Bruinet\.(Uat|Maui.*|Html.*|Blazor|Wpf|WinForms|NativeAndroid|Stride.*|Appium|Uia\.Contracts|Presenter)$");

    /// <summary>AD-001: no platform element type in Bruinet.Core.</summary>
    [Fact]
    public void Core_ReferencesNoPlatform()
    {
        var (projects, packages, properties) = Closure("src/Bruinet.Core/Bruinet.Core.csproj");

        var offenders = packages.Where(package => PlatformPackage.IsMatch(package))
            .Concat(projects.Where(project => UiProject.IsMatch(project)))
            .Concat(properties.Where(property => property is "UseWPF" or "UseWindowsForms" or "UseMaui"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Bruinet.Core must stay platform-neutral (AD-001); it reaches: {string.Join(", ", offenders)}");
    }

    /// <summary>AD-021: Bruinet.Bdd knows no UI, and no xUnit.</summary>
    [Fact]
    public void Bdd_ReferencesNoUiAndNoXunit()
    {
        var (projects, packages, _) = Closure("src/Bruinet.Bdd/Bruinet.Bdd.csproj");

        var offenders = projects.Where(project => UiProject.IsMatch(project))
            .Concat(packages.Where(package => PlatformPackage.IsMatch(package) || package.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(offenders.Count == 0, $"Bruinet.Bdd knows no UI and no xUnit (AD-021); it reaches: {string.Join(", ", offenders)}");
    }

    /// <summary>AD-026, R4: Bruinet.TestData depends on nothing.</summary>
    [Fact]
    public void TestData_DependsOnNothing()
    {
        var (projects, packages, _) = Closure("src/Bruinet.TestData/Bruinet.TestData.csproj");

        Assert.True(projects.Count + packages.Count == 0,
            $"Bruinet.TestData has no dependency at all (AD-026); it has: {string.Join(", ", projects.Concat(packages))}");
    }

    /// <summary>AD-021: only Bruinet.Uat has Uat names; the engine, the Presenter and the tools have Bdd names.</summary>
    [Fact]
    public void OnlyBruinetUat_DeclaresUatNames()
    {
        var root = Root();
        var declaration = new Regex(@"^\s*(public|internal|private|protected)?\s*(sealed |static |abstract |partial |readonly )*(class|record|interface|enum|struct) (\w*Uat\w*)");
        string[] allowed = ["UatPlatformsAttribute"];

        var offenders = new[] { "src", "tools" }
            .SelectMany(folder => Sources(Path.Combine(root, folder)))
            .Where(file => !Relative(root, file).StartsWith("src/Bruinet.Uat/", StringComparison.Ordinal))
            .SelectMany(file => Lines(root, file))
            .Select(entry => (entry.where, match: declaration.Match(entry.line)))
            .Where(entry => entry.match.Success && !allowed.Contains(entry.match.Groups[4].Value))
            .Select(entry => entry.where)
            .Concat(new[] { "src", "tools" }
                .SelectMany(folder => Sources(Path.Combine(root, folder)))
                .Where(file => !Relative(root, file).StartsWith("src/Bruinet.Uat/", StringComparison.Ordinal))
                .Where(file => Path.GetFileName(file).Contains("Uat", StringComparison.Ordinal)
                    && !allowed.Any(name => Path.GetFileName(file).StartsWith(name, StringComparison.Ordinal)))
                .Select(file => Relative(root, file)))
            .ToList();

        Assert.True(offenders.Count == 0, $"Name these Bdd, not Uat (AD-021): {string.Join(", ", offenders)}");
    }

    /// <summary>AD-005: MAUI on Windows, and the FlaUI layer it shares with WPF and WinForms, use no physical input.</summary>
    [Fact]
    public void MauiFlaUI_UsesNoPhysicalInput()
    {
        var root = Root();
        var input = new Regex(@"FlaUI\.Core\.Input|\bMouse\.|\bKeyboard\.|Clipboard|SendInput|SetForegroundWindow|SetCursorPos");
        // The quiet window hands the foreground back to the person's window; it takes nothing.
        string[] allowed = ["src/Bruinet.FlaUI/Windowing/QuietWindow.cs", "src/Bruinet.FlaUI/Windowing/NativeMethods.cs"];

        var offenders = new[] { "Bruinet.Maui.FlaUI", "Bruinet.FlaUI" }
            .SelectMany(project => Sources(Path.Combine(root, "src", project)))
            .Where(file => !allowed.Contains(Relative(root, file)))
            .SelectMany(file => Lines(root, file))
            .Where(entry => input.IsMatch(entry.line))
            .Select(entry => entry.where)
            .ToList();

        Assert.True(offenders.Count == 0, $"No physical input in Bruinet.Maui.FlaUI or Bruinet.FlaUI (AD-005): {string.Join(", ", offenders)}");
    }

    /// <summary>AD-013: the call-model controls have MAUI's shape, with no async members.</summary>
    [Fact]
    public void CallModelControls_HaveNoAsyncMembers()
    {
        var root = Root();
        var asyncMember = new Regex(@"^\s*public\b[^(=;]*\b(Task|ValueTask)\b|^\s*public\s+(\w+\s+)*async\b");
        string[] folders = ["src/Bruinet.Maui/Controls", "src/Bruinet.Html/Controls", "src/Bruinet.NativeAndroid/Controls"];

        var offenders = folders
            .SelectMany(folder => Sources(Path.Combine(root, folder)))
            .SelectMany(file => Lines(root, file))
            .Where(entry => asyncMember.IsMatch(entry.line))
            .Select(entry => entry.where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Controls have no async members (AD-013): {string.Join(", ", offenders)}");
    }

    private static string Root()
    {
        var root = SolutionTests.FindRepositoryRoot();
        Assert.NotNull(root);
        return root;
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static IEnumerable<string> Sources(string folder)
        => Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            : [];

    private static IEnumerable<(string where, string line)> Lines(string root, string file)
        => File.ReadLines(file)
            .Select((line, index) => (where: $"{Relative(root, file)}:{index + 1}", line))
            .Where(entry => !entry.line.TrimStart().StartsWith("//", StringComparison.Ordinal));

    /// <summary>The projects, packages and Use* properties a project brings in, through its project references.</summary>
    private static (List<string> Projects, List<string> Packages, List<string> Properties) Closure(string project)
    {
        var root = Root();
        var projects = new List<string>();
        var packages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = new SortedSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([Path.GetFullPath(Path.Combine(root, project))]);
        var first = true;

        while (pending.Count > 0)
        {
            var path = pending.Pop();
            if (!seen.Add(path))
            {
                continue;
            }

            var isRoot = first;
            if (!isRoot)
            {
                projects.Add(Path.GetFileNameWithoutExtension(path));
            }

            first = false;
            var xml = XDocument.Load(path);
            foreach (var reference in xml.Descendants("ProjectReference"))
            {
                var include = (string?)reference.Attribute("Include");
                if (include is not null && (string?)reference.Attribute("OutputItemType") != "Analyzer")
                {
                    pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include.Replace('\\', Path.DirectorySeparatorChar))));
                }
            }

            foreach (var package in xml.Descendants("PackageReference"))
            {
                // A referenced project's private package (PrivateAssets="all") does not flow to its users.
                var isPrivate = string.Equals((string?)package.Attribute("PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase);
                if ((string?)package.Attribute("Include") is { } name && (isRoot || !isPrivate))
                {
                    packages.Add(name);
                }
            }

            foreach (var property in xml.Descendants().Where(element => element.Name.LocalName.StartsWith("Use", StringComparison.Ordinal)
                         && string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
            {
                properties.Add(property.Name.LocalName);
            }
        }

        return (projects, packages.ToList(), properties.ToList());
    }
}
