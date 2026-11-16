# Native iOS Platform Guide

`Bruinet.NativeIos` tests iOS apps that are not MAUI apps (UIKit), through Appium and XCUITest. It
mirrors `Bruinet.NativeAndroid`: the same page objects, generated ControlObjects with the same
members and UAT verbs, one call model (act once, confirm, wait for state), scope readiness and
collections with item keys. Where iOS differs, this guide says so under "What is iOS's own".

## Projects

- `src/Bruinet.NativeIos`: interfaces, the Appium adapter, pages, containers, collections,
  controls.
- `src/Bruinet.Appium`: what the Appium stacks share (Appium's error mapping, `IosHost` for
  `simctl`, `IosRecorder`).
- `tests/Bruinet.NativeIos.Tests`: unit tests over a mocked app (CI runs them).
- `tests/Bruinet.NativeIos.UITests`: UI tests and the UAT suite against the gallery app.
- `samples/Bruinet.Samples.NativeIos.App`: the gallery app (.NET for iOS, UIKit).

## A session

<!-- sample: tests/Bruinet.Docs.Samples/NativeIos/NativeIosGuide.cs#session -->
```csharp
var context = NativeIosTestContext.Create(NativeIosTestContextOptions.FromEnvironment());
context.NavigateTo("myapp://orders");   // a deep link
var page = new OrdersPage(context);
page.AssertIdle();
```

`NativeIosTestContextOptions.FromEnvironment()` reads:

| Variable | Purpose | Default |
| --- | --- | --- |
| `APPIUM_SERVER_URI` | Appium server URL | `http://127.0.0.1:4723` |
| `APPIUM_APP_PATH` | `.app` (simulator) or `.ipa` to install | none |
| `APPIUM_BUNDLE_ID` | the app's bundle id | none |
| `APPIUM_UDID` | simulator or device id | none |
| `APPIUM_PLATFORM_VERSION` | iOS version | none |
| `APPIUM_DEVICE_NAME` | simulator or device name | `iPhone 17` |
| `APPIUM_AUTOMATION_NAME` | Appium automation | `XCUITest` |
| `APPIUM_AUTO_ACCEPT_ALERTS` | accept system alerts on sight | `false` |
| `APPIUM_NO_RESET`, `APPIUM_FULL_RESET` | Appium's reset flags | `false` |

The session's implicit wait is zero: a lookup makes one attempt and the call around it waits.

## Page objects

<!-- sample: tests/Bruinet.Docs.Samples/NativeIos/NativeIosGuide.cs#page -->
```csharp
[TestPage("Sign In")]
public sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("login");

    public TextField<LoginPage> UserName => new(this, "username");
    public Button<LoginPage> SignIn => new(this, "login");
    public Label<LoginPage> Status => new(this, "status");
}
```

A string locator is an accessibility identifier; one starting with `/` is an XPath. Locators are
sent as XCUITest predicates (`name == 'login'`); `NativeIosLocator` adds label, type and text
forms. Prefer identifiers, then predicates; avoid XPath, which makes XCUITest serialize the whole
tree.

**Readiness** is asked on every attempt of every call, outermost first: the app in front (another
app in front is a not-ready reason that names itself), the page's ready element, then the busy
signal. **The busy signal:** an app shows a view with accessibility identifier `bruinet_busy` while
it works; while it is shown the page is busy and calls wait. A page can name more busy views
(`BusyIndicators`). A ready element that is in the tree but not shown is usually covered by an
alert iOS draws over the app; the failure names it.

## Controls

Named after UIKit's views, generated from `.tpl.cs` (`tools/Scripts/CreateNativeIos.sh`):

| Family | Controls |
| --- | --- |
| Buttons | `Button` |
| Text | `Label`, `TextField`, `SecureTextField`, `TextView`, `SearchField` |
| Toggle | `Switch` |
| Range | `Slider`, `Stepper`, `ProgressView` |
| Selection | `SegmentedControl`, `PickerWheel`, `TabBar` |
| Containers | `ScrollView`, `NavigationBar`, `Toolbar` |
| Dialogs | `Alert`, `ActionSheet`, `PermissionAlert` |
| Collections | `CollectionView`, `TableView` (rows as `ListItem`, or your own row type) |
| Display | `Image` |

A date picker is not there yet.

## What is iOS's own

- **System prompts live in Springboard.** A privacy prompt (camera, photos, location) is not in
  the app's tree; `PermissionAlert` finds it in Springboard's. The AutoFill "Save Password?" sheet
  is in neither tree and is read only through Appium's alert text (`SystemAlertText`).
- **Alerts and action sheets** are found by their buttons, matched on the button type only.
- **A search under an element also matches the element itself** in XCUITest; Bruinet drops it,
  except for an XPath.
- **Tables keep all their rows in the tree**, and a view inside an off-screen cell reports the
  position of the first row. `Item("Invoice 42")` scrolls the row onto the screen before using it.
  A collection view keeps only the cells on the screen and scrolls to reach the others.
- **Scrolling is by swipes**, the fastest whose distance fits what is left, then a drag; while it
  swipes, Appium's idle wait is off (with it, one swipe took 21 seconds). Appium's
  `mobile: scroll toVisible` is not used: it took minutes on long tables.
- **Gestures:** `Hover` has no route and throws `RouteUnavailableException`. `RightClick` is a long
  press, which opens a context menu on iOS.
- **Values:** a slider is set within about 5% of the value asked; a switch reads as checked when
  its value is `1`; an empty text field never reads as its placeholder.
- **Speed:** each XCUITest request takes about half a second on the simulator, so a row lookup in
  a table takes 1.2 to 1.5 seconds, and an action on a row a few seconds.

## Evidence and recording

`NativeIosEvidenceCapture` saves the screen and the element tree. The context is `IRecordable`:
`BRUINET_RECORD` records scenarios with `xcrun simctl io recordVideo` (simulator only), and every
tap, long press, swipe and typed text is a touch mark on the video (AD-024).

## Tests

Unit tests (no device), from the Bruinet root:

```bash
dotnet test tests/Bruinet.NativeIos.Tests/Bruinet.NativeIos.Tests.csproj -v:minimal
```

UI tests and UAT need a Mac, a simulator and Appium: [run/native-ios.md](../run/native-ios.md).
