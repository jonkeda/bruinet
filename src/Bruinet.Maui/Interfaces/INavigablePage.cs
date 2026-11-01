namespace Bruinet.Maui.Interfaces;

/// <summary>
/// Page object that can leave itself through back/cancel/close behavior.
/// </summary>
public interface INavigablePage
{
    /// <summary>
    /// Gets whether this page is currently loaded.
    /// </summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds for load detection.</param>
    /// <returns>True when the page is loaded.</returns>
    bool IsLoaded(int? timeoutMs = null);

    /// <summary>
    /// Attempts to leave the page.
    /// </summary>
    /// <returns>True when a leave action was performed.</returns>
    bool TryLeave();
}
