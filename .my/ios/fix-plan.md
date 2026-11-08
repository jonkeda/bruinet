# iOS: fixing the failures from the first simulator run

The first run of the mobile suite on iOS (iPhone 17 Pro Max, iOS 26.0, 2026-10-21) had 209 passed,
68 failed and 20 skipped, with `AlertReadTests` left out. That full run took 45 minutes. This plan
fixes the failures one at a time and runs only the tests that can prove each fix. Set up as
[README.md](README.md) and [docs/run/maui-ios.md](../../docs/run/maui-ios.md) describe.

## How to run while fixing

Do not run the full suite until the last step. For each fix, run its **probe** (one test) first.
When the probe passes, run its **check** (the class or classes the fix touches). One test process at
a time.

Paste this once per shell. It puts `dotnet`, `node` and `appium` on `PATH`, which the default
macOS shell here lacks:

```bash
# Working directory: Brinell root
export PATH=~/.dotnet:~/.local/node/bin:$PATH DOTNET_ROOT=~/.dotnet
export APPIUM_PLATFORM=ios APPIUM_DEVICE_NAME="iPhone 17 Pro Max" APPIUM_PLATFORM_VERSION=26.0
export APPIUM_UDID=$(xcrun simctl list devices booted | grep "iPhone 17 Pro Max" | grep -oE '[0-9A-F-]{36}')
ui() { dotnet test testsnew/Brinell.Maui.UITests.Mobile --filter "$1" 2>&1 | grep -E "^(Passed|Failed)!|^\s+Failed |Error Message" -A1; }
# Appium, in its own terminal: appium --address 127.0.0.1 --port 4723 --relaxed-security
```

- `ui "<filter>"` rebuilds the test project and `srcnew`, which is what most fixes change.
- Rebuild the sample app only when a step changes `samples/` (steps 7 and 8). The app build
  command is in [maui-ios.md](../../docs/run/maui-ios.md).
