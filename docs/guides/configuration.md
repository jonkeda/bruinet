# Configuring a test context

Each platform has one options type a test project sets: `HtmlTestContextOptions` (and bUnit's
`BunitTestContextOptions`), `MauiTestContextOptions`, `WpfTestContextOptions`,
`WinFormsTestContextOptions`, `StrideTestContextOptions` and `NativeAndroidTestContextOptions`.
All derive from `TestContextOptionsBase`, which holds what every platform shares.

## What every platform shares

| Setting | Default |
| --- | --- |
| `Timeouts` (`DefaultWait`, `PageLoad`, `Animation` (the scroll throttle), `PollingInterval`) | `TimeoutSettings.Default`; Stride's own longer budgets |
| `Name`, `Logger`, `NearMiss` | the platform's name, no log (or `BRUINET_CALL_LOG`'s), the shared near-miss rule |
| `Artifacts` (`RootDirectory`, `RunId`, `Suite`) | the run's results folder |

## Where a value comes from

1. The defaults, on the options type.
2. The settings file beside the test assembly, `bruinet.{platform}.config.json` (`html`, `maui`, `wpf`,
   `winforms`, `stride`), which holds the options type's own names. Times are milliseconds or a
   `TimeSpan` string; names are not case-sensitive.
3. For MAUI, the `APPIUM_*` variables (`APPIUM_PLATFORM`, `APPIUM_SERVER_URI`, `APPIUM_APP_PATH`,
   `APPIUM_DEVICE_NAME`, `APPIUM_PLATFORM_VERSION`, `APPIUM_UDID`).
4. The fixture's code: `SetupWith(options => …)`, or an override of `CreateTestContextOptions(loaded)`
   (`CreateOptions(loaded)` on Html and Stride), which gets the loaded options and returns them.

Each step wins over the one before. The run switches apply on top of all of them: `BRUINET_HEAD`
picks the head, `BRUINET_TIMEOUT_SCALE` scales the budgets, `BRUINET_CALL_LOG` writes the call log.

## A settings file

```json
{
  "baseUrl": "https://localhost:5001",
  "headless": true,
  "timeouts": { "defaultWait": 5000, "pageLoad": 15000 },
  "artifacts": { "suite": "Web" }
}
```

A MAUI file nests the driver's own settings: `{ "driverOptions": { "platform": "android", "deviceName": "emulator-5554" } }`.
A WPF or WinForms file names the app to start (`executablePath`) or one to attach to (`processName`,
`processId`, `windowHandle` as a number or `"0x1A2B"`).

## A project that builds its options in code

Nothing above is required: a fixture may make its options with `new HtmlTestContextOptions { … }` and
pass them to the context, as Tomorrow's suites do. The file is read only by the fixture bases.
