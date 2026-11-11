namespace Bruinet.Core.Exceptions;

/// <summary>
/// A test failed while it set up its state, before it did what it tests: a mock it could not stub,
/// a page it could not reach. Reported as kind <c>setup</c>, so a broken setup is not read as a
/// broken feature.
/// </summary>
public sealed class UiSetupException : BruinetException
{
    /// <summary>Wraps what went wrong while setting up.</summary>
    /// <param name="cause">The exception the setup threw.</param>
    public UiSetupException(Exception cause)
        : base($"Setup failed: {cause?.Message}", cause ?? throw new ArgumentNullException(nameof(cause)))
    {
    }
}
