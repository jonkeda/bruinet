using System.Diagnostics;
using System.Text.RegularExpressions;
using Bruinet.Mocking.Data.Replay;

namespace Bruinet.Mocking.Data.Hook.Tests;

/// <summary>
/// The hook sample (<c>samples/Bruinet.Samples.Sql.Hook</c>) started with <c>dotnet</c> as a process of its own, as an app
/// on IIS would be: the tests reach it only through HTTP. Its replay mode and recordings come from its environment.
/// </summary>
internal sealed class HookApp : IAsyncDisposable
{
    private static readonly Regex Listening = new(@"Now listening on: (?<url>http://\S+)", RegexOptions.Compiled);
    private readonly Process _process;

    private HookApp(Process process, Uri address)
    {
        _process = process;
        Address = address;
    }

    public Uri Address { get; }

    /// <param name="mode">Record or Replay; null starts it with replay off.</param>
    /// <param name="key">The hook's key in the app's environment; none when left out.</param>
    public static async Task<HookApp> StartAsync(ReplayMode? mode, string recordings, string database, string? key = null)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { Dll(), "--urls", "http://127.0.0.1:0", "--Hook:Database", database },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment.Remove(ReplayRegistry.ModeVariable);
        start.Environment.Remove(ReplayRegistry.SchemaVariable);
        start.Environment.Remove(ReplayHook.KeyVariable);
        if (key is not null)
        {
            start.Environment[ReplayHook.KeyVariable] = key;
        }
        if (mode is { } replay)
        {
            start.Environment[ReplayRegistry.ModeVariable] = replay.ToString();
            start.Environment[ReplayRegistry.FolderVariable] = recordings;
        }

        var process = Process.Start(start)!;
        var address = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, line) =>
        {
            lock (output)
            {
                output.AppendLine(line.Data);
            }

            if (line.Data is { } text && Listening.Match(text) is { Success: true } match)
            {
                address.TrySetResult(new Uri(match.Groups["url"].Value));
            }
        };
        process.ErrorDataReceived += (_, line) =>
        {
            lock (output)
            {
                output.AppendLine(line.Data);
            }
        };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => address.TrySetException(new InvalidOperationException($"The hook sample stopped: {output}"));
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var started = await Task.WhenAny(address.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (started != address.Task)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"The hook sample didn't start in 30 s: {output}");
        }

        return new HookApp(process, await address.Task);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    // Its own output folder, beside this one: tests/<this>/bin/<config>/<tfm> to samples/<sample>/bin/<config>/<tfm>.
    private static string Dll()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var framework = output.Name;
        var configuration = output.Parent!.Name;
        var root = output;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Bruinet.sln")))
        {
            root = root.Parent;
        }

        var dll = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException("The repository's root"), "samples", "Bruinet.Samples.Sql.Hook", "bin", configuration, framework, "Bruinet.Samples.Sql.Hook.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("Build the hook sample first.", dll);
    }
}
