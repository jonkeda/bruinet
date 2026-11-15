using System.Xml.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Bruinet.Bdd.Workspace;

// The feature and step files a project links into its Features folder from another project:
// <None Include="..\Other\Features\**\*.feature.md" Link="Features\..." />. Read from the csproj, so
// it works before a build; an item whose paths use MSBuild properties is not followed.
internal static class BddLinkedFiles
{
    public static IReadOnlyList<string> Of(string projectDirectory)
    {
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (var csproj in Directory.EnumerateFiles(projectDirectory, "*.csproj"))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(csproj);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var item in document.Descendants().Where(element => element.Parent?.Name.LocalName == "ItemGroup"))
            {
                var link = item.Attribute("Link")?.Value;
                var include = item.Attribute("Include")?.Value;
                if (include is null || link is null || !IsInFeatures(link))
                {
                    continue;
                }

                var excluded = Expand(projectDirectory, item.Attribute("Exclude")?.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                files.UnionWith(Expand(projectDirectory, include)
                    .Where(file => !excluded.Contains(file))
                    .Where(file => BddFileFormat.IsScenarioFile(file) ||
                                   file.EndsWith(BddFileFormat.StepsExtension, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return files.Order(StringComparer.Ordinal).ToList();
    }

    private static bool IsInFeatures(string link)
        => link.Replace('/', '\\').StartsWith(BddFileFormat.FeatureFolder + "\\", StringComparison.OrdinalIgnoreCase);

    // Each ';'-separated pattern from the folder before its first wildcard, so '..' segments work.
    private static IEnumerable<string> Expand(string projectDirectory, string? patterns)
    {
        foreach (var pattern in (patterns ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (pattern.Contains("$(", StringComparison.Ordinal) || pattern.Contains("@(", StringComparison.Ordinal))
            {
                continue;
            }

            var segments = pattern.Replace('/', '\\').Split('\\');
            var fixedCount = segments.TakeWhile(segment => !segment.Contains('*') && !segment.Contains('?')).Count();
            if (fixedCount == segments.Length)
            {
                var path = Path.GetFullPath(Path.Combine(projectDirectory, pattern));
                if (File.Exists(path))
                {
                    yield return path;
                }

                continue;
            }

            var root = Path.GetFullPath(Path.Combine([projectDirectory, .. segments[..fixedCount]]));
            if (!Directory.Exists(root))
            {
                continue;
            }

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(string.Join('/', segments[fixedCount..]));
            foreach (var file in matcher.GetResultsInFullPath(root))
            {
                yield return Path.GetFullPath(file);
            }
        }
    }
}
