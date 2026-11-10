using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Bruinet.Bdd.Runner;

/// <summary>
/// A copy of a suite's build output a runner starts from, so the runner holds none of the output's
/// files and a build goes through while it waits (AD-025). A file the newest earlier
/// copy has unchanged is a hard link to it, so a copy after a build costs little. Each host process
/// has its own copies, and removes them itself (<see cref="Clean"/>): a copy a runner runs from is
/// never removed, since only the files it has not loaded yet would go.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddShadowCopy
{
    /// <summary>Where the copies go: a folder per output, a copy per build.</summary>
    public static string DefaultRoot => Path.Combine(Path.GetTempPath(), "Bruinet", "runners");

    /// <summary>What the output is now: its files' count, sizes and newest write; a build changes it.</summary>
    /// <param name="output">The suite's output folder.</param>
    public static string Fingerprint(string output)
    {
        long count = 0, length = 0, newest = 0;
        foreach (var file in new DirectoryInfo(output).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            count++;
            length += file.Length;
            newest = Math.Max(newest, file.LastWriteTimeUtc.Ticks);
        }

        return $"{count}-{length}-{newest}";
    }

    /// <summary>Who owns copies: this process, and a pool of it (<c>1234-2</c>); the process id comes first, so an ended one's are known.</summary>
    public static string ProcessOwner => Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    /// <summary>A new folder for a copy of the output under <paramref name="root"/>, of <paramref name="owner"/>; not made yet.</summary>
    /// <param name="output">The suite's output folder.</param>
    /// <param name="root">Where the copies go; <see cref="DefaultRoot"/> by default.</param>
    /// <param name="owner">Whose copies: <see cref="ProcessOwner"/> by default.</param>
    public static string NewFolder(string output, string? root = null, string? owner = null)
        => Path.Combine(Mine(Path.GetFullPath(output), root, owner), $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}");

    /// <summary>Copies the output into a folder; a file the owner's newest earlier copy has unchanged is linked to it.</summary>
    /// <param name="output">The suite's output folder.</param>
    /// <param name="copy">The copy's folder (<see cref="NewFolder"/>).</param>
    /// <returns>The copy's folder.</returns>
    public static string Make(string output, string copy)
    {
        output = Path.GetFullPath(output);
        copy = Path.GetFullPath(copy);
        var parent = Path.GetDirectoryName(copy)!;
        Directory.CreateDirectory(parent);
        var earlier = new DirectoryInfo(parent).EnumerateDirectories()
            .Where(folder => !folder.FullName.Equals(copy, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(folder => folder.CreationTimeUtc).Select(folder => folder.FullName).ToList();
        foreach (var file in new DirectoryInfo(output).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(output, file.FullName);
            var target = Path.Combine(copy, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (earlier.Count == 0 || !TryLink(Path.Combine(earlier[0], relative), file, target))
            {
                file.CopyTo(target);
                File.SetLastWriteTimeUtc(target, file.LastWriteTimeUtc);
            }
        }

        return copy;
    }

    /// <summary>Removes the owner's copies of an output but those runners use, and every copy, of any output, of a process that has ended.</summary>
    /// <param name="output">The suite's output folder.</param>
    /// <param name="inUse">The copies runners run from.</param>
    /// <param name="root">Where the copies go; <see cref="DefaultRoot"/> by default.</param>
    /// <param name="owner">Whose copies: <see cref="ProcessOwner"/> by default.</param>
    public static void Clean(string output, IEnumerable<string> inUse, string? root = null, string? owner = null)
    {
        var mine = Mine(Path.GetFullPath(output), root, owner);
        var keep = inUse.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(mine))
        {
            foreach (var copy in Directory.EnumerateDirectories(mine).Where(copy => !keep.Contains(Path.GetFullPath(copy))))
            {
                TryDelete(copy);
            }
        }

        CleanEnded(root);
    }

    /// <summary>
    /// Removes every copy, of any output, of a process that has ended. Run as a copy is made too: a host that is killed (an MCP
    /// session closing) never reaches <see cref="Clean"/>, and an output that never runs again (a checkout since removed) has
    /// no other clean.
    /// </summary>
    /// <param name="root">Where the copies go; <see cref="DefaultRoot"/> by default.</param>
    public static void CleanEnded(string? root = null)
    {
        var all = root ?? DefaultRoot;
        foreach (var outputFolder in Directory.Exists(all) ? Directory.EnumerateDirectories(all) : [])
        {
            foreach (var other in Directory.EnumerateDirectories(outputFolder))
            {
                if (int.TryParse(Path.GetFileName(other).Split('-')[0], out var process) && process != Environment.ProcessId && !Running(process))
                {
                    TryDelete(other);
                }
            }

            if (!Directory.EnumerateFileSystemEntries(outputFolder).Any())
            {
                TryDelete(outputFolder);
            }
        }
    }

    // The same output always gets the same folder name, short enough for long paths below it; then the owner's.
    private static string Mine(string output, string? root, string? owner)
        => Path.Combine(root ?? DefaultRoot, Key(output), owner ?? ProcessOwner);

    private static bool Running(int process)
    {
        try
        {
            using var found = System.Diagnostics.Process.GetProcessById(process);
            return !found.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The start of a runner from a copy: its Pages assembly there, and the copy as its working directory unless one is set.</summary>
    /// <param name="start">The start from the output.</param>
    /// <param name="copy">The copy's folder.</param>
    public static BddRunnerStart Into(BddRunnerStart start, string copy)
    {
        ArgumentNullException.ThrowIfNull(start);
        var pages = Path.GetFullPath(start.PagesAssembly);

        // A suite finds its repository's files from where it was built (AssemblyFile.FolderOf), not from the copy.
        var environment = new Dictionary<string, string>(start.Environment ?? new Dictionary<string, string>())
        {
            [Bruinet.Core.Composition.AssemblyFile.BuiltOutputEnvironmentVariable] = Path.GetDirectoryName(pages)!,
        };
        return start with
        {
            PagesAssembly = Path.Combine(copy, Path.GetFileName(pages)),
            WorkingDirectory = start.WorkingDirectory ?? copy,
            Environment = environment,
        };
    }

    private static string Key(string output)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(output.ToUpperInvariant())))[..12];

    private static bool TryLink(string earlier, FileInfo file, string target)
    {
        var previous = new FileInfo(earlier);
        if (!previous.Exists || previous.Length != file.Length || previous.LastWriteTimeUtc != file.LastWriteTimeUtc || !OperatingSystem.IsWindows())
        {
            return false;
        }

        return CreateHardLink(target, earlier, IntPtr.Zero);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // In use by a runner that has not ended yet.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
