# Native Android Platform Guide

`Bruinet.NativeAndroid` tests Android apps that are not MAUI apps (Views, AndroidX, Material),
through Appium and UiAutomator2. It has the shape of the MAUI and Html stacks: page objects,
generated ControlObjects with the same members and UAT verbs, one call model (act once, confirm,
wait for state), scope readiness and collections with item keys.

## Projects

- `src/Bruinet.NativeAndroid`: interfaces, the Appium adapter, pages, containers, collections,
  controls.
- `src/Bruinet.Appium`: what the Appium stacks share (Appium's error mapping, `AndroidHost`,
  `AndroidRecorder`).
- `tests/Bruinet.NativeAndroid.Tests`: unit tests over a mocked app (CI runs them).
- `tests/Bruinet.NativeAndroid.UITests`: UI tests and the UAT suite against the gallery app.
- `samples/Bruinet.Samples.NativeAndroid.App`: the gallery app (.NET for Android).

## A session

<!-- sample: tests/Bruinet.Docs.Samples/NativeAndroid/NativeAndroidGuide.cs#session -->
```csharp
var context = NativeAndroidTestContext.Create(NativeAndroidTestContextOptions.FromEnvironment());
context.NavigateTo("myapp://orders");   // a deep link
var page = new OrdersPage(context);
page.AssertIdle();
```

`NativeAndroidTestContextOptions.FromEnvironment()` reads:

| Variable | Purpose | Default |
| --- | --- | --- |
| `APPIUM_SERVER_URI` | Appium server URL | `http://127.0.0.1:4723` |
| `APPIUM_APP_PATH` | APK to install | none |
| `APPIUM_APP_PACKAGE` | the app's package | none |
| `APPIUM_APP_ACTIVITY` | launch activity | none |
| `APPIUM_PLATFORM_VERSION` | Android version | none |
| `APPIUM_DEVICE_NAME` | device or emulator | `emulator-5554` |
| `APPIUM_AUTOMATION_NAME` | Appium automation | `UiAutomator2` |
| `APPIUM_AUTO_GRANT_PERMISSIONS` | grant runtime permissions on install | `true` |
| `APPIUM_NO_RESET`, `APPIUM_FULL_RESET` | Appium's reset flags | `false` |

The session's implicit wait is zero: a lookup makes one attempt and the call around it waits. It
shows every window (`enableMultiWindows`), so popups that do not take focus are found.

## Page objects

<!-- sample: tests/Bruinet.Docs.Samples/NativeAndroid/NativeAndroidGuide.cs#page -->
```csharp
[TestPage("Sign In")]
public sealed class LoginPage(INativeAndroidTestContext context) : PageObjectBase<LoginPage>(context)
{
    public override string? Activity => ".LoginActivity";
    protected override Locator? ReadyLocator => Locator.ById("login");

    public EditText<LoginPage> UserName => new(this, "username");
    public Button<LoginPage> SignIn => new(this, "login");
    public TextView<LoginPage> Status => new(this, "status");
}
```

A string locator is a resource id (the package prefix is added); one starting with `/` is an XPath.
Prefer ids, then `Locator.ByAccessibilityId`, then text: every XPath makes UiAutomator2 serialize
the whole screen.

**Readiness** is asked on every attempt of every call, outermost first: the app in front (a system
dialog or another app is a not-ready reason that names itself), the page's activity and ready view,
then the busy signal. **The busy signal:** an app shows a view with resource id `bruinet_busy` while
it works; while it is shown the page is busy and calls wait. A page can name more busy views
(`BusyIndicators`). This costs about half a second per attempt on the emulator, in Appium round
trips.

## Controls

Named after Android's widgets, generated from `.tpl.cs` (`tools\Scripts\CreateNativeAndroid.Bat`):

| Family | Controls |
| --- | --- |
| Buttons | `Button`, `ImageButton`, `FloatingActionButton` |
| Text | `TextView`, `EditText`, `AutoCompleteTextView` |
| Toggle | `CheckBox`, `Switch`, `RadioButton`, `ToggleButton`, `Chip` |
| Range | `SeekBar`, `RatingBar`, `ProgressBar` |
| Selection | `Spinner`, `RadioGroup`, `TabLayout`, `BottomNavigationView` |
| Containers | `ScrollView`, `SwipeRefreshLayout`, `Toolbar` |
| Dialogs | `AlertDialog`, `PermissionDialog`, `Snackbar` |
| Collections | `RecyclerView`, `ListView` (rows as `ListItem`, or your own row type) |
| Display | `ImageView` |

What is Android's own:

- **A control below the fold** is not in UiAutomator2's tree; a control on a page scrolls it in
  (`UiScrollable`) before the call reports it missing.
- **Rows are keyed** by a view's text (`itemKeyId`), else the row's description or first text, else
  their position. `Item("Invoice 42")` scrolls the list to the item; a row follows its item when
  the list recycles its views.
- **Gestures are named and element-relative** (`Swipe`, `Scroll`, `LongPress`, `TapAt` as fractions
  of the view). `Hover` has no route and throws `RouteUnavailableException`. `RightClick` is a long
  press. A `RatingBar` is set by a tap, since Android's set-progress action does not tell the app.
- **Text** never reads as a field's hint; a password field is confirmed by length.

## Evidence and recording

`NativeAndroidEvidenceCapture` saves the screen and the view hierarchy. The context is
`IRecordable`: `BRUINET_RECORD` records scenarios with `adb screenrecord`, and every tap, long
press, swipe and typed text is a touch mark on the video (AD-024).

## Tests

Unit tests (no device), from the Bruinet root:

```powershell
dotnet test tests\Bruinet.NativeAndroid.Tests\Bruinet.NativeAndroid.Tests.csproj -v:minimal /nr:false
```

UI tests and UAT need the emulator and Appium: [run/native-android.md](../run/native-android.md).
