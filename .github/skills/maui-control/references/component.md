# Component

One control to the user, made of fixed parts that the control knows and a test wants to
address by name. Base: `ComponentObjectBase<TScope, Foo<TScope>>`, a container underneath, so
everything in [container.md](container.md) applies too: strict scoping, typed children
(plus `ActivityIndicator(id)`), root overrides.

**The principle: parts own the behaviour; the component forwards.** Every public call is one
part's own single unit of work.

## Shape

```csharp
namespace Bruinet.Maui.CommunityToolkit.Controls.Media;

/// <summary>CommunityToolkit.Maui <c>MediaElement</c>: playback with platform transport controls.</summary>
/// <remarks>...route per platform, why it reads what it reads, limits...</remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class MediaElement<TScope> : ComponentObjectBase<TScope, MediaElement<TScope>>
    where TScope : IMauiScope<TScope>
{
    private const string PlayPauseButtonId = "PlayPauseButton";
    private const string ProgressSliderId = "ProgressSlider";

    public MediaElement(IMauiScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public MediaElement(IMauiScope<TScope> scope, string locatorValue) : base(scope, locatorValue) { }

    #region Parts

    /// <summary>The transport button that toggles playback.</summary>
    public MediaPlayPauseButton<MediaElement<TScope>> PlayPauseButton => new(this, PlayPauseButtonId);

    /// <summary>The transport slider that reports and changes playback progress, 0 to 100.</summary>
    public Slider<MediaElement<TScope>> ProgressSlider => new(this, ProgressSliderId);

    #endregion

    #region Shortcuts

    /// <summary>Whether media is playing, as the play/pause button shows it.</summary>
    protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();

    /// <summary>Pauses playback. No-op when not playing.</summary>
    protected MediaElement<TScope> PauseShortcut(int? timeoutMs = null) => PlayPauseButton.Pause(timeoutMs);

    /// <summary>Playback progress, 0 to 100, from the seek slider.</summary>
    [GenerateComparisons(Comparison.EqualTo | Comparison.GreaterThan | Comparison.AtLeast)]
    protected double? GetProgressShortcut() => ProgressSlider.GetValue();

    #endregion

    #region Hand-written: across parts
    ...
    #endregion
}
```

A page declares it like a control: `public MediaElement<MyPage> Player => new(this, "Player");`.
The two public constructors take `scope`, like a simple control.

## Parts

- Named, typed properties scoped to `this`, created fresh on each access, in a `#region Parts`
  (or a region named for the platform's own term, such as `Transport Controls`).
- Part ids are `private const string`. A part the platform publishes without an id is located
  by a `private static readonly Locator` (`Locator.ByControlType("text")` in AvatarView). That
  locator stays private (R6).
- Use a plain control type where its members are enough (`Button`, `Slider`, `Label`).
- A part that needs more gets its **own control class**:
  - derived from the nearest simple control (`MediaPlayPauseButton : Button`,
    `MediaTimeLabel : Label`, `AvatarInitials : Label`, `AvatarImage : Image`);
  - named `<Component><Part>`, in the component's folder, as its own `.tpl.cs`;
  - element-first Core methods that read and act on **its own element only**
    (`MediaPlayPauseButton.SetPlayingCore` presses, then `Confirm`s its name changes);
  - class remarks naming the component and what the part cannot see ("this control cannot
    see the media's state, so a component that owns this button waits for the media first").
- **Absence is the part's answer.** When "not shown" is a valid state (no image set; no
  initials while an image shows), the part declares an `[AbsenceTolerant]` read:

  ```csharp
  [AbsenceTolerant]
  protected virtual bool? IsShownCore(IMauiElement? element) => element?.Visible == true;
  ```

  Plain `IsVisible` answers false for a missing element, as for a hidden one, and `Exists` may scroll the app and
  find another element on the page. The component then forwards to that read.

## Shortcuts

Rules (a malformed shortcut fails generation; details in
[generator-contract.md](generator-contract.md#shortcuts-components)):

- the name ends in `Shortcut`; `protected`, **not** `virtual`; expression-bodied;
- exactly one `Part.Member(...)` call on a property or field of the class;
- `Is*Shortcut` calls an `Is*` member and emits `IsX / WaitX / AssertX`;
- `Get*Shortcut` calls a `Get*` member and emits `GetX / WaitX / AssertX`; its
  `[GenerateComparisons]` forwards those variants, including `GreaterThan`, `AtLeast`,
  `LessThan`, `AtMost`;
- an action or setter returning the component is made public as is. With a separate button
  per action no part class is needed: `PlayShortcut(int? timeoutMs = null) => PlayButton.Click(timeoutMs)`.

The generated `Assert*` returns the component; it compiles only when the part is scoped to
`this`, so a mis-scoped part is a compile error.

**Target shape: no Core methods on the component**, only parts, shortcuts and hand-written
members (MediaElement, AvatarView). A component Core method never calls a part's public member
(R1): that nests one unit of work in another, with two readiness checks, two polls, timeouts
that add up, and swallowed errors.

## Across two parts

In a `#region Hand-written: across parts`, as plain calls in sequence, with no `Run*` wrapper,
and a `<remarks>` naming the parts and why no single part answers. In order of preference:

1. **Find one part that answers it.** "Has the media opened" is the remaining-time label
   showing a clock: `WaitOpened(timeoutMs) => TimeRemainingLabel.WaitSecondsAtLeast(0, timeoutMs)`.
2. **Hand-write a `Get` only**: `GetDuration() => TimeElapsedLabel.GetSeconds() + TimeRemainingLabel.GetSeconds()`.
   No generated `Wait`/`Assert` around it. If a test needs to wait on it, the value belongs to one
   part.
3. **Hand-write an action that waits on one part, then acts on another**, throwing
   `TimeoutException` naming what did not happen:

   ```csharp
   /// <remarks>
   /// Across two parts: the play/pause button, and the remaining-time readout that says the media
   /// has opened. The button is enabled before the media opens and the player drops a press made
   /// then, so the button alone cannot do this (probed 2026-09-18).
   /// </remarks>
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
   ```

Null-skip and idempotence still hold for hand-written members: `SetPlaying(null)` returns,
`Play()` while playing is a no-op.

## Root resolution

- Default: the parent's `FindElement(Locator)`.
- **The platform does not publish the root**: override `FindContainerRootElement` with a
  documented stand-in, the first part that proves the component is there (as Stepper's buttons
  stand in for Stepper). Throw `ElementNotFoundException` naming every locator tried and the
  app-side requirement (`ShouldShowPlaybackControls`).
- **The platform flattens the parts beside the root** (WinUI's transport controls): override
  `TryFindElement(Locator)`, `FindElement(Locator)` and `FindElements(Locator)` to delegate to
  `Parent`, with remarks giving the evidence and the consequence (one component per scope; put
  each in its own container). This is the only licence to break strict scoping.

## Class remarks record

- the route per platform, and that no bridge is used (or which verb is);
- why the component reads what the user sees when app state disagrees (MediaElement's
  `CurrentState` stays `Opening` while playing, with the probe reference);
- the limits: localized names, buttons the platform does not show, platforms that publish
  nothing yet.

## Examples to read

`src/Bruinet.Maui.CommunityToolkit/Controls/Media/MediaElement.tpl.cs` with `MediaPlayPauseButton.tpl.cs` and
`MediaTimeLabel.tpl.cs`; `src/Bruinet.Maui.CommunityToolkit/Controls/Views/AvatarView.tpl.cs` with
`AvatarInitials.tpl.cs` and `AvatarImage.tpl.cs`.
