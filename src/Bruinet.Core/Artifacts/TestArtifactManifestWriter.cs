using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Bruinet.Core.Artifacts;

public static class TestArtifactManifestWriter
{
    private static readonly object SyncRoot = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void RecordArtifact(
        string path,
        string kind,
        string name,
        string reason,
        Dictionary<string, string?>? metadata = null)
    {
        var provider = DefaultTestArtifactPathProvider.Create();
        RecordArtifact(provider, path, kind, name, reason, metadata);
    }

    public static void RecordArtifact(
        ITestArtifactPathProvider provider,
        string path,
        string kind,
        string name,
        string reason,
        Dictionary<string, string?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.EnsureDirectories();

        var fullPath = Path.GetFullPath(path);
        var manifestPath = Path.Combine(provider.RunDirectory, "manifest.json");
        var summaryPath = Path.Combine(provider.RunDirectory, "summary.md");

        lock (SyncRoot)
        {
            var manifest = LoadManifest(manifestPath, provider);
            var suite = manifest.Suites.FirstOrDefault(x => x.Name == provider.SuiteName);
            if (suite is null)
            {
                suite = new TestArtifactSuite { Name = provider.SuiteName };
                manifest.Suites.Add(suite);
            }

            suite.Artifacts.Add(new TestArtifactRecord
            {
                Kind = kind,
                Name = name,
                Path = GetManifestPath(provider.RunDirectory, fullPath),
                Reason = reason,
                Status = reason,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Metadata = metadata ?? []
            });

            manifest.UpdatedAtUtc = DateTimeOffset.UtcNow;

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.WriteAllText(summaryPath, BuildSummary(manifest));
        }
    }

    public static void RecordArtifact(
        string path,
        string kind,
        string name,
        string reason)
        => RecordArtifact(path, kind, name, reason, null);

    /// <summary>Reads a run's manifest; null when there is none or it cannot be read.</summary>
    /// <param name="runDirectory">The run's folder.</param>
    public static TestArtifactManifest? Read(string runDirectory)
    {
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        try
        {
            return File.Exists(manifestPath) ? JsonSerializer.Deserialize<TestArtifactManifest>(File.ReadAllText(manifestPath), JsonOptions) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Writes a changed manifest back, with its <c>summary.md</c>: after artifacts are removed from a run.</summary>
    /// <param name="runDirectory">The run's folder.</param>
    /// <param name="manifest">The manifest to write.</param>
    public static void Save(string runDirectory, TestArtifactManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        lock (SyncRoot)
        {
            File.WriteAllText(Path.Combine(runDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));
            File.WriteAllText(Path.Combine(runDirectory, "summary.md"), BuildSummary(manifest));
        }
    }

    private static TestArtifactManifest LoadManifest(
        string manifestPath,
        ITestArtifactPathProvider provider)
    {
        if (!File.Exists(manifestPath))
        {
            return CreateManifest(provider);
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<TestArtifactManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);

            if (manifest is null)
            {
                return CreateManifest(provider);
            }

            manifest.RunId = string.IsNullOrWhiteSpace(manifest.RunId)
                ? provider.RunId
                : manifest.RunId;
            manifest.RootDirectory = string.IsNullOrWhiteSpace(manifest.RootDirectory)
                ? provider.RunDirectory
                : manifest.RootDirectory;

            return manifest;
        }
        catch (JsonException)
        {
            return CreateManifest(provider);
        }
    }

    private static TestArtifactManifest CreateManifest(ITestArtifactPathProvider provider)
    {
        return new TestArtifactManifest
        {
            RunId = provider.RunId,
            RootDirectory = provider.RunDirectory
        };
    }

    private static string GetManifestPath(string runDirectory, string artifactPath)
    {
        try
        {
            return Path.GetRelativePath(runDirectory, artifactPath);
        }
        catch
        {
            return artifactPath;
        }
    }

    private static string BuildSummary(TestArtifactManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"# Test Artifacts: {manifest.RunId}");
        builder.AppendLine();

        foreach (var suite in manifest.Suites.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"## {suite.Name}");
            builder.AppendLine();

            foreach (var artifact in suite.Artifacts)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {artifact.Kind}: {artifact.Name} ({artifact.Reason}) - {artifact.Path}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
