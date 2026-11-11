---
name: android-control
description: Create or extend a Bruinet native Android ControlObject - a simple control, a container, a component with named parts, or a collection with rows - for an Android view (a widget, a Material component, a RecyclerView) in src/Bruinet.NativeAndroid. Use when asked to add, write or extend a native Android control object, container, collection or row, or when a native Android UI test needs a control member that does not exist yet.
---

# Write a native Android ControlObject

Native Android controls are generated, as MAUI's and Html's are: a template `Foo.tpl.cs` holds the
constructors, `*Core` methods and hand-written members, and `bruinet controls generate` writes the
public API into `Foo.gen.cs`. The rules are the call model's (`.docs/contracts/call-model.md`,
`.docs/contracts/control-object.md`) and the generator contract
([generator-contract.md](../maui-control/references/generator-contract.md)), with
`INativeAndroidElement` as the element type. Controls are named after Android's widgets
(`EditText`, `Spinner`, `RecyclerView`), never MAUI's.

## Choose the kind

| Kind | Base | Example |
| --- | --- | --- |
| Simple control | `ViewBase<TScope>` or a family base (`ClickableControlBase`, `FocusableControlBase`, `ToggleControlBase`, `RangeControlBase`, `SelectorControlBase`, `ScrollableControlBase`) | `EditText<TScope>`, `SeekBar<TScope>` |
| Options among its children | `ChoiceGroupBase<TScope>`: say which children are options and how one shows it is chosen | `RadioGroup`, `TabLayout`, `BottomNavigationView` |
| Container or component | `ContainerObjectBase<TParent, TSelf>`; parts are controls declared on it | `Toolbar<TParent>`, `AlertDialog<TParent>` |
| Collection with rows | `CollectionObjectBase<TParent, TSelf, TItem>` plus `ItemObjectBase<TSelf>`; `RecyclerView<TParent>` with `ListItem` when rows need no type of their own | `InvoicesPage.Invoices` in the UI tests |

## Probe the view

Dump the screen on the emulator (from the Bruinet root):

```
adb shell uiautomator dump /sdcard/ui.xml; adb pull /sdcard/ui.xml
```

Read the class, `resource-id`, `content-desc`, `text`, `checkable`/`checked`, `selected`,
`scrollable` and the bounds. Then check what UiAutomator2 does, which the dump does not show:

- **A view below the fold is not in the tree.** A control on a page scrolls it in
  (`UiScrollable`); a control in a row or a dialog does not (`ScrollsToFind`).
- **An XPath under a view is rooted at that view:** `./*` is the view itself. Write `./X` for a
  child and `.` for the view; `ToAndroidChildBy` rewrites them.
- **A popup is a window of its own** (spinner options, suggestions); the session shows every
  window (`enableMultiWindows`), so find it from `Context`, not from the control.
- **An accessibility action may not reach the app.** `SendKeys` on a `SeekBar` sets its progress
  and the app hears it; on a `RatingBar` it does not, so a rating is a tap. Check the app's own
  reaction (a status line) on the device before trusting an action.

## Write the members

Declare `protected virtual` `*Core` methods, element first, and generate (from the Bruinet root):

```
tools\Scripts\CreateNativeAndroid.Bat
```

A control with no `*Core` methods or shortcuts of its own is a plain `.cs` (R5 in
[generator-contract.md](../maui-control/references/generator-contract.md#generate)). The existing
templates with an empty generated partial (`SeekBar.tpl.cs` and others) predate the rule: debt,
not a model.

- **`Get*Core` / `Is*Core` give `GetX`/`WaitX`/`AssertX`**, with `[GenerateComparisons]` and
  `[AbsenceTolerant]` as on MAUI.
- **Every other `*Core` gives an action.** It acts once. When it changes state, confirm it inside
  the Core method with `ActAndConfirm(...)`; for a popup that must open after a tap, `Confirm`
  it and fail with `InvalidOperationException` if it does not (never `ElementNotReadyException`
  after acting: the call would act again).
- **No coordinates in the public API.** A gesture is named and element-relative
  (`Swipe(GestureDirection)`, `Scroll`, `LongPress`, `TapAt(x, y)` as fractions of the view).
- **An action Android has no route for** throws `RouteUnavailableException` naming it (`Hover`).
- **Every public member takes `int? timeoutMs = null`**; a wait below the call takes
  `CallRemainingMs`. No catch-alls, no sleeps.

## Prove it

Add a unit test in `tests/Bruinet.NativeAndroid.Tests` against a mocked `INativeAndroidElement`
(`FakeApp`): a mocked view must show the effect of an action, or a confirming member fails. Then
add a view to the gallery sample and a UI test with the `android-ui-test` skill.
