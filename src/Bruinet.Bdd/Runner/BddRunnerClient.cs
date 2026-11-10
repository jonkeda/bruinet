using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace Bruinet.Bdd.Runner;

/// <summary>What a runner process is started with.</summary>
/// <param name="PagesAssembly">The suite's Pages assembly. Its folder is the suite's output: the runner starts there, with the suite's own dependencies.</param>
/// <param name="WorkingDirectory">The runner's working directory; null for the suite's output folder.</param>
/// <param name="Environment">Variables set for the runner only: the app's path, how its window is shown.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddRunnerStart(string PagesAssembly, string? WorkingDirectory = null, IReadOnlyDictionary<string, string>? Environment = null)
{
    /// <summary>The assembly with the runner's entry point, which every suite has in its output.</summary>
    public const string RunnerAssembly = "Bruinet.Uat.dll";

    /// <summary>
    /// The process to start: <c>dotnet exec</c> on the suite's output with the suite's own
    /// <c>runtimeconfig.json</c> and <c>deps.json</c>, as a test host is started.
    /// </summary>
    /// <param name="pipe">The pipe the runner connects to.</param>
    /// <exception cref="FileNotFoundException">The suite's output lacks a file the runner needs.</exception>
    public ProcessStartInfo ToStartInfo(string pipe)
    {
        var pages = Path.GetFullPath(PagesAssembly);
        var output = Path.GetDirectoryName(pages)!;
        var runtimeConfig = Path.ChangeExtension(pages, ".runtimeconfig.json");
        var deps = Path.ChangeExtension(pages, ".deps.json");
        var runner = Path.Combine(output, RunnerAssembly);
        foreach (var (file, what) in new[] { (pages, "Build the suite."), (runtimeConfig, "The Pages assembly has to be the suite's test project, which has one."), (runner, "The suite has to reference Bruinet.Uat; rebuild it.") })
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"A run needs {file}. {what}", file);
            }
        }

        var start = new ProcessStartInfo(DotnetHost())
        {
            WorkingDirectory = WorkingDirectory ?? output,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(runtimeConfig);
        if (File.Exists(deps))
        {
            start.ArgumentList.Add("--depsfile");
            start.ArgumentList.Add(deps);
        }

        start.ArgumentList.Add(runner);
        start.ArgumentList.Add(BddRunnerProtocol.PipeArgument);
        start.ArgumentList.Add(pipe);
        foreach (var (name, value) in Environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        return start;
    }

    // The dotnet this process runs on (three folders above its runtime), or the one on the PATH.
    private static string DotnetHost()
    {
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        var host = Path.Combine(root, name);
        return File.Exists(host) ? host : name;
    }
}

/// <summary>The runner process ended, or did not answer as the protocol says.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddRunnerException(string message) : Exception(message);

/// <summary>A host's end of a runner process: starts it, sends requests, reads the answers.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddRunnerClient : IAsyncDisposable
{
    /// <summary>How long a canceled step gets to end by itself before the runner is killed.</summary>
    public static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(5);

    /// <summary>How long a runner gets to start and connect.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private const int LogLines = 40;
    private readonly Process _process;
    private readonly BddRunnerChannel _channel;
    private readonly Queue<string> _log = new();
    private readonly SemaphoreSlim _requests = new(1, 1);
    private bool _disposed;

    private BddRunnerClient(Process process, BddRunnerChannel channel)
    {
        _process = process;
        _channel = channel;
    }

    /// <summary>The runner process.</summary>
    public Process Process => _process;

    /// <summary>Called when the runner announces a spoken line, for the host to play.</summary>
    public Action<BddRunnerSay>? Said { get; set; }

    /// <summary>The last lines the runner wrote to its console.</summary>
    public string Log
    {
        get
        {
            lock (_log)
            {
                return string.Join(Environment.NewLine, _log);
            }
        }
    }

    /// <summary>Starts a runner and waits for its hello.</summary>
    /// <param name="start">What to start.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="BddRunnerException">The runner did not start, or speaks another protocol version.</exception>
    public static async Task<BddRunnerClient> StartAsync(BddRunnerStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        var pipeName = "bruinet-runner-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Process? process = null;
        BddRunnerClient? client = null;
        try
        {
            process = Process.Start(start.ToStartInfo(pipeName)) ?? throw new BddRunnerException("The runner process did not start.");
            client = new BddRunnerClient(process, new BddRunnerChannel(pipe));
            client.CaptureLog();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);
            var connected = pipe.WaitForConnectionAsync(timeout.Token);
            var exited = process.WaitForExitAsync(timeout.Token);
            if (await Task.WhenAny(connected, exited).ConfigureAwait(false) == exited && !connected.IsCompletedSuccessfully)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new BddRunnerException(client.Ended("The runner did not start."));
            }

            await connected.ConfigureAwait(false);
            var hello = await client._channel.ReceiveAsync(timeout.Token).ConfigureAwait(false) as BddRunnerHello
                        ?? throw new BddRunnerException(client.Ended("The runner did not say hello."));
            if (BddRunnerProtocol.Mismatch(hello.Version) is { } mismatch)
            {
                throw new BddRunnerException(mismatch);
            }

            return client;
        }
        catch
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                process?.Dispose();
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// Sends a request and returns its answer. When canceled, the running step is asked to stop;
    /// a step that does not within <see cref="CancelGrace"/> has its runner killed.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <exception cref="BddRunnerException">The runner ended before it answered.</exception>
    public async Task<BddRunnerMessage> RequestAsync(BddRunnerMessage request, CancellationToken cancellationToken = default)
    {
        await _requests.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _channel.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            using var grace = new CancellationTokenSource();
            await using var cancel = cancellationToken.Register(() =>
            {
                grace.CancelAfter(CancelGrace);
                _ = _channel.SendAsync(new BddRunnerCancel(), CancellationToken.None);
            }).ConfigureAwait(false);

            while (true)
            {
                BddRunnerMessage? answer;
                try
                {
                    answer = await _channel.ReceiveAsync(grace.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    Kill();
                    throw new OperationCanceledException("The step did not stop when asked; its runner was ended.", exception, cancellationToken);
                }

                switch (answer)
                {
                    case null:
                        throw new BddRunnerException(Ended("The runner ended in the middle of a run."));
                    case BddRunnerSay say:
                        Said?.Invoke(say);
                        break;
                    default:
                        return answer;
                }
            }
        }
        finally
        {
            _requests.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_process.HasExited)
            {
                await _channel.SendAsync(new BddRunnerExit(), CancellationToken.None).ConfigureAwait(false);
                using var grace = new CancellationTokenSource(CancelGrace);
                await _process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // It is ended below: asking nicely is only so the fixture can close its app itself.
        }

        Kill();
        _channel.Dispose();
        _process.Dispose();
        _requests.Dispose();
    }

    // The runner and what it started: the app under test, a mock server, a browser.
    private void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Gone already.
        }
    }

    private string Ended(string what)
    {
        _process.WaitForExit(1000);
        var code = _process.HasExited ? $" Exit code {_process.ExitCode}." : string.Empty;
        var log = Log;
        return log.Length == 0 ? what + code : what + code + Environment.NewLine + log;
    }

    private void CaptureLog()
    {
        void Keep(object sender, DataReceivedEventArgs line)
        {
            if (line.Data is null)
            {
                return;
            }

            lock (_log)
            {
                _log.Enqueue(line.Data);
                while (_log.Count > LogLines)
                {
                    _log.Dequeue();
                }
            }
        }

        _process.OutputDataReceived += Keep;
        _process.ErrorDataReceived += Keep;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }
}
