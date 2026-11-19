using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

namespace Bruinet.Bdd.VisualStudio;

/// <summary>
/// The language server, started as <c>dotnet Server\bruinet.dll lsp</c> from the
/// extension's folder, and the messages the extension sends it. The extension holds no engine
/// types: everything here is JSON (P2). A server that exits is started again on the next call.
/// </summary>
public sealed class BddServerClient : IDisposable
{
    private readonly string _serverPath;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _starting = new(1, 1);
    private Process? _process;
    private JsonRpc? _rpc;
    private string? _solutionDir;

    /// <summary>Creates the client; the server starts on the first call.</summary>
    /// <param name="serverPath">The server's dll.</param>
    /// <param name="log">Where the server's stderr and the client's notes go (the Output pane).</param>
    public BddServerClient(string serverPath, Action<string> log)
    {
        _serverPath = serverPath;
        _log = log;
    }

    /// <summary>The server says a run wrote new results for an open file (its path).</summary>
    public event EventHandler<string>? ResultsChanged;

    /// <summary>A Step-through run paused (bruinet/step/paused): where, as sent.</summary>
    public event EventHandler<JObject>? StepPaused;

    /// <summary>A Step-through run ended (bruinet/step/ended): its status, as sent.</summary>
    public event EventHandler<JObject>? StepEnded;

    /// <summary>The warm runner's settings (Tools, Options), read when the server starts.</summary>
    public Func<(bool Warm, double IdleMinutes)>? RunnerSettings { get; set; }

    /// <summary>A run on the suite's runner ended (bruinet/run/ended): its status and each scenario's, as sent.</summary>
    public event EventHandler<JObject>? RunEnded;

    /// <summary>The server's path beside the extension's assembly.</summary>
    public static string DefaultServerPath =>
        Path.Combine(Path.GetDirectoryName(typeof(BddServerClient).Assembly.Location)!, "Server", "bruinet.dll");

    /// <summary>Starts the server for a solution folder, or restarts it for another one.</summary>
    /// <param name="solutionDir">The folder whose suites the server serves.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Whether a new server started: the files it had open are gone.</returns>
    public async Task<bool> InitializeAsync(string solutionDir, CancellationToken cancellationToken)
    {
        if (_rpc is not null && string.Equals(_solutionDir, solutionDir, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _solutionDir = solutionDir;
        Stop();
        await RpcAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>LSP <c>textDocument/didOpen</c>.</summary>
    public async Task DidOpenAsync(string path, int version, string text, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false)).NotifyWithParameterObjectAsync(
            "textDocument/didOpen",
            new { textDocument = new { uri = Uri(path), languageId = "markdown", version, text } }).ConfigureAwait(false);

    /// <summary>LSP <c>textDocument/didChange</c>, the whole text.</summary>
    public async Task DidChangeAsync(string path, int version, string text, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false)).NotifyWithParameterObjectAsync(
            "textDocument/didChange",
            new { textDocument = new { uri = Uri(path), version }, contentChanges = new[] { new { text } } }).ConfigureAwait(false);

    /// <summary>LSP <c>textDocument/didClose</c>.</summary>
    public async Task DidCloseAsync(string path, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false)).NotifyWithParameterObjectAsync(
            "textDocument/didClose",
            new { textDocument = new { uri = Uri(path) } }).ConfigureAwait(false);

