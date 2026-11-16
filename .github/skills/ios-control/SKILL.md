---
name: ios-control
description: Create or extend a Bruinet native iOS ControlObject - a simple control, a container, a component with named parts, or a collection with rows - for a UIKit view (a control, an alert, a UITableView or UICollectionView) in src/Bruinet.NativeIos. Use when asked to add, write or extend a native iOS control object, container, collection or row, or when a native iOS UI test needs a control member that does not exist yet.
---

# Write a native iOS ControlObject

Native iOS controls are generated, as Android's are: a template `Foo.tpl.cs` holds the
constructors, `*Core` methods and hand-written members, and `bruinet controls generate` writes the
public API into `Foo.gen.cs`. The rules are the call model's (`.docs/contracts/call-model.md`,
`.docs/contracts/control-object.md`) and the generator contract
([generator-contract.md](../maui-control/references/generator-contract.md)), with
`INativeIosElement` as the element type. Native iOS mirrors native Android (AD-042): a member
Android has keeps its name and meaning here. Controls are named after UIKit's views (`TextField`,
`SegmentedControl`, `TableView`), never MAUI's or Android's.

## Choose the kind

| Kind | Base | Example |
| --- | --- | --- |
| Simple control | `ViewBase<TScope>` or a family base (`ClickableControlBase`, `FocusableControlBase`, `ToggleControlBase`, `RangeControlBase`, `SelectorControlBase`, `ScrollableControlBase`) | `TextField<TScope>`, `Slider<TScope>` |
| Options among its children | `ChoiceGroupBase<TScope>`: say which children are options and how one shows it is chosen | `SegmentedControl`, `TabBar` |
| Container or component | `ContainerObjectBase<TParent, TSelf>`; parts are controls declared on it | `NavigationBar<TParent>`, `Alert<TParent>` |
| Collection with rows | `CollectionObjectBase<TParent, TSelf, TItem>` plus `ItemObjectBase<TSelf>`; `TableView<TParent>` or `CollectionView<TParent>` with `ListItem` when rows need no type of their own | `InvoicesPage.Invoices` in the UI tests |

## Probe the view

With a simulator booted and Appium running, open a session and read the tree:
`GET /session/{id}/source` (or Appium Inspector). Read the `type` (`XCUIElementTypeButton`),
`name` (the accessibility identifier), `label`, `value`, `enabled`, `visible` and the rect. Then
check what XCUITest does, which the tree does not show:

- **A table keeps every row in the tree**, but a view inside an off-screen cell reports the first
  row's position. A row is scrolled in before it is used (`CollectionObjectBase.Item`).
- **A collection view keeps only the cells on the screen**; reaching the others takes scrolling.
- **A search under an element matches the element itself**; the adapter drops it, except for an
  XPath.
- **System prompts are not in the app's tree.** A privacy prompt is in Springboard's
  (`FindSystemElements`); the "Save Password?" sheet is only in Appium's alert text.
- **Alert and sheet buttons match on the button type only.**
- **Check the app's own reaction** (a status line) on the simulator before trusting an action: a
  slider lands within about 5% of the value asked.

## Write the members

Declare `protected virtual` `*Core` methods, element first, and generate (from the Bruinet root):

```
tools/Scripts/CreateNativeIos.sh
```

A control with no `*Core` methods or shortcuts of its own is a plain `.cs` (R5 in
[generator-contract.md](../maui-control/references/generator-contract.md#generate)).

- **`Get*Core` / `Is*Core` give `GetX`/`WaitX`/`AssertX`**, with `[GenerateComparisons]` and
  `[AbsenceTolerant]` as on MAUI.
- **Every other `*Core` gives an action.** It acts once. When it changes state, confirm it inside
  the Core method with `ActAndConfirm(...)`; for a popup that must open after a tap, `Confirm`
  it and fail with `InvalidOperationException` if it does not (never `ElementNotReadyException`
  after acting: the call would act again).
- **No coordinates in the public API.** A gesture is named and element-relative
  (`Swipe(GestureDirection)`, `Scroll`, `LongPress`, `TapAt(x, y)` as fractions of the view).
- **An action iOS has no route for** throws `RouteUnavailableException` naming it (`Hover`).
- **A swipe turns Appium's idle wait off** (`WithoutIdleWait`); with it on, one swipe takes about
  20 seconds.
- **Every public member takes `int? timeoutMs = null`**; a wait below the call takes
  `CallRemainingMs`. No catch-alls, no sleeps.

## Prove it

Add a unit test in `tests/Bruinet.NativeIos.Tests` against a mocked `INativeIosElement`
(`FakeApp`): a mocked view must show the effect of an action, or a confirming member fails. Then
add a view to the gallery sample and a UI test with the `ios-ui-test` skill. Where Android has the
same control, its gallery screen and test are the model.
