using System.Text.RegularExpressions;

namespace Bruinet.Contracts;

/// <summary>An endpoint the app called that no registered contract covers.</summary>
public sealed record UnregisteredEndpoint(string Method, string? Host, string Path, int Count);

/// <summary>
/// Lists the endpoints the app called that have no contract, so a report shows which endpoints a
/// module still misses.
/// </summary>
public static partial class UnregisteredEndpoints
{
    /// <summary>
    /// Groups the exchanges that match none of <paramref name="contracts"/> by method, host and
    /// path, with numeric segments shown as <c>{id}</c>.
    /// </summary>
    public static IReadOnlyList<UnregisteredEndpoint> Find(IEnumerable<ContractExchange> exchanges, IEnumerable<DtoContract> contracts)
    {
        var contractList = contracts.ToList();
        return exchanges
            .Where(exchange => !contractList.Any(contract =>
                exchange.Method.Equals(contract.Method, StringComparison.OrdinalIgnoreCase)
                && contract.MatchesPath(ContractExchanges.AbsolutePath(exchange.Path))))
            .GroupBy(exchange => (
                Method: exchange.Method.ToUpperInvariant(),
                Host: ContractExchanges.Host(exchange.Path),
                Path: NumericSegment().Replace(ContractExchanges.AbsolutePath(exchange.Path), "/{id}")))
            .Select(group => new UnregisteredEndpoint(group.Key.Method, group.Key.Host, group.Key.Path, group.Count()))
            .OrderBy(endpoint => endpoint.Host, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Path, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ToList();
    }

    [GeneratedRegex(@"/[0-9]+(?=/|$)")]
    private static partial Regex NumericSegment();
}
