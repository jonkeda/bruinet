using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// A UI test project that is scraped: the folder with <c>scraper.config.md</c> and the project file. Its
/// recordings are in <c>Recordings/</c>, one folder per session.
/// </summary>
public sealed partial class ScrapeProject
{
    /// <summary>The folder recordings are kept in, in the project.</summary>
    public const string RecordingsFolderName = "Recordings";

    private ScrapeProject(string folder, string name, ScraperConfig config)
    {
        Folder = folder;
        Name = name;
        Config = config;
    }

    /// <summary>The project's folder.</summary>
    public string Folder { get; }

    /// <summary>The project's name: its project file's name, or the folder's when it has none.</summary>
    public string Name { get; }

    /// <summary>Its settings.</summary>
    public ScraperConfig Config { get; }

    /// <summary>The UAT suite's config beside the scraped one: its presence makes a scenario the default draft.</summary>
    public const string UatConfigFileName = "uat.config.md";

    /// <summary>What a test recording is drafted as by default: the config's <c>## Tests</c> Target, else a scenario when the project is a UAT suite, else an xUnit test.</summary>
    public TestTarget TestTarget => Config.Tests.Target ?? DefaultTestTarget(Folder);

    /// <summary>The target written into a new config: a scenario when the folder has a <c>uat.config.md</c>, otherwise an xUnit test.</summary>
    public static TestTarget DefaultTestTarget(string folder) =>
        File.Exists(Path.Combine(folder, UatConfigFileName)) ? TestTarget.Scenario : TestTarget.Xunit;

    /// <summary>The namespace generated code goes in.</summary>
    public string Namespace => Config.Namespace.Length > 0 ? Config.Namespace : Name;

    /// <summary>The folder and namespace a page's object is drafted into: its module's (<c>## Modules</c>), else the project's.</summary>
    /// <param name="pageKey">The page's key, such as <c>/invoice#/</c>.</param>
    public (string Folder, string Namespace) PagesFor(string pageKey)
        => Config.Modules.FirstOrDefault(m => pageKey.StartsWith(m.Path, StringComparison.OrdinalIgnoreCase)) is { } module
            ? (module.Pages, module.Namespace)
            : (Config.PagesFolder, Namespace);

    /// <summary>Where its recordings are.</summary>
    public string RecordingsFolder => Path.Combine(Folder, RecordingsFolderName);

    /// <summary>Opens the project in <paramref name="folder"/>.</summary>
    /// <exception cref="FileNotFoundException">The folder has no <c>scraper.config.md</c>.</exception>
    public static ScrapeProject Open(string folder)
    {
        folder = Path.GetFullPath(folder);
        var configPath = Path.Combine(folder, ScraperConfig.FileName);
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"{folder} is not a scraped project: it has no {ScraperConfig.FileName}.", configPath);
        var projectFile = Directory.EnumerateFiles(folder, "*.csproj").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
        var name = projectFile is null ? Path.GetFileName(folder) : Path.GetFileNameWithoutExtension(projectFile);
        return new ScrapeProject(folder, name, ScraperConfig.Read(configPath));
    }

    /// <summary>The project that holds <paramref name="path"/> (a file or folder in it), or null.</summary>
    public static ScrapeProject? Find(string path)
    {
        var folder = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path));
        for (; folder is not null; folder = Path.GetDirectoryName(folder))
            if (File.Exists(Path.Combine(folder, ScraperConfig.FileName)))
                return Open(folder);
        return null;
    }

    /// <summary>
    /// The scraped projects of a solution folder: those the <c>.sln</c> or <c>.slnx</c> in it lists (which
    /// may be anywhere), and those under it. Build output, test results (the loop's copies of projects)
    /// and hidden folders are skipped.
    /// </summary>
    public static IReadOnlyList<ScrapeProject> FindAll(string root)
    {
        root = Path.GetFullPath(root);
        var folders = new HashSet<string>(SolutionProjectFolders(root), StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            folders.Add(folder);
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(child);
                if (SkippedFolders.Contains(name) || name.StartsWith('.'))
                    continue;
                pending.Push(child);
            }
        }
        return folders.Where(f => File.Exists(Path.Combine(f, ScraperConfig.FileName)))
            .Select(Open)
            .OrderBy(p => p.Folder, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Build output, packages, recordings, and test results: the loop copies whole projects there.
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", RecordingsFolderName, "TestResults", "artifacts",
    };

    // The folders of the projects the solutions directly in a folder list.
    private static IEnumerable<string> SolutionProjectFolders(string root)
    {
        var solutions = Directory.EnumerateFiles(root, "*.sln").Concat(Directory.EnumerateFiles(root, "*.slnx"));
        foreach (var solution in solutions)
        {
            var paths = solution.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                ? System.Xml.Linq.XDocument.Load(solution).Descendants("Project").Select(p => (string?)p.Attribute("Path"))
                : SolutionProject().Matches(File.ReadAllText(solution)).Select(m => (string?)m.Groups["path"].Value);
            foreach (var path in paths.OfType<string>())
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, path.Replace('\\', Path.DirectorySeparatorChar))));
                if (folder is not null && Directory.Exists(folder))
                    yield return folder;
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*"",\s*""(?<path>[^""]+\.(?:cs|vb|fs)proj)""", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex SolutionProject();

    /// <summary>Whether a location is one of the app's own pages: on the start URL's host or an alias's.</summary>
    public bool IsOwnPage(string location)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri))
            return false;
        return new[] { Config.StartUrl }.Concat(Config.Aliases)
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var a) ? a : null)
            .Any(a => a is not null && string.Equals(a.Authority, uri.Authority, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The recording sessions, oldest first.</summary>
    public IReadOnlyList<RecordingSession> Sessions() =>
        Directory.Exists(RecordingsFolder)
            ? Directory.EnumerateDirectories(RecordingsFolder)
                .Where(d => File.Exists(Path.Combine(d, RecordingSession.FileName)))
                .OrderBy(d => d, StringComparer.Ordinal)
                .Select(d => RecordingSession.Open(d, Config.Masks))
                .ToArray()
            : [];

    /// <summary>The session with this id (its folder name), or null.</summary>
    public RecordingSession? Session(string id)
    {
        var folder = Path.Combine(RecordingsFolder, id);
        return id.Length > 0 && Path.GetFileName(id) == id && File.Exists(Path.Combine(folder, RecordingSession.FileName))
            ? RecordingSession.Open(folder, Config.Masks)
            : null;
    }

    /// <summary>Starts a session: a new folder named after the day and <paramref name="name"/>.</summary>
    public RecordingSession StartSession(string name, DateTimeOffset now)
    {
        var baseName = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-" + Slug.Of(name, "recording");
        var folder = Path.Combine(RecordingsFolder, baseName);
        for (var i = 2; Directory.Exists(folder); i++)
            folder = Path.Combine(RecordingsFolder, $"{baseName}-{i}");
        return RecordingSession.Create(folder, name, Config.Masks);
    }

    /// <summary>What is recorded, by page, across all sessions.</summary>
    public RecordingIndex Index() => RecordingIndex.Build(Sessions());

    /// <summary>
    /// Where the recording browser keeps its login and cookies for this project: under the user's local
    /// application data, never in the repository.
    /// </summary>
    public string ProfileFolder
    {
        get
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Folder.ToLowerInvariant())))[..12];
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bruinet", "Scraper", "profiles", $"{Name}-{hash}");
        }
    }
}
