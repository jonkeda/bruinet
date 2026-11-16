using System.Diagnostics;

// The spec commands moved to Lirenet, the spec tool. For one release frainet still takes them, and runs lirenet:
// frainet check ...  is  lirenet check ...
// frainet trace ...  is  lirenet trace ...
// Frainet reaches Lirenet as a command, never as code: no project reference crosses a tool's folder.
return args switch
{
    ["check" or "trace", ..] => Forward(args),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage: frainet check ...   moved: lirenet check <spec.md or folder>...");
    Console.Error.WriteLine("       frainet trace ...   moved: lirenet trace <spec.md> <tests folder>... [--runnable] [--results <file.trx or folder>...]");
    return 2;
}

static int Forward(string[] args)
{
    Console.Error.WriteLine($"frainet {args[0]} is lirenet {args[0]} now; frainet runs it for one more release.");
    var start = Lirenet();
    if (start is null)
    {
        Console.Error.WriteLine("lirenet was not found: not beside frainet, not in Lirenet/src/Lirenet.Cli above it. Build Lirenet, or set LIRENET to lirenet.dll.");
        return 2;
    }

    foreach (var arg in args)
    {
        start.ArgumentList.Add(arg);
    }

    // Not redirected: lirenet writes to this process's output and error as its own.
    start.UseShellExecute = false;
    using var process = Process.Start(start)!;
    process.WaitForExit();
    return process.ExitCode;
}

// lirenet.dll: the one LIRENET names, the one beside frainet, or the one built in Lirenet's folder beside Frainet's
// (the same configuration first).
static ProcessStartInfo? Lirenet()
{
    var here = AppContext.BaseDirectory;
    var named = Environment.GetEnvironmentVariable("LIRENET");
    var candidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(named))
    {
        candidates.Add(named);
    }

    candidates.Add(Path.Combine(here, "lirenet.dll"));
    for (var dir = new DirectoryInfo(here); dir is not null; dir = dir.Parent)
    {
        var bin = Path.Combine(dir.FullName, "Lirenet", "src", "Lirenet.Cli", "bin");
        if (!Directory.Exists(bin))
        {
            continue;
        }

        var configuration = here.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ? "Release" : "Debug";
        candidates.AddRange(Directory.EnumerateFiles(bin, "lirenet.dll", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ThenByDescending(File.GetLastWriteTimeUtc));
        break;
    }

    var found = candidates.FirstOrDefault(File.Exists);
    if (found is null)
    {
        return null;
    }

    var start = new ProcessStartInfo("dotnet");
    start.ArgumentList.Add(found);
    return start;
}
