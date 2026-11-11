# Troubleshooting

Start with diagnostics. Do not add sleeps or longer timeouts as the first fix.

## Build Fails

Run:

```powershell
dotnet build Bruinet.sln -v:minimal /nr:false
```

## Test Cannot Find Element

Check:

- automation ID or locator is correct;
- page object waits for the right loaded state;
- control is visible/enabled before action;
- app navigation completed;
- driver page source or automation tree contains the element.

## Flaky UI Test

Before changing code:

1. Inspect screenshots.
2. Inspect runner output.
3. Inspect framework logs and app logs.
4. Check whether the failure is a stale element, wrong page, blocked UI thread,
   missing readiness signal, or real app bug.

Fix by waiting for a real state change, not by sleeping.

## Appium Problems

Check:

- `APPIUM_SERVER_URI`;
- Appium server is running;
- platform driver is installed;
- app path or package is valid;
- emulator/device is connected;
- platform value is `android` or `ios` - MAUI on Windows is driven by FlaUI, not Appium.

## Playwright Problems

Install browsers from the test project's output if needed:

```powershell
pwsh bin\Debug\net10.0\playwright.ps1 install
```

## "Cannot ... Without Real Mouse Or Keyboard Input"

MAUI on Windows has no physical input: no click, no typing, no clipboard, no foreground.
An action with no UI Automation pattern and no bridge verb throws `NotSupportedException`
(`GestureUnavailableException` for a gesture), and the message names what to use instead.

- **Fix the route.** Usually the element needs a verb declared in the app's markup - `Tap`,
  `Focus`, `SetText`, `Submit`, `LongPress`, `Swipe*` - see
  [AD-008](../../.docs/decisions/ad-008-gestures-and-semantic-actions-go-through-ui-automation.md).
  If the message says the bridge answers nothing at all, see
  [Gesture Bridge Problems](#gesture-bridge-problems).
- **Or ask for the operation you mean.** A raw `Click` is not a UI Automation operation;
  controls invoke, toggle or select. `SendKeys` with `TextInputMethod.Keys` types key by key and
  has no Windows route; `SetValue` puts the text in the field.
- **`RightClick` and `Hover` always throw on Windows.** Use `IMauiDriver.InvokeMenuItem` for a
  context-menu item.
- **A test that is about real input** - per-keystroke `TextChanged`, a menu appearing - runs on
  the Android head, where Appium injects input inside the device.

There is no environment variable that turns physical input back on for MAUI.
`BRUINET_BACKGROUND_MODE` and `BRUINET_PHYSICAL_INPUT_LOG` apply to WPF and WinForms only (see
[AD-005](../../.docs/decisions/ad-005-physical-input-is-opt-in.md)).
`BRUINET_ALLOW_POINTER_INPUT` no longer does anything, and has not for some time.

## Gesture Bridge Problems

"No element published on the app's bridge answers ..." has several causes that read
identically from a test:

- **The app was built without the bridge.** It is compiled in only in Debug, or with
  `-p:BruinetUiaBridge=true`. A Release build contains no bridge at all, by design.
- **The app never turned it on.** Every app under test must call
  `builder.UseBruinetGestureBridge()` in `MauiProgram`, before its first page loads.
- **The element never declared the verb**, or has no `AutomationId`.
- **The page had not published yet.** Pages publish on `Loaded`, after they appear in
  the tree; the driver waits through that, so a persistent failure is one of the above.

Set `BRUINET_UIA_LOG` to a file and rerun: the app records whether the bridge is on,
what it published, and every call it answered.

**If every test after some point fails in milliseconds,** check the driver's root
element before the app: UI Automation can retire a cached element for a live window.
The driver re-attaches by itself; if it keeps happening, the `BRUINET_UIA_LOG` file
shows each re-attach, and something is invalidating the root again.
