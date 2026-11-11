using Bruinet.Core.Scopes;

namespace Bruinet.Core.Exceptions;

/// <summary>
/// A scope a call stands in (a page, a container, a frame, a row) did not become ready within the
/// call's budget, or is misconfigured.
/// </summary>
public sealed class ScopeNotReadyException : BruinetException
{
    /// <summary>Creates the report of a scope that was not ready.</summary>
    /// <param name="readiness">The last readiness answer.</param>
    /// <param name="message">The full message, naming the call and its budget.</param>
    public ScopeNotReadyException(ScopeReadiness readiness, string message)
        : base(message)
    {
        Readiness = readiness;
    }

    /// <summary>The last readiness answer: which scope, and what it found.</summary>
    public ScopeReadiness Readiness { get; }

    /// <summary>The control or container whose call waited for the scope, when the call knows it.</summary>
    public Bruinet.Core.Locators.Locator? Locator { get; init; }
}

