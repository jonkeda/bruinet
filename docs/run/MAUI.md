# Run MAUI Tests

Working directory: Bruinet root.

## Unit Tests

```powershell
dotnet test tests\Bruinet.Maui.Tests\Bruinet.Maui.Tests.csproj -v:minimal /nr:false
```

## Windows FlaUI Adapter

```powershell
dotnet build src\Bruinet.Maui.FlaUI\Bruinet.Maui.FlaUI.csproj -f net10.0-windows -v:minimal /nr:false
dotnet test tests\Bruinet.Maui.UITests\Bruinet.Maui.UITests.csproj -v:minimal /nr:false
```

The Windows run drives the app without mouse, keyboard or foreground, so you can keep
working while it runs. It needs no environment variables, and the app under test must
host the gesture bridge - see the [MAUI platform guide](../platform-guides/maui.md).

## MauiHeadless Head

```powershell
$env:BRUINET_HEAD='headless'; dotnet test tests\Bruinet.Maui.UITests\Bruinet.Maui.UITests.csproj -v:minimal /nr:false
```

The same suite renders both sample apps headless with MauiHeadless, from each app's own plain `net10.0`
target (`samples\Bruinet.Samples.Maui.App`, `...ShellApp`). It needs no
device, no built app and no window, and the whole suite takes seconds. A test that needs a device
is marked `[DeviceFact("why")]`, or `[WindowsOnlyFact(Device = "why")]`, and a scenario is
tagged `@device`; they are skipped on this head with the reason. A fixture names its headless
app in `CreateHeadlessOptions`. The device heads stay the judge at a gate: no layout or pixels
here. Run Windows before every merge, and Android nightly and for changes to lists, navigation,
gestures or layout: [Which Head When](../guides/maui-headless.md#which-head-when).

On a Mac or Linux the same command works (`export BRUINET_HEAD=headless` there), for this suite and
the Todo sample's `Bruinet.Samples.Todo.Uat` and `.UITests`, after `dotnet workload install maui`
for the MauiHeadless builds' XAML. Off Windows those projects build for plain `net10.0` with the MauiHeadless
and Appium heads only: no FlaUI, and without the Windows driver's own tests (`Tests\Diagnostics`,
`Tests\Background`). `BruinetWindowsHead` in `Directory.Build.props` chooses this, and
`-p:BruinetWindowsHead=false` builds that shape on Windows. Unset, the head is the Windows app,
which does not exist there.

`./tools/Scripts/test-maui-headless.ps1` runs all three suites this way, on any of them; CI runs it
on `windows-latest` after the unit tests and in a job of its own on `macos-latest`.

## Appium Setup

Appium drives MAUI on Android and iOS only; Windows always uses FlaUI. Use
[MAUI Android](maui-android.md) for Android setup.
