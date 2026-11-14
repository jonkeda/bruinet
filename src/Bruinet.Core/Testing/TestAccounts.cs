using Bruinet.Core.Utilities;

namespace Bruinet.Core.Testing;

/// <summary>
/// An account a test or a scenario signs in with: a row of the <c>Accounts</c> table of
/// <c>TestAccounts.md</c>, with its username and password from <c>TestAccounts.local.md</c>.
/// </summary>
/// <param name="Name">What tests and steps call the account, such as <c>NormalUser</c>.</param>
/// <param name="Username">The username to sign in with.</param>
/// <param name="Password">The password. Never logged: <see cref="ToString"/> is the name.</param>
/// <param name="Organization">The organization the sign-in picks, or null.</param>
/// <param name="Writes">False when the account must never create, change or delete data.</param>
/// <param name="Values">Every column of the account's row in <c>TestAccounts.md</c>, by column name.</param>
public sealed record TestAccount(
    string Name,
    string Username,
    string Password,
    string? Organization,
    bool Writes,
    IReadOnlyDictionary<string, string> Values)
{
    /// <summary>The value of <paramref name="column"/> in <c>TestAccounts.md</c>, or null when it is missing or empty.</summary>
    public string? this[string column]
        => Values.TryGetValue(column, out var value) && value.Length > 0 ? value : null;

    /// <summary>The account's name, never its secrets.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// An account a test names is not in <c>TestAccounts.md</c>, or has no username or password in
/// <c>TestAccounts.local.md</c>. The test fails with it: a missing account is a broken set-up.
/// </summary>
public sealed class TestAccountException(string message) : Exception(message);

/// <summary>
/// The accounts of a suite, by name, from two markdown files side by side: <c>TestAccounts.md</c>
/// (tracked, no secrets) and <c>TestAccounts.local.md</c> (git-ignored, usernames and passwords).
/// Both have an <c>Accounts</c> table with a <c>Name</c> column. Nothing else is read.
/// </summary>
public sealed class TestAccounts
{
    /// <summary>The tracked file.</summary>
    public const string FileName = "TestAccounts.md";

    /// <summary>The git-ignored file beside it.</summary>
    public const string SecretsFileName = "TestAccounts.local.md";

    private const string Section = "Accounts";

    private static readonly Lazy<TestAccounts> Found = new(() => FindFrom(AppContext.BaseDirectory));

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _accounts;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>>? _secrets;

    private TestAccounts(
        Dictionary<string, IReadOnlyDictionary<string, string>> accounts,
        Dictionary<string, IReadOnlyDictionary<string, string>>? secrets)
    {
        _accounts = accounts;
        _secrets = secrets;
    }

    /// <summary>The accounts found upwards from the test's folder, read once per process.</summary>
    public static TestAccounts Default => Found.Value;

    /// <summary>The names in <c>TestAccounts.md</c>, in its order.</summary>
    public IReadOnlyList<string> Names => [.. _accounts.Keys];

    /// <summary>The accounts of two markdown documents.</summary>
    /// <param name="accountsMarkdown">The text of <c>TestAccounts.md</c>.</param>
    /// <param name="secretsMarkdown">The text of <c>TestAccounts.local.md</c>, or null when there is none.</param>
    public static TestAccounts Read(string accountsMarkdown, string? secretsMarkdown)
    {
        ArgumentNullException.ThrowIfNull(accountsMarkdown);
        var accounts = Rows(accountsMarkdown)
            ?? throw new TestAccountException($"{FileName} has no '## {Section}' table with a Name column.");
        var secrets = secretsMarkdown is null
            ? null
            : Rows(secretsMarkdown) ?? throw new TestAccountException($"{SecretsFileName} has no '## {Section}' table with a Name column.");
        return new TestAccounts(accounts, secrets);
    }

    /// <summary>The accounts of <c>TestAccounts.md</c> at <paramref name="path"/> and the <c>TestAccounts.local.md</c> beside it.</summary>
    /// <param name="path">The path of <c>TestAccounts.md</c>.</param>
    public static TestAccounts ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var secretsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, SecretsFileName);
        return Read(File.ReadAllText(path), File.Exists(secretsPath) ? File.ReadAllText(secretsPath) : null);
    }

    /// <summary>The accounts of the first <c>TestAccounts.md</c> in <paramref name="folder"/> or a folder above it.</summary>
    /// <param name="folder">Where to start looking, such as the test's output folder.</param>
    public static TestAccounts FindFrom(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName, FileName);
            if (File.Exists(path))
            {
                return ReadFile(path);
            }
        }

        throw new TestAccountException($"{FileName} was not found in '{folder}' or a folder above it.");
    }

    /// <summary>The account named <paramref name="name"/>, with its secrets.</summary>
    /// <param name="name">The account's name, such as <c>NormalUser</c>.</param>
    /// <exception cref="TestAccountException">The account is not in <c>TestAccounts.md</c>, or has no username or password.</exception>
    public TestAccount Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_accounts.TryGetValue(name, out var row))
        {
            throw new TestAccountException($"Account {name} is not in {FileName}. Known: {string.Join(", ", _accounts.Keys)}.");
        }

        if (_secrets is null)
        {
            throw new TestAccountException($"{SecretsFileName} is missing beside {FileName}, so account {name} has no username or password.");
        }

        if (!_secrets.TryGetValue(name, out var secret))
        {
            throw new TestAccountException($"Account {name} has no row in {SecretsFileName}.");
        }

        var username = Cell(secret, "Username")
            ?? throw new TestAccountException($"Account {name} has no username in {SecretsFileName}.");
        var password = Cell(secret, "Password")
            ?? throw new TestAccountException($"Account {name} has no password in {SecretsFileName}.");
        var organization = Cell(secret, "Organization") ?? Cell(row, "Organization");
        var writes = !string.Equals(Cell(row, "Writes"), "no", StringComparison.OrdinalIgnoreCase);
        return new TestAccount(name, username, password, organization, writes, row);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>>? Rows(string markdown)
    {
        if (!MarkdownTables.ReadSections(markdown).TryGetValue(Section, out var table) || !table.Columns.Contains("Name"))
        {
            return null;
        }

        Dictionary<string, IReadOnlyDictionary<string, string>> rows = new(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            if (Cell(row.Cells, "Name") is { } name)
            {
                rows[name] = row.Cells;
            }
        }

        return rows;
    }

    private static string? Cell(IReadOnlyDictionary<string, string> row, string column)
        => row.TryGetValue(column, out var value) && value.Length > 0 ? value : null;
}
