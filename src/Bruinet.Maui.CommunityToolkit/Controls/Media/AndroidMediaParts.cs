using System.Text.RegularExpressions;
using Bruinet.Maui.Controls.Range;

namespace Bruinet.Maui.CommunityToolkit.Controls.Media;

/// <summary>
/// Android's Media3 controls, read for what they do not show (R2-T121): the remaining time and the progress, worked out
/// from the elapsed and duration labels inside the controller (<c>exo_controller</c>), which is this part's element.
/// </summary>
internal static class AndroidMediaClock
{
    private static readonly Regex Clock = new(@"(?:(\d+):)?(\d{1,2}):(\d{2})\s*$", RegexOptions.CultureInvariant);

    /// <summary>The elapsed and total seconds the controller shows; null while either label shows none.</summary>
    public static (double Elapsed, double Duration)? Read(IMauiElement? controller)
        => controller is not null
           && Seconds(controller, "exo_position") is { } elapsed
           && Seconds(controller, "exo_duration") is { } duration
            ? (elapsed, duration)
            : null;

    private static double? Seconds(IMauiElement controller, string id)
    {
        if (controller.TryFindElement(Locator.ByXPath($"//*[contains(@resource-id, ':id/{id}')]"))?.Name is not { } name
            || Clock.Match(name) is not { Success: true } match)
        {
            return null;
        }

        return (match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0) * 3600
               + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60
               + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    }
}

/// <summary>Android's remaining time: duration less elapsed, as Media3 shows no remaining readout.</summary>
internal sealed class AndroidRemainingTime<TScope>(IMauiScope<TScope> scope, Locator controller)
    : MediaTimeLabel<TScope>(scope, controller)
    where TScope : IMauiScope<TScope>
{
    protected override double? GetSecondsCore(IMauiElement? element)
        => AndroidMediaClock.Read(element) is { } clock ? clock.Duration - clock.Elapsed : null;
}

/// <summary>Android's progress in percent: Media3's time bar publishes no value to read.</summary>
internal sealed class AndroidMediaProgress<TScope>(IMauiScope<TScope> scope, Locator controller)
    : Slider<TScope>(scope, controller)
    where TScope : IMauiScope<TScope>
{
    protected override double? GetValueCore(IMauiElement? element)
        => AndroidMediaClock.Read(element) is { Duration: > 0 } clock ? clock.Elapsed / clock.Duration * 100 : null;
}
