using Bruinet.Maui.Containers;
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Range;

namespace Bruinet.Maui.CommunityToolkit.Controls.Media;

/// <summary>
/// CommunityToolkit.Maui <c>MediaElement</c>: audio and video playback with platform transport
/// controls.
/// Driven and read through its transport controls, one media element per scope; the
/// toolkit's <c>CurrentState</c> is not read, because it stays <c>Opening</c> on Windows.
/// Routes and platform limits: <c>docs/controls/community-toolkit.md</c>.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class MediaElement<TScope> : ComponentObjectBase<TScope, MediaElement<TScope>>
    where TScope : IMauiScope<TScope>
{
    private const string PlayPauseButtonId = "PlayPauseButton";
    private const string VolumeMuteButtonId = "VolumeMuteButton";
    private const string RepeatButtonId = "RepeatButton";
    private const string RewindButtonId = "RewindButton";
    private const string FastForwardButtonId = "FastForwardButton";
    private const string TimeElapsedId = "TimeElapsedElement";
    private const string TimeRemainingId = "TimeRemainingElement";
    private const string ProgressSliderId = "ProgressSlider";

    /// <summary>
    /// Creates a media element control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the media element.</param>
    public MediaElement(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a media element control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID).</param>
    public MediaElement(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Transport Controls

    /// <summary>The transport button that toggles playback.</summary>
    public MediaPlayPauseButton<MediaElement<TScope>> PlayPauseButton => new(this, PlayPauseButtonId);

    /// <summary>The transport button that toggles muted audio.</summary>
    public Button<MediaElement<TScope>> VolumeMuteButton => new(this, VolumeMuteButtonId);

    /// <summary>The transport button that changes repeat mode.</summary>
    public Button<MediaElement<TScope>> RepeatButton => new(this, RepeatButtonId);

    /// <summary>The transport button that rewinds playback.</summary>
    public Button<MediaElement<TScope>> RewindButton => new(this, RewindButtonId);

    /// <summary>The transport button that advances playback.</summary>
    public Button<MediaElement<TScope>> FastForwardButton => new(this, FastForwardButtonId);

    /// <summary>The transport slider that reports and changes playback progress, 0 to 100.</summary>
    public Slider<MediaElement<TScope>> ProgressSlider => new(this, ProgressSliderId);

    /// <summary>The transport readout of elapsed playback time.</summary>
    public MediaTimeLabel<MediaElement<TScope>> TimeElapsedLabel => new(this, TimeElapsedId);

    /// <summary>The transport readout of remaining playback time.</summary>
    public MediaTimeLabel<MediaElement<TScope>> TimeRemainingLabel => new(this, TimeRemainingId);

    #endregion

    #region Element Finding

    /// <summary>
    /// The element by its locator where the platform shows it; otherwise its play/pause button,
    /// which stands in for it as the Stepper's buttons stand in for a Stepper.
    /// </summary>
    protected override IMauiElement FindContainerRootElement()
        => Parent.TryFindElement(Locator)
           ?? Parent.TryFindElement(Locator.ByAutomationId(PlayPauseButtonId))
           ?? throw new ElementNotFoundException(
               $"MediaElement was not found by '{Locator}' or by its '{PlayPauseButtonId}'. The "
               + "transport controls must be shown (ShouldShowPlaybackControls).");

    /// <summary>
    /// Resolves transport controls from the parent because WinUI flattens them beside the
    /// unexposed MediaElement root.
    /// </summary>
    public override IMauiElement? TryFindElement(Locator locator)
        => Parent.TryFindElement(locator);

    /// <inheritdoc />
    public override IMauiElement FindElement(Locator locator)
        => Parent.FindElement(locator);

    /// <inheritdoc />
    public override IReadOnlyList<IMauiElement> FindElements(Locator locator)
        => Parent.FindElements(locator);

    #endregion

    #region Shortcuts

    /// <summary>Whether media is playing, as the play/pause button shows it.</summary>
    protected bool IsPlayingShortcut() => PlayPauseButton.IsPlaying();

    /// <summary>Pauses playback. No-op when not playing.</summary>
    protected MediaElement<TScope> PauseShortcut(int? timeoutMs = null) => PlayPauseButton.Pause(timeoutMs);

    /// <summary>Playback progress, 0 to 100, from the seek slider.</summary>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast)]
    protected double? GetProgressShortcut() => ProgressSlider.GetValue();

    /// <summary>Elapsed time in seconds; null while playing or before the media opens.</summary>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected double? GetElapsedShortcut() => TimeElapsedLabel.GetSeconds();

    /// <summary>Remaining time in seconds; null while playing or before the media opens.</summary>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast
        | Comparison.LessThan | Comparison.AtMost)]
    protected double? GetRemainingShortcut() => TimeRemainingLabel.GetSeconds();

    #endregion

    #region Hand-written: across parts

    /// <summary>
    /// Waits until the media has opened, shown by the remaining time getting its clock.
    /// </summary>
    /// <remarks>
    /// Reads the remaining-time readout. Meaningful while paused only: during playback Windows
    /// drops the clock from the name, so this would wait for the player to pause.
    /// </remarks>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>True when the media opened within the timeout.</returns>
    public bool WaitOpened(int? timeoutMs = null)
        => TimeRemainingLabel.WaitSecondsAtLeast(0, timeoutMs);

    /// <summary>
    /// Starts playback, first waiting for the media to open. No-op when already playing.
    /// </summary>
    /// <remarks>
    /// Across two parts: the play/pause button, and the remaining-time readout that says the media
    /// has opened. The button is enabled before the media opens and the player drops a press made
    /// then, so the button alone cannot do this (probed 2026-09-18).
    /// </remarks>
    /// <param name="timeoutMs">Optional timeout in milliseconds, for each of the two steps.</param>
    /// <returns>This media element, for chaining.</returns>
    /// <exception cref="TimeoutException">The media did not open.</exception>
    public MediaElement<TScope> Play(int? timeoutMs = null)
    {
        if (PlayPauseButton.IsPlaying() == true)
        {
            return this;
        }

        if (!WaitOpened(timeoutMs))
        {
            throw new TimeoutException(
                $"MediaElement '{Locator.Value}' never showed a remaining time, so its media did not open.");
        }

        return PlayPauseButton.Play(timeoutMs);
    }

    /// <summary>
    /// Plays or pauses. Null skips the operation.
    /// </summary>
    /// <remarks>Hand-written because <see cref="Play"/> is.</remarks>
    /// <param name="playing">True to play, false to pause, null to do nothing.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>This media element, for chaining.</returns>
    public MediaElement<TScope> SetPlaying(bool? playing, int? timeoutMs = null)
        => playing switch
        {
            true => Play(timeoutMs),
            false => Pause(timeoutMs),
            null => this
        };

    /// <summary>
    /// Stops playback: pauses, then seeks to the start.
    /// </summary>
    /// <remarks>
    /// Across two parts: the play/pause button and the seek slider. The WinUI transport shows no
    /// stop button (probed 2026-09-18), and seeking the slider moves playback.
    /// </remarks>
    /// <param name="timeoutMs">Optional timeout in milliseconds, for each of the two steps.</param>
    /// <returns>This media element, for chaining.</returns>
    public MediaElement<TScope> Stop(int? timeoutMs = null)
    {
        Pause(timeoutMs);
        return ProgressSlider.SetValue(0, timeoutMs);
    }

    /// <summary>
    /// Reads the media's length in whole seconds: elapsed plus remaining.
    /// </summary>
    /// <remarks>
    /// Across two parts: the elapsed and remaining readouts. Null while playing or before the
    /// media opens, when Windows drops the clock from the readouts' names.
    /// </remarks>
    /// <returns>The duration, or null while either readout shows no clock.</returns>
    public double? GetDuration()
        => TimeElapsedLabel.GetSeconds() + TimeRemainingLabel.GetSeconds();

    #endregion
}
