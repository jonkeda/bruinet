# Forbidden in native iOS tests

In test methods, page objects' public members and test helpers, as on MAUI
([maui-ui-test forbidden-apis.md](../../maui-ui-test/references/forbidden-apis.md), which gives the
reasons), Html and native Android:

| Forbidden | Use instead |
| --- | --- |
| `Context.Driver` and any `INativeIosDriver` member | a page-object member, or `GalleryFixture.Open(screen)` for a deep link |
| `INativeIosElement` variables, parameters, returns or members | a ControlObject |
| `FindElement`, `FindElements`, `TryFindElement`, `FindSystemElements`, `TryGetItemRoot` | a control, container or collection member |
| `new Locator(...)` / `Locator.By...` / `NativeIosLocator.By...` in a test method | a control declared on the page object |
| `IOSDriver`, `AppiumElement`, `By`, `MobileBy` and other Appium or Selenium types | the stack's own types |
| `xcrun simctl`, `IosHost` or `mobile:` commands from a test | a page-object or fixture member |
| `Thread.Sleep`, `Task.Delay`, a hand-written wait loop | an `Assert*` or `Wait*` member, or the app's busy view |

The one exception is the stack's own adapter tests (`AppiumErrorMappingTests`, the locator
tests), which test the adapter itself, and the fixture, which uses `IosHost` to set up the
simulator.
