using System.Text.RegularExpressions;
using Bruinet.Maui.Controls.Display;

namespace Bruinet.Maui.CommunityToolkit.Controls.Media;

/// <summary>
/// One of the media transport's time readouts: elapsed or remaining playback time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Windows</b>: the WinUI transport names its <c>TimeElapsedElement</c> and
/// <c>TimeRemainingElement</c> "Time elapsed 00:00:03" and "Time remaining 00:00:03". The name
/// carries the clock only while the media is open and not playing. Before the media opens, and
/// during playback, it is just "Time elapsed", and <c>GetSeconds</c> answers null; the clock is
/// then only in the Text pattern, which the driver does not read.
/// </para>
/// <para>
/// So <c>WaitSecondsAtLeast(0)</c> on the remaining time is "the media has opened" for a paused
/// player (probed 2026-09-18: no clock at 311 ms, "00:00:06" at 393 ms).
/// </para>
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class MediaTimeLabel<TScope> : Label<TScope>
    where TScope : IMauiScope<TScope>
{
    // h:mm:ss (WinUI), or mm:ss (Android's Media3 player, exo_position and exo_duration, R2-T121).
    private static readonly Regex Clock = new(@"(?:(\d+):)?(\d{1,2}):(\d{2})\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Creates a time readout within the specified scope.
    /// </summary>
    /// <param name="scope">The scope providing element finding.</param>
    /// <param name="locator">The locator for the readout.</param>
    public MediaTimeLabel(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a time readout within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID).</param>
    public MediaTimeLabel(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Reads the time shown, in whole seconds, from the trailing h:mm:ss (or mm:ss) of the name.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The seconds, or null while the readout shows no clock.</returns>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected virtual double? GetSecondsCore(IMauiElement? element)
    {
        if (element?.Name is not { } name || Clock.Match(name) is not { Success: true } match)
        {
            return null;
        }

        return (match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0) * 3600
               + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60
               + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    }

    #endregion
}
