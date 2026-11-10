namespace Bruinet.Contracts;

/// <summary>
/// The run settings that decide whether a live contract test may call the real API.
/// </summary>
/// <remarks>
/// The <c>*Setting</c> names appear in skip reasons, so an app can name its own configuration
/// keys, e.g. <c>mockBackend.enabled</c>.
/// </remarks>
public sealed record LiveContractSettings
{
    /// <summary>Whether this run allows calls to the real API.</summary>
    public bool LiveApi { get; init; }

    /// <summary>Whether the app is pointed at a mock backend.</summary>
    public bool MockBackend { get; init; }

    /// <summary>Whether the app signs in to the real backend rather than a mock sign-in.</summary>
    public bool RealSignIn { get; init; }

    /// <summary>The test account this run signs in with.</summary>
    public string? Account { get; init; }

    /// <summary>Whether <see cref="Account"/> has credentials in the local account file.</summary>
    public bool AccountDefined { get; init; } = true;

    /// <summary>The setting that turns <see cref="LiveApi"/> on.</summary>
    public string LiveApiSetting { get; init; } = "the live API setting";

    /// <summary>The setting that turns <see cref="MockBackend"/> on.</summary>
    public string MockBackendSetting { get; init; } = "the mock backend setting";

    /// <summary>The setting that chooses the sign-in.</summary>
    public string SignInSetting { get; init; } = "the sign-in mode";

    /// <summary>The setting that names <see cref="Account"/>.</summary>
    public string AccountSetting { get; init; } = "the test account";

    /// <summary>The file that holds the account credentials.</summary>
    public string AccountFile { get; init; } = LocalAccountStore.DefaultFileName;
}

/// <summary>
/// Decides whether a live contract test runs: only on a live run, against the real backend,
/// signed in as the account the test needs.
/// </summary>
/// <remarks>
/// Framework-free; a test framework attribute sets its skip reason from <see cref="SkipReason"/>.
/// Each test names its account because the server's answers depend on that account's rights and
/// data.
/// </remarks>
public static class LiveContractGate
{
    /// <summary>
    /// Why a test that needs <paramref name="account"/> must skip, or <c>null</c> when it may run.
    /// </summary>
    public static string? SkipReason(LiveContractSettings settings, string account)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        if (!settings.LiveApi)
        {
            return $"Live contract test: set {settings.LiveApiSetting} to true.";
        }

        if (settings.MockBackend)
        {
            return $"Live contract test: {settings.MockBackendSetting} must be off so the app calls the real backend.";
        }

        if (!settings.RealSignIn)
        {
            return $"Live contract test: {settings.SignInSetting} must sign in to the real backend.";
        }

        if (!string.Equals(settings.Account, account, StringComparison.OrdinalIgnoreCase))
        {
            return $"Live contract test: needs {settings.AccountSetting} '{account}', but this run uses '{settings.Account}'.";
        }

        if (!settings.AccountDefined)
        {
            return $"Live contract test: account '{account}' is not defined in {settings.AccountFile}.";
        }

        return null;
    }
}
