# Run The Native iOS UI Tests

`tests/Bruinet.NativeIos.UITests` drives the gallery app (`samples/Bruinet.Samples.NativeIos.App`)
on an iOS simulator through Appium. It holds the UI tests and the platform's UAT suite. It runs on
a Mac only, and CI does not run it: it needs a simulator.

## Once

- Xcode, with an iOS simulator runtime, and the .NET `ios` workload (`dotnet workload install ios`).
- Appium 2 or later and its XCUITest driver: `npm i -g appium`, then
  `appium driver install xcuitest`. The first session builds WebDriverAgent, which takes a few
  minutes.

## Each time

From the Bruinet root:

```bash
# A simulator. The fixture uses the booted one.
xcrun simctl boot "iPhone 17"; open -a Simulator

# Appium, in a second terminal.
appium --address 127.0.0.1 --port 4723

# The gallery app. Rebuild it whenever anything under samples/ changed: the fixture installs it.
dotnet build samples/Bruinet.Samples.NativeIos.App -v:minimal

# The tests: one class, the UAT suite, or everything.
dotnet test tests/Bruinet.NativeIos.UITests --filter "FullyQualifiedName~ControlsTests"
dotnet test tests/Bruinet.NativeIos.UITests --filter "Category=UAT"
dotnet test tests/Bruinet.NativeIos.UITests
```

The fixture opens one Appium session for the whole run. It uninstalls the gallery app first, so
each run starts with its permissions cleared, and installs the `.app` it finds under the sample's
`bin/Debug/net10.0-ios/iossimulator-arm64`; `APPIUM_APP_PATH` overrides it, and the other
`APPIUM_*` variables ([the platform guide](../platform-guides/native-ios.md)) override the
simulator and the server. One UI test process at a time, and no build while one runs.

The suite without UAT is 28 tests and takes about six minutes on an iPhone 17 simulator; the UAT
suite is 2 tests and takes a little over a minute.

## Recording

```bash
BRUINET_RECORD=1 BRUINET_RECORD_DIR=TestResults/recordings \
  dotnet test tests/Bruinet.NativeIos.UITests --filter "Category=UAT"
```

Each scenario is recorded with `xcrun simctl io recordVideo`, with its taps and typing in the
timeline.

## Testing an app of your own

- **An app that uses an app group or keychain sharing crashes at launch** when built unsigned:
  the simulator reads the entitlements from the binary, and an unsigned build has none. Build it
  ad-hoc signed, for example `xcodebuild ... CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual
  DEVELOPMENT_TEAM=`. Re-signing the built `.app` with `codesign` does not help.
- **Give each screen a deep link**, so a test opens it directly instead of tapping through.

## When a run fails

- **"The app under test is no longer available"**: the Appium session ended. Is Appium still running?
- **The first session times out**: WebDriverAgent is still building; run again once it is built.
- **"not ready ... is not shown"**: an alert iOS draws over the app covers the screen, often
  "Save Password?". Dismiss it in a step before the check.
- **A step on a long table is slow**: each request takes about half a second; a row far down
  takes several swipes. That is the normal cost, not a hang.
