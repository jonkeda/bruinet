namespace Bruinet.Core.Logging;

/// <summary>
/// Null logger implementation that discards all messages.
/// Used as default when no logger is configured.
/// </summary>
public class NullTestLogger : ITestLogger
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static NullTestLogger Instance { get; } = new();

    /// <inheritdoc />
    public void Write(LogEvent entry) { }

    public void Flush() { }

    public void Dispose() { }
}
