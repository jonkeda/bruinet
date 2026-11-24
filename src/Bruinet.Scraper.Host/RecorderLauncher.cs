using System.Diagnostics;
using Bruinet.Scraper.Record;

namespace Bruinet.Scraper.Host;

/// <summary>Opens the Recorder for the command line and the MCP server: asks the one that runs, or starts it.</summary>
public static class RecorderLauncher
{
    /// <summary>Where the Recorder's program is, when it is not beside this one: a path to it.</summary>
    public const string ProgramVariable = "BRUINET_RECORDER";

    /// <summary>How long a Recorder that was just started may take to answer.</summary>
    public static readonly TimeSpan StartLimit = TimeSpan.FromSeconds(60);

    // How often a Recorder that was just started is asked whether it is there yet.
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Opens a folder and an address in the Recorder: in the one that runs, else in one started for it. It opens
    /// not recording.
    /// </summary>
    /// <param name="folder">The Scraper project to save in; null leaves what the Recorder has.</param>
    /// <param name="url">The address; null leaves where it is, or where the project's settings left off.</param>
    /// <param name="account">Whose sign-in a Recorder that is started keeps; null for its own.</param>
    /// <param name="workingDirectory">Where a Recorder that is started runs.</param>
    /// <param name="cancellationToken">Stops waiting for it.</param>
    /// <returns>How the Recorder is, or why there is none.</returns>
    public static async Task<(RecorderStatus? Status, string? Problem)> OpenAsync(string? folder, string? url, string? account, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (await RecorderLink.AskAsync(new RecorderRequest("open", folder, url), cancellationToken).ConfigureAwait(false) is { } running)
            return (running, null);
        if (Program() is not { } program)
            return (null, $"the Recorder's program was not found. Build tools/Bruinet.Recorder, or set {ProgramVariable} to it.");

        var start = new ProcessStartInfo(program) { UseShellExecute = false, WorkingDirectory = folder ?? workingDirectory };
        foreach (var (name, value) in new[] { ("--folder", folder), ("--url", url), ("--as", account) })
        {
            if (value is not { Length: > 0 })
                continue;
            start.ArgumentList.Add(name);
            start.ArgumentList.Add(value);
        }

        using var started = Process.Start(start);
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < StartLimit)
        {
            if (await RecorderLink.AskAsync(new RecorderRequest("status"), cancellationToken).ConfigureAwait(false) is { } status)
                return (status, null);
            await Task.Delay(Poll, cancellationToken).ConfigureAwait(false);
        }

        return (null, $"the Recorder was started and did not answer within {StartLimit.TotalSeconds:0} s.");
    }

    /// <summary>The Recorder's program: where the variable says, else beside this program, else as this repository builds it; null when none.</summary>
    public static string? Program()
    {
        var name = OperatingSystem.IsWindows() ? "Bruinet.Recorder.exe" : "Bruinet.Recorder";
        if (Environment.GetEnvironmentVariable(ProgramVariable) is { Length: > 0 } given)
            return File.Exists(given) ? given : null;
        var beside = Path.Combine(AppContext.BaseDirectory, "recorder", name);
        if (File.Exists(beside))
            return beside;
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var built = Path.Combine(folder.FullName, "tools", "Bruinet.Recorder", "bin");
            if (Directory.Exists(built))
                return Directory.EnumerateFiles(built, name, SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }

        return null;
    }
}
