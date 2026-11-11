# Head parity: review and proposals

Written 2026-11-11 at `b2ea4570`. Review 4 of `additional-reviews-proposal.md`.
Read from the source only; nothing was built or run, so every difference below is what the code says, not what a run showed.

## Outcome

The contracts prove less than their name suggests. Html binds eight of the sixteen contracts on both of its heads, and bUnit skips one of the eight whole. MAUI runs five, on MauiHeadless only: no device head has ever run one. WPF, WinForms, native Android and Stride run none.

Reading the in-process heads against their real heads found about twenty differences the contracts do not ask about. The first five findings let a test pass on bUnit that fails, or does something else, in a browser. The worst is not a bUnit fault: `Submit()` on the browser head skips the app's submit handler.

Nothing here needs a design change. The fixes are small, and each should arrive with the contract test that pins it.

## Short names used below

| Short | File |
| --- | --- |
| Bunit | `src/Bruinet.Html.Bunit/BunitHtmlElement.cs` |
| Playwright | `src/Bruinet.Html.Playwright/PlaywrightHtmlElement.cs` |
| Headless | `src/Bruinet.Maui.Headless/MauiHeadlessElement.cs` |
| FlaUI | `src/Bruinet.Maui.FlaUI/FlaUIMauiElement.cs` |
| Appium | `src/Bruinet.Maui.Appium/AppiumMauiElement.cs` |
| Interactions | `src/MauiHeadless/Interaction/Interactions.cs` |
| Uia | `src/Bruinet.FlaUI/UiaElementBase.cs` |

## Which head runs which contract

Run means a test class binds the contract on that head and CI runs it. Missing means nothing binds it.

### Html and Blazor

Bound in `tests/Bruinet.ControlContracts.Html/HtmlBindings.cs`, run by `BunitContracts.cs` and `ChromiumContracts.cs`.

| Contract | bUnit | Chromium |
| --- | --- | --- |
| Element | run, 3 of 11 skipped | run, the same 3 skipped |
| Clickable | run, Hover and LongPress skipped | run |
| Focusable | bound, all 5 skipped | run |
| Text | run | run |
| EditableText (input, textarea) | run | run |
| Toggle (check box) | run | run |
| Range (slider) | run | run |
| Selector (select, radio group) | run | run |
| Progress, Date, Time | missing | missing |
| Expandable, Tab | missing | missing |
| Scrollable, Swipeable, Refreshable | missing | missing |
| Blazor's own controls (9 tests) | run, 4 skipped | run |

Html has controls for Progress, Date, Time, Tab and a scroll container, so five of the missing rows have something to bind.

### MAUI

Bound in `tests/Bruinet.Maui.UITests/Tests/Contracts/MauiContracts.cs`.

| Contract | MauiHeadless | Windows, Android, iOS |
| --- | --- | --- |
| Range (Slider, Stepper) | run | compiled in, never run |
| Toggle (CheckBox, Switch) | run | compiled in, never run |
| EditableText (Entry) | run | compiled in, never run |
| Focusable (Entry) | run | compiled in, never run |
| Selector (Picker) | run | compiled in, never run |
| Element, Clickable, Text | missing; read here | missing; read here |
| The other eight | missing | missing |

The file says so itself (`MauiContracts.cs:8-10`), and the Android project links the same file. CI sets `BRUINET_HEAD: headless` for every MAUI run (`ci.yml:132`, `coverage.ps1:104-110`).

### The one-head stacks

| Stack | Contracts run | Read for this review |
| --- | --- | --- |
| WPF, WinForms | none | Uia, in full |
| Native Android | none | its element's members only |
| Stride | none | its element's members only |

No test project of these four references `Bruinet.ControlContracts`.

## Html: where bUnit answers differently from the browser

The control layer is shared, so both heads refuse a disabled control the same way (`ClickableControlBase.tpl.cs:26`). The differences are below it, in the two element classes.

### 1. Submit runs the app's handler on bUnit and skips it in the browser

- bUnit raises the form's `onsubmit` (Bunit:216).
- The browser head calls `form.submit()` (Playwright:183-184), which by the DOM's rule raises no submit event and runs no validation.
- So `TextInputControl.Submit()` and `ButtonControl.Submit()` (`TextInputControl.tpl.cs:122-125`, `ButtonControl.tpl.cs:27`) run an `EditForm`'s handler on bUnit and post the page natively in a browser.
- No test on either head calls `Submit()`.
- The fault is in the browser head; `requestSubmit()` is the call that does what bUnit does.

### 2. A value no option has is selected on bUnit

- bUnit's `SelectOption` raises `onchange` with whatever value it is given (Bunit:239).
- Playwright's waits for an option with that value and times out (Playwright:202-203).
- `SelectByValue` passes the value straight through (`SelectorControlBase.tpl.cs:51-57`), and its confirmation reads back the value bUnit just stored (`:79-83`).
- `SelectByText` and `SelectByIndex` look the option up first, so they fail alike on both heads.

### 3. A read-only field takes text on bUnit

