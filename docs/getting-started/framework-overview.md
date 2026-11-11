# Framework Overview

Bruinet gives tests a consistent page-object and control-object model across
desktop, web, MAUI, Stride, native Android, UAT markdown scenarios, and the
Presenter shell.

## Layers

```text
Tests and UAT scenarios
  Page objects and fixture/runtime glue
    Bruinet controls and platform contexts
      Bruinet.Core interfaces, locators, waits, artifacts
        Platform drivers: FlaUI, Playwright, Appium, Stride, Android, MauiHeadless, bUnit
```

## Project Families

| Family | Purpose |
| --- | --- |
| `Bruinet.Core` | Shared interfaces, locators, artifacts, wait helpers, assertions |
| `Bruinet.Maui` | MAUI page objects and controls |
| `Bruinet.Maui.Appium` | Appium driver adapter for MAUI/mobile |
| `Bruinet.Maui.FlaUI` | Windows/FlaUI driver adapter for MAUI desktop |
| `Bruinet.Wpf` | WPF automation through FlaUI |
| `Bruinet.WinForms` | WinForms automation through FlaUI |
| `Bruinet.Html` | HTML/web abstractions, control objects, pages, containers and collections |
| `Bruinet.Html.Playwright` | Playwright-backed web driver |
| `Bruinet.Blazor` | Blazor-focused web testing helpers |
| `Bruinet.Stride` and `Bruinet.Stride.Automation` | Stride UI automation and in-game hooks |
| `Bruinet.Mocking` | WireMock and mock sensor helpers |
| `Bruinet.NativeAndroid` | Native Android automation helpers |
| `Bruinet.Uat` | Markdown-driven UAT scenario runtime |
| `Bruinet.Presenter` | Desktop presenter for UAT workspaces |

## Design Principles

- Tests express user intent.
- Page objects own screen structure.
- Controls own repeated interaction behavior.
- Platform drivers stay behind Bruinet abstractions.
- Synchronization waits for observable state.
- Test artifacts use the shared `TestResults/<run-id>/...` layout.
- Test settings load from `TestSettings/*.json` with default, local, scenario,
  and explicit layers.
- Public APIs prefer semantic operations over pointer or coordinate actions.

## Where To Go Next

- [Codebase Structure](../architecture/structure.md)
- [Control Object Index](../controls/index.md)
- [Testing](../architecture/testing.md)
- [Reporting And Artifacts](../guides/reporting-artifacts.md)
- [Test Settings](../guides/settings.md)