- Add `--no-build` when you only rerun without changing code.
- When a probe fails for a reason you do not understand, dump the tree before changing code:
  `curl -s http://127.0.0.1:4723/session/<id>/source > tree.xml` (the session id is in
  Appium's log).

## Order

Step 1 comes first, because a stuck alert made 130 later tests fail in the first full run. After
that, steps are ordered by how many tests each one fixes for the least work.

| Step | Fixes | Tests |
| --- | --- | --- |
| 1. Alert recovery and alert buttons | AlertReadTests, and the cascade | 3 |
| 2. Swipe | Gestures, ProductCollection, IndicatorView | about 12 |
| 3. Picker wheel at its end | PickerTests | 4 |
| 4. Shell flyout locator | Shell tab, stack and flyout | 13 |
| 5. RatingView stars | RatingViewTests | 5 |
| 6. Slider value | SliderTests | 6 |
| 7. Stepper value | StepperTests | 10 |
| 8. Grid count label | GridContainerTests | 1 |
| 9. Full run, then record what was measured | everything | all |

MediaElement, DrawingView and CarouselView (except swipes) are out of scope, as
[README.md](README.md) says: they fail on Android too.

## 1. Alert recovery and alert buttons

**Symptom.** `Dialog_PublishesItsTitleAndButtons` fails on `ButtonTexts`, and the alert stays on
screen. Every later test then fails with "Could not get back to the hub".

**Cause.** There are two. The fixture's return to the hub does not dismiss an iOS alert. Separately,
`ContentDialog.GetButtonTextsCore` reads buttons in tree order. On iOS the cancel button comes first
([README.md](README.md) table), but MAUI's order is accept first, then cancel.

**Change.**
- Make the hub recovery dismiss any `XCUIElementTypeAlert` (tap its cancel button) before it
  navigates. Look in `MauiFixture.ReturnToHub` and the Appium navigation-root code in
  `AppiumMauiDriver`.
- In `ContentDialog.GetButtonTextsCore`, on iOS, put the first (cancel) button last when there are
  two buttons. Check the real order in a tree dump first.

**Probe.** `ui "FullyQualifiedName~AlertReadTests.Dialog_PublishesItsTitleAndButtons"`

**Check.** `ui "FullyQualifiedName~AlertReadTests|FullyQualifiedName~.ButtonTests"`. The button
tests run after the alerts and show whether the app got back to the hub.

## 2. Swipe

**Symptom.** "The following required parameter is missing: [direction]".

**Cause.** `AppiumMauiElement.Swipe` calls XCUITest's `mobile: swipe` with start and end points.
That command takes a `direction` and an element, not points.

**Change.** On iOS, use `mobile: dragFromToForDuration` with `fromX`, `fromY`, `toX`, `toY` and
`duration` in seconds. `AppiumMauiDriver` already calls it that way for drags. A swipe needs a short
duration (about 0.1 s) or iOS reads it as a drag. A refresh pull may need a longer one.

**Probe.** `ui "FullyQualifiedName~GestureVerbTests.Swipe_MatchesOnTheDirectionFlag"`

**Check.** `ui "FullyQualifiedName~GestureVerbTests|FullyQualifiedName~GestureBridgeTests|FullyQualifiedName~RecordedInteractionTests|FullyQualifiedName~ProductCollectionTests|FullyQualifiedName~.IndicatorViewTests"`

## 3. Picker wheel at its end

**Symptom.** "Picker wheel value has not been changed after 2 seconds timeout".

**Cause.** `AppiumMauiElement.Wheels.cs` `TurnToIndex` steps `previous` until the wheel stops
moving, and `StepWheel` expects a `false` at the end. WebDriverAgent instead throws
`InvalidElementStateException` when the wheel is already at its end. The date and time pickers
pass because they set their wheels by value, not by stepping.

**Change.** In `StepWheel`, treat that one WebDriverAgent error as "already at the end" and return
`false`. Match on its message, so that other errors still throw.

**Probe.** `ui "FullyQualifiedName~PickerTests.Picker_SelectByIndex_UpdatesStatus"`

**Check.** `ui "FullyQualifiedName~.PickerTests|FullyQualifiedName~.DatePickerTests|FullyQualifiedName~.TimePickerTests"`.
The date and time tests guard the shared wheel code.

## 4. Shell flyout locator

**Symptom.** Every Shell test fails in 1 ms. The `AppShell` scope never becomes ready on
`//XCUIElementTypeTable[XCUIElementTypeCell]`.

**Cause (to confirm).** `ShellFixture.ReturnToShellRoot` closes the flyout first, and the flyout
host locator in `AppiumMauiDriver.IosShellChrome` does not match what MAUI draws on iOS 26. It may be
a collection view, not a table. The opener names in `AppiumMauiElement.IosFlyoutOpener` are also
unmeasured.

**Change.** Dump the tree twice in the Shell app: once at rest and once with the flyout open. Set
`FlyoutHost`, `FlyoutItem`, `Tab` and the opener from what you see. Check that a closed flyout is
absent from the tree, because `IsFlyoutOpen` relies on that.

**Probe.** `ui "FullyQualifiedName~ShellTabTests.Shell_ReportsItsTabs"`. This test needs the tabs
only, so it proves the fixture first.

**Check.** `ui "FullyQualifiedName~ShellTabTests|FullyQualifiedName~ShellFlyoutTests|FullyQualifiedName~ShellStackTests"`

## 5. RatingView stars

**Symptom.** "SelectIndex: 'TestRatingView' was tapped, but no picker wheel showed".

**Cause.** `RatingView.TapStarCore` taps the stars only on Android. Every other platform goes through
`SelectIndex`, which on Appium is the picker route. The README already warns that the `./*/*/*` star
path was measured on UiAutomator2 only.

**Change.** Use `TapStarOnScreen` on iOS too, with an iOS star path taken from a tree dump. Check
whether iOS publishes the rating (`GetValueCore`); if not, keep the Android branch's
"nothing to confirm" behavior for iOS as well. Then see whether `ReadsInitialRangeFromApp` and
`SetValue_OutsideStars_Throws` pass on their own, before changing anything for them.

**Probe.** `ui "FullyQualifiedName~RatingViewTests.RatingView_TapStar_SetsRatingToThatStar"`

**Check.** `ui "FullyQualifiedName~RatingViewTests"`

## 6. Slider value

**Symptom.** "AppiumMauiElement.SetRangeValue has no route for iOS yet".

**Cause.** `SetRangeValue` is written for Android only.

**Change.** On iOS, `XCUIElementTypeSlider` takes a position from 0 to 1 through the element's
value endpoint (`SendKeys` or `mobile: setValue`). Turn the app value into that fraction with the
slider's minimum and maximum. Read the value back as the `value` attribute, which iOS reports as a
percentage such as `50%`. Rounding can miss by one step, so check what the tests' tolerance allows.

**Probe.** `ui "FullyQualifiedName~SliderTests.Slider_SetValue_UpdatesDisplay"`

**Check.** `ui "FullyQualifiedName~SliderTests"`

## 7. Stepper value

**Symptom.** "Stepper 'X' does not expose its current value".

**Cause.** A `UIStepper` publishes only its + and − buttons, no value. On Android,
`Brinell.Maui.AppSupport` publishes the value as the node's range. On Windows, a `GetState` verb
does.

**Change.** Add an iOS handler in `Brinell.Maui.AppSupport` that sets the stepper's
`AccessibilityValue` (and its range, if `SetValueCore` needs the bounds). Read it in the Appium
element's range read on iOS. Before you write the handler, read AD-008's admission rules in
`.docs/`. Then rebuild the sample app.

**Probe.** `ui "FullyQualifiedName~StepperTests.Stepper_Increment_ChangesValueByStepSize"`

**Check.** `ui "FullyQualifiedName~StepperTests|FullyQualifiedName~SliderTests"`. The slider tests
guard the shared range read.

## 8. Grid count label

**Symptom.** "The element 'ProductCountLabel' was found but is not visible", after the product was
added.

**Cause (to confirm).** The software keyboard probably still covers the label after the entry, or
the label scrolled off screen. Take a screenshot before guessing.

**Change.** Decide from the screenshot. Hide the keyboard after text entry, or scroll the label into
view. Use an existing Brinell control route, not a sleep.

**Probe and check.** `ui "FullyQualifiedName~GridContainerTests"`

## 9. Full run and recording

Run the suite once without a filter, in the background, and check it after the first few minutes
for a cascade ("Could not get back to the hub"). Expect only the out-of-scope MediaElement,
DrawingView and CarouselView failures. For any other failure, fix it as one more step above and
run only that step's tests.

Then do what [README.md](README.md) asks. Remove the "not yet run on a simulator" remarks from code
that now passes. Write the measured names, values and timings into
[maui-ios.md](../../docs/run/maui-ios.md). Delete this topic and its row in
[.my/README.md](../README.md).
