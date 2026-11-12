using Bruinet.Samples.Todo.Core.Services;

namespace Bruinet.Samples.Todo.Infrastructure.Connectivity;

/// <summary>Always online. The default when nothing says otherwise.</summary>
public sealed class AlwaysOnline : IConnectivityState
{
    /// <inheritdoc />
    public bool IsOnline => true;

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Online according to a network-state file holding <c>online</c> or <c>offline</c>.
/// </summary>
/// <remarks>
/// <para>
/// The app side of the test network switch (Bruinet.Mocking's <c>NetworkStateFile</c>; the
/// pattern is Construction's <c>NetworkStateOverride</c>). Only used when the launch settings name
/// a file, which only a test run does.
/// </para>
/// <para>
/// A missing or unreadable file counts as online: a test that has not created one yet should see
/// the app behave normally. <see cref="Changed"/> is raised on a thread-pool thread.
/// </para>
/// </remarks>
public sealed class NetworkStateFileConnectivity : IConnectivityState, IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;

    /// <summary>Watches <paramref name="path"/>.</summary>
    public NetworkStateFileConnectivity(string path)
    {
        _path = System.IO.Path.GetFullPath(path);

        var directory = System.IO.Path.GetDirectoryName(_path);
        if (directory is not null && Directory.Exists(directory))
        {
            _watcher = new FileSystemWatcher(directory, System.IO.Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;

            // A full buffer drops events, the offline write among them (R2-T109): read the file again.
            _watcher.Error += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            _watcher.EnableRaisingEvents = true;
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsOnline
    {
        get
        {
            try
            {
                return !string.Equals(File.ReadAllText(_path).Trim(), "offline", StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _watcher?.Dispose();

    private void OnFileChanged(object sender, FileSystemEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Online only when every source says so - the device's connectivity and, under test, the
/// network-state file - and raises <see cref="Changed"/> through a dispatcher, so the app can
/// put it on the UI thread.
/// </summary>
public sealed class CompositeConnectivity : IConnectivityState
{
    private readonly IReadOnlyList<IConnectivityState> _sources;
    private readonly Action<Action> _dispatch;

    /// <summary>Combines <paramref name="sources"/>; <paramref name="dispatch"/> runs each notification.</summary>
    public CompositeConnectivity(IReadOnlyList<IConnectivityState> sources, Action<Action>? dispatch = null)
    {
        _sources = sources;
        _dispatch = dispatch ?? (action => action());

        foreach (var source in _sources)
        {
            source.Changed += (_, _) => _dispatch(() => Changed?.Invoke(this, EventArgs.Empty));
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsOnline => _sources.All(source => source.IsOnline);
}
