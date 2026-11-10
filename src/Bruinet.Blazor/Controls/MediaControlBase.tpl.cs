using System.Globalization;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Blazor.Controls;

/// <summary>
/// Base class for &lt;audio&gt; and &lt;video&gt;: play, pause, seek, volume and mute, read
/// from the element's DOM properties.
/// </summary>
public abstract partial class MediaControlBase<TScope> : ClickableControlBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    protected MediaControlBase(IHtmlScope<TScope> scope, Locator locator) : base(scope, locator) { }
    protected MediaControlBase(IHtmlScope<TScope> scope, string selectorOrId) : base(scope, selectorOrId) { }

    #region Playback - Core Methods

    /// <summary>Starts playback.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void PlayCore(IHtmlElement element, int? timeoutMs = null) => element.Evaluate("el => el.play()");

    /// <summary>Pauses playback, and confirms it is paused.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void PauseCore(IHtmlElement element, int? timeoutMs = null)
        => ActAndConfirm(
            "Pause", () => element.Evaluate("el => el.pause()"),
            () => IsPausedCore(element), paused => paused == true,
            "paused", timeoutMs);

    /// <summary>Whether the media is playing: neither paused nor ended.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsPlayingCore(IHtmlElement? element)
        => element == null ? null : !Flag(element, "paused") && !Flag(element, "ended");

    /// <summary>Whether the media is paused.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsPausedCore(IHtmlElement? element) => element == null ? null : Flag(element, "paused");

    /// <summary>Whether the media has played to its end.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsEndedCore(IHtmlElement? element) => element == null ? null : Flag(element, "ended");

    /// <summary>The playback position, in seconds.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast)]
    protected virtual double? GetCurrentTimeCore(IHtmlElement element) => Number(element, "currentTime", 0);

    /// <summary>Moves the playback position to <paramref name="seconds"/>, and confirms it moved.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="seconds">The position, in seconds.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SeekCore(IHtmlElement element, double seconds, int? timeoutMs = null)
        => ActAndConfirm(
            "Seek", () => element.Evaluate($"el => el.currentTime = {seconds.ToString(CultureInfo.InvariantCulture)}"),
            () => GetCurrentTimeCore(element), position => position is { } at && Math.Abs(at - seconds) < 0.5,
            $"position {seconds}s", timeoutMs);

    /// <summary>The media's length, in seconds.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetDurationCore(IHtmlElement element) => Number(element, "duration", 0);

    #endregion

    #region Volume - Core Methods

    /// <summary>The volume, from 0 to 1.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual double? GetVolumeCore(IHtmlElement element) => Number(element, "volume", 1);

    /// <summary>Sets the volume (clamped to 0-1), and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="volume">The volume; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetVolumeCore(IHtmlElement element, double? volume, int? timeoutMs = null)
    {
        if (volume is not { } requested)
            return;

        var target = Math.Clamp(requested, 0, 1);
        ActAndConfirm(
            "SetVolume", () => element.Evaluate($"el => el.volume = {target.ToString(CultureInfo.InvariantCulture)}"),
            () => GetVolumeCore(element), actual => actual is { } v && Math.Abs(v - target) < 0.001,
            $"volume {target}", timeoutMs);
    }

    /// <summary>Whether the media is muted.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsMutedCore(IHtmlElement? element) => element == null ? null : Flag(element, "muted");

    /// <summary>Mutes the media, and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void MuteCore(IHtmlElement element, int? timeoutMs = null)
        => ActAndConfirm(
            "Mute", () => element.Evaluate("el => el.muted = true"),
            () => IsMutedCore(element), muted => muted == true,
            "muted", timeoutMs);

    /// <summary>Unmutes the media, and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void UnmuteCore(IHtmlElement element, int? timeoutMs = null)
        => ActAndConfirm(
            "Unmute", () => element.Evaluate("el => el.muted = false"),
            () => IsMutedCore(element), muted => muted == false,
            "not muted", timeoutMs);

    #endregion

    /// <summary>The media's source: its <c>src</c>, or the source the browser chose.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetSourceCore(IHtmlElement element)
        => element.GetDomAttribute("src") ?? element.GetDomProperty("currentSrc");

    private static bool Flag(IHtmlElement element, string property)
        => string.Equals(element.GetDomProperty(property), "true", StringComparison.OrdinalIgnoreCase);

    private static double Number(IHtmlElement element, string property, double fallback)
        => double.TryParse(element.GetDomProperty(property), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}
