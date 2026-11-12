using System.Globalization;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Recording;
using Bruinet.Maui.Interfaces;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// One of a media element's transport controls, which the platform draws outside any tree the agent
/// reads, standing in under the id WinUI gives it (<c>PlayPauseButton</c>, <c>ProgressSlider</c>, the
/// time readouts), as a Stepper's buttons stand in. It reads the element's state, position and
/// duration from the agent, and plays, pauses and seeks through the app's bridge (its <c>media</c>
/// route), so Bruinet's MediaElement control object works unchanged.
/// </summary>
internal sealed class DevFlowMediaPart : IMauiElement
{
    /// <summary>The transport controls a page object asks for, by the ids WinUI's MediaPlayerElement gives them.</summary>
    public static readonly IReadOnlySet<string> Parts = new HashSet<string>(StringComparer.Ordinal)
    {
        PlayPause, Progress, Elapsed, Remaining, "VolumeMuteButton", "RepeatButton", "RewindButton", "FastForwardButton",
    };

    private const string PlayPause = "PlayPauseButton";
    private const string Progress = "ProgressSlider";
    private const string Elapsed = "TimeElapsedElement";
    private const string Remaining = "TimeRemainingElement";

    private readonly DevFlowMauiDriver _driver;
    private readonly string _mediaId;
    private readonly DevFlowMauiElement _media;
    private readonly string _part;

    public DevFlowMediaPart(DevFlowMauiDriver driver, string mediaId, string part)
    {
        _driver = driver;
        _mediaId = mediaId;
        _media = new DevFlowMauiElement(driver, mediaId);
        _part = part;
    }

    private DevFlowNode Node
        => _driver.Snapshot().Find(_mediaId)
           ?? throw new StaleElementException(Locator.ByAutomationId(_part), detail: "Its media element has left the app's tree.");

    private string MediaAutomationId => Node.AutomationId ?? _mediaId;

    private TimeSpan? Read(string property)
        => TimeSpan.TryParse(_driver.Agent.GetProperty(_mediaId, property), CultureInfo.InvariantCulture, out var value) ? value : null;

    private bool Playing => _driver.Agent.GetProperty(_mediaId, "CurrentState") == "Playing";

    // WinUI's readouts: h:mm:ss.
    private static string Clock(TimeSpan time)
        => string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}");

    #region State

    public bool Visible => _media.Visible;

    public bool Enabled => _media.Enabled;

    public bool Selected => false;

    /// <summary>
    /// What the control shows: "Pause" while playing and "Play" otherwise, as WinUI names the button;
    /// the elapsed and remaining times; nothing yet for the remaining time before the media opened.
    /// </summary>
    public string? Text => _part switch
    {
        PlayPause => Playing ? "Pause" : "Play",
        Elapsed => Read("Position") is { } position ? Clock(position) : null,
        Remaining => Read("Duration") is { TotalSeconds: > 0 } duration
            ? Clock(duration - (Read("Position") ?? TimeSpan.Zero) is { Ticks: > 0 } left ? left : TimeSpan.Zero)
            : null,
        _ => null,
    };

    public string? Name => _part == Progress ? "Seek" : Text ?? _part;

    public string? TagName => _part switch
    {
        Progress => "Slider",
        Elapsed or Remaining => "Text",
        _ => "Button",
    };

    public Rectangle Rect => _media.Rect;

    public Point Location => Rect.Location;

    public Size Size => Rect.Size;

    public string InstanceKey => _mediaId + "/" + _part;

    public string? AutomationId => _part;

    public bool Focused => false;

    public string? Hint => null;

    /// <summary>The slider's position, in seconds, as WinUI's progress slider gives it.</summary>
    public double? RangeValue => _part == Progress ? Read("Position")?.TotalSeconds : null;

    public double? RangeMinimum => _part == Progress ? 0 : null;

    public double? RangeMaximum => _part == Progress ? Read("Duration")?.TotalSeconds : null;

    public double? RangeSmallChange => _part == Progress ? 1 : null;

    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.ToLowerInvariant() switch
        {
            "classname" or "class" or "controltype" => TagName,
            "name" => Name,
            "automationid" => AutomationId,
            "enabled" => Enabled.ToString(),
            "visible" => Visible.ToString(),
            _ => null,
        };
    }

    #endregion

    #region Actions

    public void Click() => Invoke();

    /// <summary>The play/pause button turns playback over; the others have no route here.</summary>
    public void Invoke()
    {
        if (_part != PlayPause)
            throw NoRoute($"press {_part}");

        using var mark = Interactions.Report(InteractionKind.Tap, () => Rect, () => AutomationId);
        Media(Playing ? "Pause" : "Play");
    }

    public void SetRangeValue(double value)
    {
        if (_part != Progress)
            throw NoRoute($"set {_part}");

        using var mark = Interactions.Report(InteractionKind.Type, () => Rect, () => AutomationId);
        Media("SeekTo", value);
    }

    private void Media(string action, double seconds = 0)
    {
        if (!Enabled || !Visible)
            throw new ElementNotReadyException(Locator.ByAutomationId(_part), Enabled ? NotReadyReason.NotVisible : NotReadyReason.Disabled, $"cannot {action}");

        var answer = _driver.Agent.Media(MediaAutomationId, action, seconds);
        _driver.Changed();
        if (answer.Delivered)
            return;
        throw answer.Declared
            ? new BruinetException($"'{MediaAutomationId}' could not {action}: {answer.Reason}.")
            : new RouteUnavailableException(
                $"'{MediaAutomationId}' cannot {action} on the DevFlow runner: the agent calls no methods, and the app carries no Bruinet bridge "
                + $"(AddBruinetBridge, BRUINET_UIA_BRIDGE=1). {answer.Reason}");
    }

    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys) => throw NoRoute("type into");

    public void Clear() => throw NoRoute("clear");

    public void DoubleClick() => throw NoRoute("double-click");

    public void RightClick() => throw NoRoute("right-click");

    public void Hover() => throw NoRoute("hover over");

    public void LongPress(int durationMs = 1000) => throw NoRoute("long-press");

    public void ScrollIntoView(int timeoutMs) => _media.ScrollIntoView(timeoutMs);

    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500) => throw NoRoute("swipe");

    public void Submit() => throw NoRoute("submit");

    #endregion

    #region Child lookup

    public IMauiElement? TryFindElement(Locator locator) => null;

    public IReadOnlyList<IMauiElement> FindElements(Locator locator) => [];

    #endregion

    #region DOM Access (Hybrid Apps)

    public string? GetDomAttribute(string attributeName) => null;

    public string? GetDomProperty(string propertyName) => null;

    public string? GetCssValue(string propertyName) => null;

    #endregion

    private RouteUnavailableException NoRoute(string what)
        => new($"The DevFlow runner cannot {what} on '{MediaAutomationId}': the platform draws its transport controls outside the agent's tree, "
               + "and only play, pause and seek stand in for them.");
}
