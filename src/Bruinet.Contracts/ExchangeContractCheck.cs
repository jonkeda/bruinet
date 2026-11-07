using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>What makes a recorded exchange usable as contract evidence.</summary>
/// <param name="Environment">The environment the run targets; an exchange from another one is blocked. Null accepts any.</param>
/// <param name="RequireRemoteHost">A call a local mock answered is blocked, so a mock never passes for the real service.</param>
public sealed record ContractEvidence(string? Environment = null, bool RequireRemoteHost = true)
{
    /// <summary>Environments never used as evidence, such as production, even when the run targets them.</summary>
    public IReadOnlySet<string> Never { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Checks one recorded exchange: first that it is usable evidence (right environment, real
/// service, complete JSON body), then the body against the endpoint's DTO contract.
/// </summary>
public static class ExchangeContractCheck
{
    public static ContractResult Check(ContractExchange? exchange, DtoContract contract, JsonSerializerOptions options, ContractEvidence evidence)
    {
        if (exchange is null)
            return Single(contract, ContractSeverity.Blocked, "The app did not call this endpoint during the run, so it was not observed.");

        if ((exchange.Environment is { } environment && evidence.Never.Contains(environment))
            || (evidence.Environment is { } target && evidence.Never.Contains(target)))
            return Single(contract, ContractSeverity.Blocked, $"{exchange.Environment ?? evidence.Environment} traffic is never used as contract evidence.");

        if (evidence.Environment is not null && !string.Equals(exchange.Environment, evidence.Environment, StringComparison.OrdinalIgnoreCase))
            return Single(contract, ContractSeverity.Blocked, $"The exchange came from {exchange.Environment ?? "an unknown environment"}, not {evidence.Environment}.");

        if (evidence.RequireRemoteHost && IsLoopback(ContractExchanges.Host(exchange.Path)))
            return Single(contract, ContractSeverity.Blocked, "The exchange was served by a local mock, not the real backend.");

        if (exchange.Error is not null)
            return Single(contract, ContractSeverity.Failure, $"The request failed: {exchange.Error}.");

        if (exchange.StatusCode is not (>= 200 and < 300))
            return Single(contract, ContractSeverity.Failure, $"The response status was {exchange.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "missing"}, not 2xx.");

        if (exchange.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
            return Single(contract, ContractSeverity.Failure, $"The response content type was '{exchange.ContentType}', not JSON.");

        if (exchange.IsTruncated)
        {
            // The app caps each recorded body, so a large list keeps only its leading items.
            if (TruncatedJson.CompleteArrayItems(exchange.ResponseBody) is not { } complete)
                return Single(contract, ContractSeverity.Blocked, "The recorded body was truncated, so it cannot be validated.");

            var partial = DtoContractValidator.Validate(complete.Json, contract, options);
            partial.Add(ContractSeverity.Warning, "$", $"The recorded body was truncated; only the first {complete.Count} complete items were checked.");
            return partial;
        }

        if (string.IsNullOrEmpty(exchange.ResponseBody))
            return Single(contract, ContractSeverity.Blocked, "The exchange log holds no response body for this call.");

        return DtoContractValidator.Validate(exchange.ResponseBody, contract, options);
    }

    private static ContractResult Single(DtoContract contract, ContractSeverity severity, string message)
    {
        var result = new ContractResult(contract.Endpoint);
        result.Add(severity, "$", message);
        return result;
    }

    private static bool IsLoopback(string? host)
        => host is not null
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)));
}
