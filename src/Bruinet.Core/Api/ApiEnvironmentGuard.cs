namespace Bruinet.Core.Api;

/// <summary>
/// The hosts a suite may call, checked before every call of an <see cref="ApiSession"/>: a suite for an acceptance
/// environment names its acceptance hosts, and no test can reach another. A host is a name (<c>api-acc.example.com</c>)
/// or a pattern with a leading star (<c>*-acc.example.com</c>).
/// </summary>
public sealed class ApiEnvironmentGuard
{
    private readonly string[] _hosts;

    /// <summary>A guard for the hosts a suite names.</summary>
    /// <param name="hosts">The hosts; at least one.</param>
    public ApiEnvironmentGuard(IEnumerable<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        _hosts = [.. hosts.Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim())];
        if (_hosts.Length == 0)
        {
            throw new ArgumentException("An API session needs the hosts it may call: none were named.", nameof(hosts));
        }
    }

    /// <summary>The hosts allowed.</summary>
    public IReadOnlyList<string> Hosts => _hosts;

    /// <summary>Whether a host is one the suite may call.</summary>
    /// <param name="host">The host of an address.</param>
    public bool Allows(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return _hosts.Any(allowed => allowed.StartsWith('*')
            ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Refuses an address whose host the suite did not name.</summary>
    /// <param name="address">The absolute address about to be called.</param>
    /// <exception cref="ApiCallException">The host is not allowed.</exception>
    public void Check(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || !Allows(address.Host))
        {
            throw new ApiCallException($"{(address.IsAbsoluteUri ? address.Host : address.ToString())} is not a host this suite may call ({string.Join(", ", _hosts)}): the call is refused.");
        }
    }
}
