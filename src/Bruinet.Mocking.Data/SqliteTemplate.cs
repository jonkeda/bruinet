namespace Bruinet.Mocking.Data;

/// <summary>
/// A SQLite dependency's <c>copy</c> mode (AI11): the real engine in the test process, each test on its own copy of a
/// seeded template. Nothing is replayed: an embedded database answers in microseconds already, and the copy keeps what
/// it proves (the real queries, constraints and migrations). Tests stay parallel and share nothing.
/// </summary>
public static class SqliteTemplate
{
    /// <summary>How often a process waiting for another's template looks again.</summary>
    public static readonly TimeSpan LockPollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>How long it waits for another process making the template.</summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The template at <paramref name="templatePath"/>, made by <paramref name="create"/> when it is missing or was made
    /// for another <paramref name="stamp"/> (a build's migrations and seed, hashed). Processes share it: one makes it while
    /// the others wait.
    /// </summary>
    /// <param name="templatePath">The template file.</param>
    /// <param name="stamp">What the template was made from; a different one makes it again.</param>
    /// <param name="create">Makes the database at the path it is given: the app's own migrations and seed. It closes its
    /// connections (and clears a provider's pool) before it returns, so the file is complete and free to move.</param>
    /// <param name="cancellationToken">Stops the wait for another process's template.</param>
    /// <returns><paramref name="templatePath"/>, full.</returns>
    public static async Task<string> EnsureAsync(string templatePath, string stamp, Func<string, Task> create, CancellationToken cancellationToken = default)
    {
        if (templatePath is null)
        {
            throw new ArgumentNullException(nameof(templatePath));
        }

        if (create is null)
        {
            throw new ArgumentNullException(nameof(create));
        }

        var full = Path.GetFullPath(templatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var stampPath = full + ".stamp";
        using (await LockAsync(full + ".lock", cancellationToken).ConfigureAwait(false))
        {
            if (File.Exists(full) && File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
            {
                return full;
            }

            var made = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await create(made).ConfigureAwait(false);
            TryDelete(stampPath);
            // Replaced in one step, so a copy made meanwhile reads the old template or the new, never none.
            if (File.Exists(full))
            {
                File.Replace(made, full, destinationBackupFileName: null);
            }
            else
            {
                File.Move(made, full);
            }
            File.WriteAllText(stampPath, stamp);
            return full;
        }
    }

    /// <summary>A fresh copy of the template for one test, deleted when disposed.</summary>
    /// <param name="templatePath">The template (<see cref="EnsureAsync"/>).</param>
    /// <param name="test">The test's name, for the copy's file name.</param>
    /// <param name="folder">Where copies go; a folder under the temp folder when null.</param>
    public static SqliteCopy Copy(string templatePath, string test, string? folder = null)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Make the template first (SqliteTemplate.EnsureAsync).", templatePath);
        }

        var invalid = Path.GetInvalidFileNameChars();
        var name = new string((test ?? "test").Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        var directory = folder ?? Path.Combine(Path.GetTempPath(), "Bruinet", "sqlite");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N").Substring(0, 8) + "-" + name + ".db");
        File.Copy(templatePath, path);
        return new SqliteCopy(path);
    }

    /// <summary>Deletes <paramref name="path"/>; false when another handle still holds it (a pooled connection).</summary>
    internal static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    // A lock file opened for this process alone; another process making the template waits.
    private static async Task<FileStream> LockAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            // A lock file another process is closing (delete on close) refuses access until it is gone.
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && DateTime.UtcNow < deadline)
            {
                await Task.Delay(LockPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>One test's copy of a SQLite template; disposing deletes it with its journal files.</summary>
public sealed class SqliteCopy : IDisposable
{
    internal SqliteCopy(string path)
    {
        Path = path;
    }

    /// <summary>The copy's file.</summary>
    public string Path { get; }

    /// <summary><c>Data Source=&lt;file&gt;</c>, for <c>SqliteConnection</c> or <c>UseSqlite</c>.</summary>
    public string ConnectionString => "Data Source=" + Path;

    /// <summary>
    /// Deletes the copy, its <c>-wal</c>, <c>-shm</c> and <c>-journal</c>. A pooled connection still holding it keeps it
    /// (clear the pools first, <c>SqliteConnection.ClearAllPools()</c>); it is in the temp folder either way.
    /// </summary>
    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            SqliteTemplate.TryDelete(Path + suffix);
        }
    }
}
