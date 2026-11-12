# AD-037: A MAUI Runner Is Chosen Apart From The Platform, And DevFlow Judges No Gate

On a device the platform no longer picks the driver by itself: a run chooses its runner
(`MauiDriverOptions.Runner`, `BRUINET_MAUI_RUNNER`). The default is the platform's own, FlaUI on Windows and Appium
on Android and iOS. `devflow` drives the app through the MauiDevFlow agent the app carries, on any platform. Where
the agent has no route, the DevFlow runner sends the verbs the app already declares for the Windows bridge
(`uia:GestureAutomation.Verbs`, [bridge](../contracts/bridge.md)) through an agent extension, behind the bridge's
own gates. What the agent's tree does not show (a Stepper's buttons, a MediaElement's transport, WinUI's back
button) stands in under the id the platform runner shows. The FlaUI and Appium runners judge every gate.

**Why:** one runner on every platform, with no Appium server and no UI Automation quirks, is the fast device
runner for an agent's inner loop and the only one for Mac Catalyst. But its input is semantic: a tap invokes, typing
sets the text. It cannot show that a touch or a key reaches the control, so it cannot stand in for the runners that
do. Reusing the bridge's verbs keeps one declaration in the app and one security gate. Standing in under the
platform's ids keeps page objects and controls the same on every runner (AD-002, AD-014).

**Ruled out:**
- a gate passed on the DevFlow runner in place of FlaUI or Appium;
- a page object, control or feature file that asks which runner it is on;
- a second app-side protocol for DevFlow beside the bridge's verbs;
- the bridge extension in a build without `BRUINET_UIA_BRIDGE`, or answering while `BRUINET_UIA_BRIDGE=1` is unset;
- the preview DevFlow packages in `Bruinet.Maui`, or in an app build made without `-p:BruinetDevFlow=true`.

**Broken when:** a gate job sets `BRUINET_MAUI_RUNNER=devflow`; code under `src/Bruinet.Maui*/Controls` or a page
object reads `MauiRunner`; `Bruinet.Maui` references `Microsoft.Maui.DevFlow.*`; or a sample app built without
`BruinetDevFlow=true` contains the agent.

The design, its measurements and the gaps that remain:
[.my/devflow-runner/design.md](https://github.com/exact-mobile-apps/test-tooling/blob/4999322a/.my/devflow-runner/design.md).
