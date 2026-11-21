using System.Net;
using System.Text.Json;
using Bruinet.Core.Api;

namespace Bruinet.Html.Playwright;

/// <summary>
/// A browser's saved sign-in (the storage-state file <see cref="PlaywrightTestContext.SaveStorageState"/> writes) as
/// the sign-in of an API session: the web session's cookies get the token the web app itself calls its back end with.
/// </summary>
public static class SavedSignIn
{
    /// <summary>The cookies of a saved sign-in that a host would be sent.</summary>
    /// <param name="statePath">The storage-state file.</param>
    /// <param name="host">The host the cookies are for, such as the web app's.</param>
    /// <exception cref="ApiCallException">There is no such file: the account never signed in.</exception>
    public static CookieContainer Cookies(string statePath, string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (!File.Exists(statePath))
        {
            throw new ApiCallException($"There is no saved sign-in at {statePath}: run a test that signs in first.");
        }

        var cookies = new CookieContainer();
        using var state = JsonDocument.Parse(File.ReadAllText(statePath));
        if (!state.RootElement.TryGetProperty("cookies", out var saved) || saved.ValueKind != JsonValueKind.Array)
        {
            return cookies;
        }

        foreach (var cookie in saved.EnumerateArray())
        {
            var domain = cookie.GetProperty("domain").GetString() ?? string.Empty;
            if (host.EndsWith(domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            {
                cookies.Add(new Cookie(cookie.GetProperty("name").GetString()!, cookie.GetProperty("value").GetString(), cookie.GetProperty("path").GetString(), domain));
            }
        }

        return cookies;
    }

    /// <summary>The sign-in of an API session from a saved sign-in: its cookies, sent to where the web app gets its token.</summary>
    /// <param name="statePath">The storage-state file.</param>
    /// <param name="tokenAddress">Where the web app gets its token, such as <c>https://app/auth/token</c>.</param>
    /// <param name="who">The account's name, for the message when the saved sign-in ran out.</param>
    public static BearerSignIn Token(string statePath, Uri tokenAddress, string who)
    {
        ArgumentNullException.ThrowIfNull(tokenAddress);
        return ApiSignIns.FromCookies(Cookies(statePath, tokenAddress.Host), tokenAddress, who);
    }
}
