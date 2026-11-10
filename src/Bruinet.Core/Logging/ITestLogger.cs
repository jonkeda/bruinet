namespace Bruinet.Core.Logging;

/// <summary>
/// Where Bruinet writes what a test did: one <see cref="LogEvent"/> per event (public API review,
/// proposal 10). A new field is a new property on the record, so it breaks no logger.
/// </summary>
public interface ITestLogger : IDisposable
{
    /// <summary>Writes one event.</summary>
    void Write(LogEvent entry);

    /// <summary>Writes out anything buffered.</summary>
    void Flush();
}
