namespace Bruinet.Core.Artifacts;

public interface ITestArtifactPathProvider
{
    string RootDirectory { get; }

    string RunId { get; }

    string RunDirectory { get; }

    string SuiteName { get; }

    string SuiteDirectory { get; }

    string RunnerDirectory { get; }

    string LogsDirectory { get; }

    string ScreenshotsDirectory { get; }

    /// <summary>The old name of <see cref="BddDirectory"/>.</summary>
    [Obsolete("Use BddDirectory (AD-040).")]
    string UatDirectory { get; }

    /// <summary>Where the suite's scenario results, traces and reports go: <c>suites/&lt;suite&gt;/bdd</c>.</summary>
#pragma warning disable CS0618 // An implementer from before AD-040 has only UatDirectory.
    string BddDirectory => UatDirectory;
#pragma warning restore CS0618

    string CoverageDirectory { get; }

    string TracesDirectory { get; }

    string VideosDirectory { get; }

    string DownloadsDirectory { get; }

    string SnapshotsDirectory { get; }

    string AttachmentsDirectory { get; }

    void EnsureDirectories();
}
