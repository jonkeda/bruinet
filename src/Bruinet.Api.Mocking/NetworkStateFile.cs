namespace Bruinet.Api.Mocking;

/// <summary>
/// The test side of a network-state control file: a file holding <c>online</c> or
/// <c>offline</c> that the app under test reads instead of the real connectivity.
/// </summary>
/// <remarks>
/// <para>
/// Lets a UI test take the app offline and back in the middle of a test, on a desktop head,
/// without the gesture bridge and without touching the machine's network. The pattern is
/// <c>Exact.Construction.UITests</c>' <c>NetworkStateOverride</c>.
/// </para>
/// <para>
/// <b>The app has to cooperate.</b> It reads the file's path from a setting (an environment
/// variable it inherits, usually) and consults it wherever it asks whether it is online. That
/// half lives in the app, in a debug or test build only; this class does not know the variable's
/// name, because that is the app's to choose.
/// </para>
/// </remarks>
public sealed class NetworkStateFile : IDisposable
{
    /// <summary>The file content meaning "online".</summary>
    public const string Online = "online";

    /// <summary>The file content meaning "offline".</summary>
    public const string Offline = "offline";

    private readonly Lock _gate = new();
    private bool _disposed;

    private NetworkStateFile(string path)
    {
        Path = path;
    }

    /// <summary>The file's full path, to hand to the app.</summary>
    public string Path { get; }

    /// <summary>Whether the file currently says <c>online</c>.</summary>
    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return string.Equals(File.ReadAllText(Path).Trim(), Online, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Creates the file, saying <c>online</c>.
    /// </summary>
    /// <param name="path">Where; a unique file in the temp folder when omitted.</param>
    public static NetworkStateFile Create(string? path = null)
    {
        path ??= System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "Bruinet",
            "network-state",
            $"network-state-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new NetworkStateFile(path);
        file.SetOnline();
        return file;
    }

    /// <summary>Tells the app it is online.</summary>
    public void SetOnline() => Write(Online);

    /// <summary>Tells the app it is offline.</summary>
    public void SetOffline() => Write(Offline);

    /// <summary>Deletes the file.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            File.Delete(Path);
        }
    }

    private void Write(string state)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Whole-file replace, so the app never reads a half-written word.
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, state);
            Replace(temporary);
        }
    }

    // Windows refuses to replace a file another handle has open, and the app polls this one: a read
    // lasts microseconds, so the move is tried again a few times before the refusal stands. An app in
    // the test's own process (the MauiHeadless head) meets the read often enough to fail a run.
    private void Replace(string temporary)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, Path, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException && attempt < ReplaceAttempts)
            {
                Thread.Sleep(ReplacePause);
            }
        }
    }

    private const int ReplaceAttempts = 20;
    private static readonly TimeSpan ReplacePause = TimeSpan.FromMilliseconds(10);
}
