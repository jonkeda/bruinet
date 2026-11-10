namespace Bruinet.Contracts;

/// <summary>A test account read from a local, git-ignored settings file.</summary>
public sealed record LocalAccount(string Username, string Password, string Organization);

/// <summary>
/// Reads named test accounts from a git-ignored <c>.env.local</c>, so tracked configuration can
/// name an account without holding its credentials.
/// </summary>
/// <remarks>
/// With key prefix <c>APP_AUTH</c>, account <c>reader</c> reads <c>APP_AUTH_READER_USERNAME</c>,
/// <c>_PASSWORD</c> and <c>_ORGANIZATION</c>; the account <c>default</c> reads
/// <c>APP_AUTH_USERNAME</c> and so on. Blank lines and <c>#</c> comments are skipped, and a value
/// may be wrapped in single or double quotes. The file is searched for upwards from the given
/// folder, the current directory and the test assembly's folder, in that order.
/// </remarks>
public sealed class LocalAccountStore
{
    /// <summary>The file name searched for.</summary>
    public const string DefaultFileName = ".env.local";

    /// <summary>The account name that reads the keys without an account segment.</summary>
    public const string DefaultAccountName = "default";

    /// <param name="keyPrefix">The key prefix, e.g. <c>APP_AUTH</c>, without a trailing underscore.</param>
    /// <param name="fileName">The file name searched for.</param>
    public LocalAccountStore(string keyPrefix, string fileName = DefaultFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        KeyPrefix = keyPrefix.TrimEnd('_');
        FileName = fileName;
    }

    /// <summary>The key prefix, without a trailing underscore.</summary>
    public string KeyPrefix { get; }

    /// <summary>The file name searched for.</summary>
    public string FileName { get; }

    /// <summary>
    /// The account called <paramref name="name"/>, or <c>null</c> when there is no file or the
    /// file has no username and password for it.
    /// </summary>
    public LocalAccount? Find(string name, string? searchFrom = null)
    {
        var path = FindFile(searchFrom);
        return path is null ? null : Parse(File.ReadAllLines(path), name);
    }

    /// <summary>The account called <paramref name="name"/> in <paramref name="lines"/>.</summary>
    public LocalAccount? Parse(IEnumerable<string> lines, string name)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Select(line => line.Trim()))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length == 0 || line.StartsWith('#') || separator < 1)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            values[line[..separator].Trim()] = value;
        }

        var prefix = string.Equals(name, DefaultAccountName, StringComparison.OrdinalIgnoreCase)
            ? $"{KeyPrefix}_"
            : $"{KeyPrefix}_{name.ToUpperInvariant()}_";
        var username = values.GetValueOrDefault(prefix + "USERNAME");
        var password = values.GetValueOrDefault(prefix + "PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return new LocalAccount(username, password, values.GetValueOrDefault(prefix + "ORGANIZATION") ?? string.Empty);
    }

    /// <summary>The nearest file, or <c>null</c> when there is none.</summary>
    public string? FindFile(string? searchFrom = null)
    {
        foreach (var root in new[] { searchFrom, Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = root is null ? null : new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