- bUnit's `Fill` raises the input and change events whatever the element's state (Bunit:220-237).
- Playwright's `Fill` waits for an editable element and times out on `readonly` (Playwright:200).
- The control checks enabled before acting but not read-only (`ClickableControlBase.tpl.cs:26`; `TextInputControl.tpl.cs:49-58`).
- The contract asks `IsReadOnly` and never writes to the read-only field (`EditableTextContract.cs:76-97`).

### 4. Text is `textContent` on bUnit and `innerText` in the browser

- bUnit reads `TextContent` with whitespace folded to single spaces (Bunit:71, `BunitLocators.cs:63-64`).
- The browser reads `innerText` (Playwright:90).
- A hidden child's text is in bUnit's answer and not in the browser's.
- A `<br>` or a block child is a space on bUnit and a line break in the browser, so `AssertText("Apple pie")` passes on one head only.
- The contract's label is one text node, so it cannot see this (`TextContract.cs`, bound at `HtmlBindings.cs:128-132`).

### 5. Visible, enabled and checked read attributes only

- **Visible:** bUnit checks `hidden`, inline style, `<details>` and `<dialog>` (Bunit:44-65); Playwright also wants a box with a size (Playwright:84). An empty `<div>` or `<span>`, such as an empty message area, is visible on bUnit and not in a browser. The guide names style sheets but not this.
- **Enabled:** bUnit reads the `disabled` attribute on any element (Bunit:67, 479-500); Playwright, by its documented rule, ignores it off form controls and honours `aria-disabled="true"` (Playwright:86). A component library's `aria-disabled` button reads enabled on bUnit.
- **Checked:** bUnit reads the `checked` attribute (Bunit:100); Playwright also reads `aria-checked` on a switch, check box or radio role, and throws for anything else (Playwright:190). An ARIA switch reads unchecked on bUnit.

### 6. Only a click bubbles, and no default action runs

- `Click` finds the nearest handler up the tree (Bunit:123, 393-404). Every other event returns at once unless the element itself listens (Bunit:306-309).
- So a double click, right click or key press on an element whose row or form holds the handler does nothing on bUnit (Bunit:189-203) and works in a browser (Playwright:119-126).
- A click on a `<label>` does not check its input on bUnit (Bunit:120-169); a browser's click does (Playwright:100).
- Enter or Space on a button raises key handlers only on bUnit (Bunit:189-197); a browser clicks it.
- Enter in an input always submits its form on bUnit (Bunit:193-196); a browser needs a submit button or a lone field.
- A submit click raises `onsubmit` past an empty `required` field on bUnit (Bunit:147-158); a browser stops it.
- All of these are silent: nothing happens and nothing is thrown, where the guide's rule for missing abilities is `BrowserOnlyException`.

## MAUI: where MauiHeadless answers differently from Windows

### 7. The contracts have never met a device

- About sixty contract tests (seven bindings) pass on MauiHeadless and are unrun on FlaUI and Appium (`MauiContracts.cs:8-10`).
- Element, Clickable and Text are bound on no MAUI head, so "missing answers null" and "click reaches the control" are unchecked there.
- AD-032 makes Windows the gate at every merge; CI runs no Windows head, so that gate is a local run.

### 8. Click takes a different route

- Headless `Invoke` handles a button, toggles a Switch or CheckBox, selects a row, and otherwise taps the view or the nearest ancestor that takes a tap (Headless:213-266, Interactions:62-110).
- FlaUI `Invoke` is the Invoke pattern and nothing else, and throws without it (FlaUI:402-403, 441-451).
- `ClickableControlBase.ClickCore` calls `Invoke` (`src/Bruinet.Maui/Controls/Base/ClickableControlBase.tpl.cs:41-44`), so a `Button` page object over a tappable Label or Border passes headless and throws on Windows.
- Appium taps for all three verbs (Appium:290-314), so headless is nearer to Android than to Windows here.

### 9. Append, Submit and MaxLength

- **Append:** headless appends to any field (Headless:143-148, 422); FlaUI needs the app to declare the `AppendText` verb and throws without it (FlaUI:1416-1427). The contract's `Append_WritesAfterTheText` has only run headless.
- **Submit:** headless raises `Completed` and the `ReturnCommand` (Headless:433-446, Interactions:155-159); FlaUI raises a bound command only and throws for a `Completed` handler (FlaUI:927-949).
- **MaxLength:** headless cuts a set text at `MaxLength` (Interactions:137-147); FlaUI's own remark says a value write does not apply it (FlaUI:471-475). An assert on the cut text passes headless.

### 10. GetAttribute has two vocabularies

- Headless answers `ClassName`, `Name` and any bindable property by name: `IsToggled`, `Placeholder`, `Text` (Headless:115-124, 725-733).
- FlaUI answers a fixed UI Automation list: `name`, `automationid`, `classname`, `controltype`, `enabled`, `visible`, `helptext` and four scroll values, and null for the rest (FlaUI:856-882).
- Only `name` and `classname` overlap, and `classname` is `Entry` on one head and the WinUI class on the other.
- A page object that calls `GetAttribute` or `WaitAttribute` cannot pass on both heads; the Element contract that would show it is not bound.

