using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Calls;

/// <summary>
/// Every observation of one phase of a call, and the failure built from them.
/// </summary>
public sealed class ObservationLog
{
    private readonly List<(Observation Observation, long AtMs)> _entries = [];
    private readonly HashSet<string> _instanceKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, int> _failures = [];
    private int _staleCount;

    /// <summary>The most recent observation, or a <see cref="ObservationKind.Pending"/> placeholder.</summary>
    public Observation Last => _entries.Count == 0 ? Observation.Pending("no attempt was made") : _entries[^1].Observation;

    /// <summary>How many attempts were made.</summary>
    public int Attempts => _entries.Count;

    /// <summary>When the last attempt ended, in milliseconds into the phase: how long the poll took.</summary>
    public long LastAtMs => _entries.Count == 0 ? 0 : _entries[^1].AtMs;

    /// <summary>
    /// How often the element was replaced: distinct instances seen beyond the first, or stale
    /// answers, whichever says more.
    /// </summary>
    public int Replacements => Math.Max(Math.Max(0, _instanceKeys.Count - 1), _staleCount);

    /// <summary>Adds what an attempt saw, at <paramref name="atMs"/> into the call.</summary>
    public void Add(Observation observation, long atMs)
    {
        _entries.Add((observation, atMs));

        if (!string.IsNullOrEmpty(observation.InstanceKey))
        {
            _instanceKeys.Add(observation.InstanceKey);
        }

        if (observation.Kind == ObservationKind.Stale)
        {
            _staleCount++;
        }

        if (observation is { Kind: ObservationKind.Failed, Error: { } error })
        {
            _failures[error.GetType()] = _failures.GetValueOrDefault(error.GetType()) + 1;
        }
    }

    /// <summary>"3 attempts; replaced 2 times; last: NotReady(Disabled) at 1800 ms".</summary>
    public string Summary()
    {
        if (_entries.Count == 0)
        {
            return "no attempt was made";
        }

        var replaced = Replacements > 0 ? $"; replaced {Times(Replacements)}" : string.Empty;
        var (last, at) = _entries[^1];
        return $"{Attempts} attempt{(Attempts == 1 ? "" : "s")}{replaced}; last: {last} at {at} ms";
    }

    /// <summary>
    /// The exception a phase that ran out of time throws, built from what it last saw.
    /// </summary>
    /// <param name="locator">The control's locator, for the message.</param>
    /// <param name="budgetMs">The phase's budget, for the message.</param>
    /// <param name="notFound">The control's own "not found" exception, when the last attempt found nothing.</param>
    /// <param name="scopeNotReady">The exception for a scope that never became ready.</param>
    public Exception ToException(
        Locator? locator,
        int budgetMs,
        Func<ElementNotFoundException> notFound,
        Func<ScopeReadiness, Exception> scopeNotReady)
    {
        var last = Last;
        var history = Replacements > 0 ? $"(replaced {Times(Replacements)} in {budgetMs} ms)" : null;

        return last.Kind switch
        {
            ObservationKind.ScopeNotReady when last.Scope is { } scope => scopeNotReady(scope),
            ObservationKind.Missing => notFound(),
            ObservationKind.NotReady when locator is not null => new ElementNotReadyException(
                locator,
                last.Reason ?? NotReadyReason.Other,
                Join(last.Detail, $"still after {budgetMs} ms", history)),
            ObservationKind.Stale or ObservationKind.ItemChanged => new StaleElementException(
                locator,
                last.Error,
                Join(last.Kind == ObservationKind.ItemChanged ? last.Detail : "It was found, then gone", history)),
            ObservationKind.Mismatch when last.Error is { } mismatch => mismatch,
            ObservationKind.Pending => new WaitTimeoutException(
                $"'{locator}' did not reach the state waited for within {budgetMs} ms. {Summary()}.", budgetMs),
            ObservationKind.Failed when last.Error is { } error => Unexpected(error, $"'{locator}'", budgetMs),
            _ => last.Error ?? new WaitTimeoutException(
                $"'{locator}' did not complete within {budgetMs} ms. {Summary()}.", budgetMs)
        };
    }

    /// <summary>
    /// The failure of a phase whose last attempt raised <paramref name="error"/>, an exception no
    /// observation explains. After one attempt it is the answer as it is; after retries it is
    /// wrapped in a <see cref="WaitTimeoutException"/> naming its type, how many attempts raised
    /// it, and its message. It is never reported as "not found".
    /// </summary>
    /// <param name="error">The last attempt's exception.</param>
    /// <param name="subject">What the call was about, for the message.</param>
    /// <param name="budgetMs">The phase's budget.</param>
    public Exception Unexpected(Exception error, string subject, int budgetMs)
    {
        if (Attempts <= 1)
        {
            return error;
        }

        var raised = _failures.GetValueOrDefault(error.GetType());
        return new WaitTimeoutException(
            $"{subject} did not complete within {budgetMs} ms: {raised} of {Attempts} attempts raised " +
            $"{error.GetType().Name}. Last: {error.Message}",
            error);
    }

    private static string Times(int count) => count == 1 ? "1 time" : $"{count} times";

    private static string Join(params string?[] parts)
        => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