    /// <summary><c>bruinet/buildDone</c>: the suites load their new build output on next use.</summary>
    public async Task BuildDoneAsync(CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false)).NotifyWithParameterObjectAsync("bruinet/buildDone", new { }).ConfigureAwait(false);

    /// <summary><c>bruinet/previewHost</c>: the page the webview loads once.</summary>
    public async Task<string> PreviewHostAsync(CancellationToken cancellationToken)
    {
        var result = await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/previewHost", new { }, cancellationToken).ConfigureAwait(false);
        return (string?)result["html"] ?? string.Empty;
    }

    /// <summary><c>bruinet/preview</c>: the file rendered, with its suites and bind state, as the page's <c>update</c> message takes it.</summary>
    public async Task<JObject> PreviewAsync(string path, string? suite, CancellationToken cancellationToken, string? recordingsFolder = null)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/preview", new { uri = Uri(path), suite, recordingsFolder }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/recordTarget</c>: the project, filter and variables of the run that records a video script, or the scenario at a heading's line.</summary>
    public async Task<JObject> RecordTargetAsync(string path, int? line, string? suite, BddOptionsPage options, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/recordTarget", new
            {
                uri = Uri(path),
                line,
                suite,
                recordingsFolder = Setting(options.RecordingsFolder),
                voice = Setting(options.Voice),
                beat = options.Beat > 0 ? options.Beat : (int?)null,
                touchColor = Setting(options.TouchColor),
            }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/recordings</c>: the videos of a video script, or of a feature file's scenarios, newest first.</summary>
    public async Task<JArray> RecordingsAsync(string path, string? suite, string? recordingsFolder, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JArray>("bruinet/recordings", new { uri = Uri(path), suite, recordingsFolder = Setting(recordingsFolder) }, cancellationToken).ConfigureAwait(false);

    private static string? Setting(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary><c>bruinet/runTarget</c>: the project and filter for the scenario at a heading's line, or the whole file.</summary>
    public async Task<JObject> RunTargetAsync(string path, int? line, string? suite, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/runTarget", new { uri = Uri(path), line, suite }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/step/start</c>: Step through the scenario at a heading's line, in the suite's runner.</summary>
    public async Task<JObject> StepStartAsync(string path, int line, string? suite, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/step/start", new { uri = Uri(path), line, suite }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/step/move</c>: <c>over</c>, <c>into</c>, <c>out</c>, <c>continue</c> or <c>stop</c>.</summary>
    public async Task<JObject> StepMoveAsync(string move, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/step/move", new { move }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/step/debug</c>: Debug from here; the runner's <c>processId</c> to attach to, or the <c>reason</c> there is none.</summary>
    public async Task<JObject> StepDebugAsync(CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/step/debug", new { }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/run/start</c>: runs the scenario at a heading's line, or the file, on the suite's runner (AD-022).</summary>
    public async Task<JObject> RunStartAsync(string path, int? line, string? suite, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/run/start", new { uri = Uri(path), line, suite }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/run/cancel</c>: cancels the run on the runner.</summary>
    public async Task<JObject> RunCancelAsync(CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/run/cancel", new { }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/step/screenshot</c>: the paused Step-through run's app, now; its <c>path</c> or the <c>reason</c> there is none.</summary>
    public async Task<JObject> StepScreenshotAsync(CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/step/screenshot", new { }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/deleteResults</c>: deletes, or counts in a dry run, the old results of a scenario, a file or a suite.</summary>
    public async Task<JObject> DeleteResultsAsync(string scope, string? uri, int? line, string? suite, bool dryRun, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/deleteResults", new { scope, uri, line, suite, dryRun }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/explorerHost</c>: the page the BDD Explorer's webview loads once.</summary>
    public async Task<string> ExplorerHostAsync(CancellationToken cancellationToken)
        => (string)(await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/explorerHost", new { }, cancellationToken).ConfigureAwait(false))["html"]!;

    /// <summary><c>bruinet/explorer</c>: a view (<c>steps</c>, <c>pages</c>, <c>results</c>) of a suite.</summary>
    public async Task<JObject> ExplorerAsync(string view, string? suite, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/explorer", new { view, suite }, cancellationToken).ConfigureAwait(false);

    /// <summary><c>bruinet/usages</c>: the steps across every suite that use a C# member, for CodeLens.</summary>
    public async Task<JObject> UsagesAsync(string symbol, CancellationToken cancellationToken)
        => await (await RpcAsync(cancellationToken).ConfigureAwait(false))
            .InvokeWithParameterObjectAsync<JObject>("bruinet/usages", new { symbol }, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _starting.Dispose();
    }

    private async Task<JsonRpc> RpcAsync(CancellationToken cancellationToken)
    {
        if (_rpc is { IsDisposed: false } running)
        {
            return running;
        }

        await _starting.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rpc is { IsDisposed: false } started)
            {
                return started;
            }

            if (!File.Exists(_serverPath))
            {
                throw new FileNotFoundException($"The Bruinet language server is not at {_serverPath}. Reinstall the extension.", _serverPath);
            }

            var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{_serverPath}\" lsp")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _solutionDir ?? Path.GetDirectoryName(_serverPath)!,
            }) ?? throw new InvalidOperationException("dotnet did not start. The Bruinet language server needs the .NET 10 runtime.");
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is { } line)
                {
                    _log(line);
                }
            };
            process.BeginErrorReadLine();

            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(process.StandardInput.BaseStream, process.StandardOutput.BaseStream, formatter));
            rpc.Disconnected += (_, e) => _log($"The language server stopped: {e.Description}");
            rpc.AddLocalRpcMethod("bruinet/resultsChanged", new Action<string>(uri => ResultsChanged?.Invoke(this, new Uri(uri).LocalPath)));
            AddWhole(rpc, "bruinet/step/paused", paused => StepPaused?.Invoke(this, paused));
            AddWhole(rpc, "bruinet/step/ended", ended => StepEnded?.Invoke(this, ended));
            AddWhole(rpc, "bruinet/run/ended", ended => RunEnded?.Invoke(this, ended));
            rpc.StartListening();
            await rpc.InvokeWithParameterObjectAsync<JObject>(
                "initialize",
                new { processId = Process.GetCurrentProcess().Id, initializationOptions = new { solutionDir = _solutionDir, warmRunner = RunnerSettings?.Invoke().Warm ?? true, warmRunnerIdleMinutes = RunnerSettings?.Invoke().IdleMinutes ?? 10 } },
                cancellationToken).ConfigureAwait(false);
            await rpc.NotifyWithParameterObjectAsync("initialized", new { }).ConfigureAwait(false);
            _log($"Started the language server for {_solutionDir}.");

            _process = process;
            _rpc = rpc;
            return rpc;
        }
        finally
        {
            _starting.Release();
        }
    }

    // A notification whose params object comes whole, not by parameter name.
    private static void AddWhole(JsonRpc rpc, string name, Action<JObject> handler)
        => rpc.AddLocalRpcMethod(handler.Method, handler.Target, new JsonRpcMethodAttribute(name) { UseSingleObjectParameterDeserialization = true });

    private void Stop()
    {
        var rpc = _rpc;
        var process = _process;
        _rpc = null;
        _process = null;
        rpc?.Dispose();
        if (process is { HasExited: false })
        {
            process.Kill();
        }

        process?.Dispose();
    }

    private static string Uri(string path) => new Uri(path).AbsoluteUri;
}
