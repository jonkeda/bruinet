namespace Bruinet.Core.Exceptions;

/// <summary>
/// Exception thrown when a wait operation times out.
/// </summary>
public class WaitTimeoutException : BruinetException
{
    public int TimeoutMs { get; }
    public string? Condition { get; }

    public WaitTimeoutException(string message) : base(message) { }

    public WaitTimeoutException(string message, int timeoutMs) : base(message)
    {
        TimeoutMs = timeoutMs;
    }

    public WaitTimeoutException(string message, int timeoutMs, string condition) : base(message)
    {
        TimeoutMs = timeoutMs;
        Condition = condition;
    }

    public WaitTimeoutException(string message, Exception innerException)
        : base(message, innerException) { }
}
