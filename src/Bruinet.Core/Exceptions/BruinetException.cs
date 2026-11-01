namespace Bruinet.Core.Exceptions;

/// <summary>
/// Base exception for all Bruinet framework exceptions.
/// </summary>
public class BruinetException : Exception
{
    public BruinetException(string message) : base(message) { }

    public BruinetException(string message, Exception innerException)
        : base(message, innerException) { }
}
