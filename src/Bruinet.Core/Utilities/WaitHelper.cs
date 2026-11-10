using System.Diagnostics;

namespace Bruinet.Core.Utilities;

/// <summary>
/// The poll's pause, between one attempt of a call and the next. A wait below a call is
/// <c>Bruinet.Core.Calls.Settle</c>.
/// </summary>
internal static class WaitHelper
{
    /// <summary>
    /// Pauses execution for a short polling/settling interval: the poll's pause between attempts.
    /// It blocks rather than spins, so a suite's parallel collections leave the cores to the app
    /// and the database they wait for.
    /// </summary>
    /// <param name="milliseconds">Pause duration in milliseconds.</param>
    public static void Pause(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            Thread.Yield();
            return;
        }

        Thread.Sleep(milliseconds);
    }
}
