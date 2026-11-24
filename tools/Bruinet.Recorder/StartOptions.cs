using Bruinet.Scraper.Projects;

namespace Bruinet.Recorder;

/// <summary>What the Recorder is started with: <c>--folder</c>, <c>--url</c> and <c>--as</c>, each optional.</summary>
/// <param name="Folder">The Scraper project to save in; null when none is known.</param>
/// <param name="Url">The address to open; null for where the project's settings left off.</param>
/// <param name="Account">Whose sign-in the webview keeps; <c>own</c> when left out.</param>
public sealed record StartOptions(string? Folder, string? Url, string Account)
{
    /// <summary>The account when none is named.</summary>
    public const string OwnAccount = "own";

    /// <summary>
    /// The account name that stands for the one the person is signed in to the computer with: the webview then signs
    /// in to what takes that account without asking, and shows the device as a browser of the computer does.
    /// </summary>
    public const string OsAccount = "os";

    /// <summary>True when the webview may sign in with the computer's own account (<c>--as os</c>).</summary>
    public bool UseOsAccount => string.Equals(Account, OsAccount, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the options; the folder is the current one when it is a Scraper project and none is named.</summary>
    public static StartOptions Parse(IReadOnlyList<string> arguments, string currentDirectory)
    {
        string? Value(string name)
        {
            for (var i = 0; i < arguments.Count - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                    return arguments[i + 1];
            }

            return null;
        }

        var folder = Value("--folder") is { Length: > 0 } given ? Path.GetFullPath(given, currentDirectory)
            : File.Exists(Path.Combine(currentDirectory, ScraperConfig.FileName)) ? currentDirectory
            : null;
        var account = Value("--as") is { Length: > 0 } named && named.All(c => char.IsLetterOrDigit(c) || c is '-' or '_') ? named : OwnAccount;
        return new StartOptions(folder, Value("--url"), account);
    }

    /// <summary>Where the webview keeps an account's sign-in: under the user's profile, never in a repository.</summary>
    public string ProfileFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bruinet", "Recorder", "profiles", Account);
}
