namespace Bruinet.Core.Exceptions;

/// <summary>
/// The platform or the app was asked and said no: the pattern was there and refused, the game refused the
/// action, the picker has no such item. An answer about this element, not a fault in Bruinet or the driver.
/// </summary>
/// <remarks>
/// Bruinet used <see cref="InvalidOperationException"/> for this, which .NET also throws for a great many
/// bugs, so a catch meaning "this candidate refused" could not tell a refusal from a fault (review 05).
/// Catch this type where a refusal is an answer; let <see cref="InvalidOperationException"/> through.
/// </remarks>
public sealed class ActionRefusedException : BruinetException
{
    /// <summary>Creates the report of an action that was refused.</summary>
    /// <param name="message">What was asked, of what, and what refused it.</param>
    public ActionRefusedException(string message) : base(message)
    {
    }

    /// <summary>Creates the report of an action that was refused, with the platform's own error.</summary>
    /// <param name="message">What was asked, of what, and what refused it.</param>
    /// <param name="innerException">The platform's error.</param>
    public ActionRefusedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
