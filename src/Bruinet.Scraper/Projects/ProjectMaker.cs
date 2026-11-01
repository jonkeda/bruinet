using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// Makes a scraped UI test project: its <c>.csproj</c> referencing Bruinet, its <c>scraper.config.md</c>,
/// and an empty <c>Pages/</c>. Adding it to the solution is the IDE's.
/// </summary>
public static partial class ProjectMaker
{
    /// <summary>What a new project gets when the form leaves it as proposed.</summary>
    /// <param name="solutionDir">The solution's folder.</param>
    /// <param name="name">The project's name, for its folder.</param>
    public static NewProjectDefaults Defaults(string solutionDir, string name)
    {
        var inBruinet = IsBruinetRepository(solutionDir);
        var folder = inBruinet ? Path.Combine("tests", name) : TestsParent(solutionDir) is { } parent ? Path.Combine(parent, name) : name;
        return new NewProjectDefaults(folder, inBruinet ? BruinetReferences.Projects : BruinetReferences.Packages, Version());
    }

    /// <summary>What is wrong with a request, by field (<c>name</c>, <c>folder</c>, <c>platform</c>, <c>startUrl</c>, <c>testIds</c>); empty when nothing is.</summary>
    /// <param name="solutionDir">The solution's folder.</param>
    /// <param name="request">The request.</param>
    public static IReadOnlyDictionary<string, string> Problems(string solutionDir, NewProject request)
    {
        var problems = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!ProjectName().IsMatch(request.Name))
            problems["name"] = "A project name: letters, digits, dots, dashes and underscores, starting with a letter.";
        else if (TakenNames(solutionDir).Contains(request.Name, StringComparer.OrdinalIgnoreCase))
            problems["name"] = $"{request.Name} is a project here already.";
        var folder = FullFolder(solutionDir, request.Folder);
        if (folder is null)
            problems["folder"] = "A folder under the solution's folder.";
        else if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            problems["folder"] = $"{request.Folder} is there already and not empty.";
        if (!Uri.TryCreate(request.StartUrl, UriKind.Absolute, out var start) || start.Scheme is not ("http" or "https"))
            problems["startUrl"] = "An http or https address to start at.";
        if (request.Platform is not ("html" or "blazor"))
            problems["platform"] = "html or blazor.";
        if (request.TestIds.Any(t => !TestId().IsMatch(t)))
            problems["testIds"] = "Attribute names, such as data-testid, separated by commas.";
        return problems;
    }

    /// <summary>Writes the project; its folder.</summary>
    /// <param name="solutionDir">The solution's folder.</param>
    /// <param name="request">The request, without <see cref="Problems"/>.</param>
    /// <exception cref="ArgumentException">The request has problems.</exception>
    public static string Create(string solutionDir, NewProject request)
    {
        var problems = Problems(solutionDir, request);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Values));
        var folder = FullFolder(solutionDir, request.Folder)!;
        Directory.CreateDirectory(Path.Combine(folder, "Pages"));
        File.WriteAllText(Path.Combine(folder, request.Name + ".csproj"), ProjectFile(solutionDir, folder, request));
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), Config(request));
        return folder;
    }

    private static string ProjectFile(string solutionDir, string folder, NewProject request)
    {
        var libraries = request.Platform == "blazor" ? new[] { "Bruinet.Html", "Bruinet.Blazor" } : ["Bruinet.Html"];
        var references = request.References == BruinetReferences.Projects
            ? libraries.Select(l => $"    <ProjectReference Include=\"{Path.GetRelativePath(folder, Path.Combine(solutionDir, "src", l, l + ".csproj"))}\" />")
            : libraries.Select(l => $"    <PackageReference Include=\"{l}\" Version=\"{Version()}\" />");
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
            {string.Join("\n", references)}
              </ItemGroup>
            </Project>

            """;
    }

    private static string Config(NewProject request)
    {
        var md = new StringBuilder();
        md.Append("# Scraper Config\n\n## App\n\n| Field | Value |\n| --- | --- |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Platform | {request.Platform} |\n| StartUrl | {request.StartUrl} |\n| Namespace | {request.Name}.Pages |\n| Pages | Pages |\n");
        if (request.TestIds.Count > 0)
            md.Append("\n## Test ids\n\n| Attribute |\n| --- |\n").Append(string.Concat(request.TestIds.Select(t => $"| {t} |\n")));
        // Explore runs against localhost by default; any other host is named, so the start can be explored.
        var host = new Uri(request.StartUrl).Host;
        if (host is not ("localhost" or "127.0.0.1"))
            md.Append(CultureInfo.InvariantCulture, $"\n## Explore hosts\n\n| Host |\n| --- |\n| localhost |\n| 127.0.0.1 |\n| {host} |\n");
        md.Append(ScraperConfigWriter.Tests(TestTarget.Xunit)); // a new project is no UAT suite
        return md.ToString();
    }

    // The projects under the solution's folder, by name; build output and the loop's copies left out.
    private static IEnumerable<string> TakenNames(string solutionDir) =>
        Directory.EnumerateFiles(solutionDir, "*.csproj", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4 })
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "TestResults" or "node_modules"))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>();

    private static string? FullFolder(string solutionDir, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;
        var root = Path.GetFullPath(solutionDir);
        var full = Path.GetFullPath(Path.Combine(root, folder));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    // This repository: Bruinet's own sources beside the solution, referenced as projects.
    private static bool IsBruinetRepository(string solutionDir) =>
        File.Exists(Path.Combine(solutionDir, "src", "Bruinet.Html", "Bruinet.Html.csproj"));

    // Where the solution's other test projects are, relative to it: a new one goes beside them.
    private static string? TestsParent(string solutionDir)
    {
        var project = Directory.EnumerateFiles(solutionDir, "*Tests.csproj", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 })
            .FirstOrDefault(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        var parent = project is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(project));
        return parent is null ? null : Path.GetRelativePath(solutionDir, parent) is var relative && relative != "." ? relative : null;
    }

    private static string Version() =>
        typeof(ProjectMaker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0";

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9._-]*$")]
    private static partial Regex ProjectName();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]*$")]
    private static partial Regex TestId();
}

/// <summary>How a new project references Bruinet.</summary>
public enum BruinetReferences
{
    /// <summary>Project references to <c>src/</c>: inside Bruinet's repository.</summary>
    Projects,

    /// <summary>NuGet packages at the running Bruinet's version.</summary>
    Packages,
}

/// <summary>A scraped project to make.</summary>
/// <param name="Name">The project's name, and its namespace's start.</param>
/// <param name="Folder">Its folder, relative to the solution's.</param>
/// <param name="Platform"><c>html</c> or <c>blazor</c>.</param>
/// <param name="StartUrl">Where recording and Explore start.</param>
/// <param name="TestIds">The attributes that are test ids, preferred first.</param>
/// <param name="References">How it references Bruinet.</param>
public sealed record NewProject(string Name, string Folder, string Platform, string StartUrl, IReadOnlyList<string> TestIds, BruinetReferences References);

/// <summary>What the form proposes: the folder, how to reference Bruinet, and Bruinet's version.</summary>
public sealed record NewProjectDefaults(string Folder, BruinetReferences References, string Version);
