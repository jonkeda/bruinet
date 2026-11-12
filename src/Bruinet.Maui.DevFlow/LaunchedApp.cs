using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Bruinet.Core.Exceptions;

namespace Bruinet.Maui.DevFlow;

/// <summary>An app the runner launched: its process, and closing it.</summary>
internal interface ILaunchedApp : IDisposable
{
    int ProcessId { get; }

    bool HasExited { get; }

    /// <summary>What to say when the app went away before its agent answered.</summary>
    string ExitDescription { get; }
}

internal static class LaunchedApp
{
    /// <summary>The launch variable the sample apps read their agent's port from (<c>AddMauiDevFlowAgent(o =&gt; o.Port = ...)</c>).</summary>
    public const string PortVariable = "BRUINET_DEVFLOW_PORT";

    /// <summary>
    /// Starts the app. On Windows through Bruinet.FlaUI's <c>DesktopApp</c>, as the FlaUI runner starts
    /// it: behind the person's window (it hands the foreground back and keeps it), placed as
    /// <c>BRUINET_AUT_PLACE</c> asks, and only once its main window shows.
    /// </summary>
    public static ILaunchedApp Start(string appPath, IReadOnlyDictionary<string, string> environment, TimeSpan timeout)
    {
#if WINDOWS
        return new DesktopLaunch(Bruinet.FlaUI.DesktopApp.Launch(appPath, arguments: null, environment, timeout));
#else
        _ = timeout;
        return new ProcessLaunch(appPath, environment);
#endif
    }

    /// <summary>A loopback port nothing listens on now, for one launch's agent.</summary>
    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

#if WINDOWS
    private sealed class DesktopLaunch(Bruinet.FlaUI.DesktopApp app) : ILaunchedApp
    {
        public int ProcessId => app.ProcessId ?? throw new BruinetException("The launched app has no known process.");

        public bool HasExited => app.HasExited;

        public string ExitDescription => "The app exited";

        public void Dispose() => app.Dispose();
    }
#endif

    private sealed class ProcessLaunch : ILaunchedApp
    {
        private readonly Process _process;

        public ProcessLaunch(string appPath, IReadOnlyDictionary<string, string> environment)
        {
            var start = new ProcessStartInfo(appPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(appPath)!,
            };
            foreach (var (name, value) in environment)
                start.Environment[name] = value;
            _process = Process.Start(start) ?? throw new BruinetException($"The app did not start: {appPath}");
        }

        public int ProcessId => _process.Id;

        public bool HasExited => _process.HasExited;

        public string ExitDescription => $"The app exited with code {_process.ExitCode}";

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            _process.Dispose();
        }
    }
}