### 11. Smaller ones

- **Selected:** headless reports a checked CheckBox as selected (Headless:81-88); FlaUI stopped doing that on purpose (FlaUI:149-164). The controls read `Selected || Checked` (`SelectableItemBase.tpl.cs:54`), so nothing shows today.
- **Placeholder:** headless reads `Placeholder` (Headless:202-207); FlaUI reads UI Automation's help text (FlaUI:233-243), which `SemanticProperties.Hint` also sets. Not settled without a run.
- **Visible** off the viewport is already in the guide's "What Needs A Device".

## Across the stacks

### 12. A missing element answers false everywhere, and the contract says null

- `IsVisible()` and `IsEnabled()` are generated as `…Core(TryFindElement()) == true` on all six stacks (`src/Bruinet.Html/Controls/ViewBase.gen.cs:49-52, 74-77`; `src/Bruinet.Wpf/Controls/Base/ViewBase.gen.cs:40-43, 65-68`; the same in Maui, WinForms, NativeAndroid and Stride).
- The contract and Core's documentation expect null (`ElementContract.cs:33-46`).
- Html skips the three tests with a reason that says WPF and WinForms answer null (`HtmlBindings.cs:74-85`); they no longer do.
- So the one contract rule every stack breaks is skipped where it is bound and unbound elsewhere.

### 13. Tests of a head's edges run on one head

- `HtmlSelectorEdgeTests` and `HtmlRangeEdgeTests` bind `BunitHead` only (`tests/Bruinet.Html.Bunit.Tests/Contracts/HtmlControlEdgeTests.cs:11-21`).
- `PressKey` is tested on bUnit only (`tests/Bruinet.Html.Bunit.Tests/MemberTests.cs:356, 372`).
- CI runs `Bruinet.Blazor.UITests` in the browser only (`coverage.ps1:86`), so nothing in CI runs a real suite on the bUnit head.

### 14. The one-head stacks

- WPF and WinForms share Uia, so one binding of the contracts would cover both.
- Their `Click` tries Invoke, Toggle, SelectionItem and ExpandCollapse in turn, then the mouse (Uia:168-199); MAUI's FlaUI head refuses to guess (FlaUI:378-392). The same word means two things in two stacks.
- Their `Selected` falls back to the Toggle pattern (Uia:80-85), the behaviour MAUI's FlaUI head removed.
- Their check box is set by one `Click` and a confirmation (`src/Bruinet.Wpf/Controls/Base/ToggleControlBase.tpl.cs:39-51`), so a three-state box fails `SetChecked(false)` from checked, loudly.
- Native Android and Stride were not read beyond their members.

## Proposals

Ordered by what a wrongly passing test costs.

1. **Browser head: submit with `requestSubmit()`**, and add a `Submit` test to the EditableText binding on both heads. Fixes finding 1.
2. **bUnit: refuse what a browser refuses.** `SelectOption` for a value no option has, and `Fill` on a `readonly` element, end as the browser's do. Add both to the Selector and EditableText contracts. Findings 2 and 3.
3. **bUnit: make the silent no-ops loud or real.** Bubble double click, right click and keys as click does; let a label's click reach its input and Enter or Space click a button. Where that is too much, throw `BrowserOnlyException`. Finding 6.
4. **bUnit: read `aria-disabled` and `aria-checked`**, and leave hidden children out of `Text`. Write the empty-element and line-break differences into `docs/guides/bunit.md` under "What Behaves Differently". Findings 4 and 5.
5. **Run the MAUI contracts on Windows once**, record which tests that head keeps, and drop the `Head=MauiHeadless` trait for per-test `[Fact(Skip)]` overrides as Html does. Finding 7.
6. **Bind Element, Clickable and Text for MAUI.** They are the three most used and would have shown findings 8 and 10.
7. **Give MAUI's `GetAttribute` one vocabulary**, or take it off the page-object surface for MAUI. Finding 10.
8. **Settle "missing answers null or false"** in Core's documentation and the contract, then delete the three skips. All six stacks already agree on false. Finding 12.
9. **Run the Html edge tests on both heads**, by moving them into the bindings. Finding 13.
10. **Bind the contracts for WPF** on its sample app; WinForms follows from the shared base. Finding 14.
11. **Add the missing Html bindings**: Progress, Date, Time, Tab and Scrollable have controls and no contract.

## Not settled

- Whether Playwright 1.62 treats a disabled `<option>` as unselectable; finding 2 is stated for an absent option only.
- Whether a `MaxLength` Entry on Windows really keeps an over-long value write; the claim rests on the FlaUI head's own remark.
- Whether WinUI publishes an Entry's placeholder as help text, which decides if the three placeholder tests pass on Windows.
- What a password Entry's text reads as on Windows; headless returns the real text (`DefaultElementAdapter.cs:69`).
- Whether anyone has run the MAUI contracts on Windows by hand since `MauiContracts.cs` was written; no record says so.
- Native Android and Stride against the contracts: not read.
