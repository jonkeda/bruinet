# Contract: The gesture bridge (`GestureAutomation.Verbs`)

When a MAUI app has an action that plain UI Automation cannot express, the app under
test publishes a **gesture bridge**. This is the contract an app author must implement so
`Bruinet.Maui.FlaUI` can drive it. Decision:
[AD-008](../decisions/ad-008-gestures-and-semantic-actions-go-through-ui-automation.md).

## What the app publishes

- A reference to `Bruinet.Maui.AppSupport` and a call to `UseBruinetGestureBridge()` in
  app startup.
- Per element, in markup, the verbs it answers: `GestureAutomation.Verbs="..."`.
- The provider is compiled in only under `BRUINET_UIA_BRIDGE` (Debug) and active only
  when the launcher sets `BRUINET_UIA_BRIDGE=1`. It is **absent from shipping builds** -
  UI Automation has no per-caller authentication. On Android, which gives an app no
  environment variables, the launcher asks with the system property `debug.bruinet.bridge=1`
  (`adb shell setprop`), read into that variable at start: the same two gates.

## How a verb behaves

- **Declared, never inferred.** An element is automatable because its markup says so.
- **Classified.** Every verb is a *read*, an *element action* or an *app action*. Adding
  a verb without classifying it fails `ContractTests`. The accessibility audit examines
  element actions.
- **Outcomes in values or failure codes, never `S_FALSE`.** Every success HRESULT reaches
  the client as `S_OK`. A refusal that must explain itself returns `S_OK` with the reason
  in the payload; a refusal that needs no explanation returns `BRUINET_E_DECLINED`. A
  failing call carries no payload.
- **Republished per page.** An app republishes its bridge between pages, so a verb the app
  did not answer is retried by the call - it is not a missing route
  ([AD-009](../decisions/ad-009-app-bugs-stay-failures.md)).
- **Joined by `AutomationId`, then by position.** A bridge target carries the element's
  `AutomationId`, and several live elements may share one (a template used twice on a page).
  The bridge keeps them all; re-publishing an element replaces only its own target. When
  several match, the client asks each for the meta verb `GetBounds` (answered for every
  target, physical pixels) and picks the one whose centre is nearest the real element's,
  converted from the client's DPI space. Unique ids are not required of the app.

## Three admission tests before adding a verb

1. **Is it blocked?** If a real UI Automation pattern already answers it (`Invoke`,
   `Toggle`, `Value`, `SelectionItem`, `Scroll`), it stays there.
2. **Is the physical path the thing under test?** A test asserting typing fires
   `TextChanged` must type - on the mobile head, not the bridge.
3. **Does it stay a UI test?** Read what the user can see; if it needs the view model, the
   coverage belongs in `Bruinet.Maui.Tests`.

## Broken when

- A verb duplicates an existing UI Automation pattern (test 1).
- A verb is added without a read/element-action/app-action classification.
- The bridge provider ships in a release build.
- A refusal is signalled with `S_FALSE` instead of a payload or `BRUINET_E_DECLINED`.
- Registering a target replaces another element's target with the same `AutomationId`, or a
  client resolves a shared id without comparing bounds.
