# WPF, WinForms and Stride onto the call model

Status: **plan**, 2026-11-09. Nothing built. Bruinet at 802337ce.

## 1. The outcome

WPF, WinForms and Stride are written, run and fail the way Html, MAUI and native Android do: one
call, one budget, act once and confirm, generated controls with the same members, scopes that
probe their readiness, `WaitFirst` on every scope. When the last of them moves,
`Bruinet.Core.Legacy` is deleted, AD-029 is superseded, and the README's "one way to write a test"
is true for every stack.

The owner asked for this on 2026-11-09. Until now it was parked: AD-029 ruled it out as "a topic of
its own, which nobody has asked for", and the improvements list (item 5) proposed to wait for a user.

## 2. Where things stand

| Stack | Model | Size | Tests |
| --- | --- | --- | --- |
| Html, Blazor | call model, own interfaces (`IHtml*`) | 64 files | bUnit and browser heads |
| MAUI (+ FlaUI, Appium, Headless) | call model, own interfaces (`IMaui*`) | 172 files | headless, Windows, Android |
| Native Android | call model, own interfaces (`INativeAndroid*`) | 91 files | emulator |
| WPF | first model, `Bruinet.Core.Legacy` | 36 files, FlaUI 1 100 lines | 25 facts, 4 features |
| WinForms | first model, `Bruinet.Core.Legacy` | 40 files, FlaUI 880 lines | 59 facts, 2 features |
| Stride | first model, `Bruinet.Core.Legacy` | 35 files, named pipe | 30 facts, 2 features |

What the three old stacks have in common, from the code:

- Lookups wait: `IDriver<T>.FindElement(locator, timeoutMs = 5000)`, so a call's waits add up.
- `ControlBase` is hand-written: `RunWithElement`, `Poll`, `PollWithElement`, `RunAssert` with
  their own stopwatches and `WaitHelper.Pause`; no `ControlCalls`, no `ControlCall`, no log pair.
- Few Core capability interfaces: across the three, 3 `IClickableControlObject`, 2 range,
  2 selector, 2 toggle. No editable text, focusable, text, date or expandable, so most UAT verbs
  cannot reach them.
- No generator: plain `.cs` controls, no `.tpl.cs`, no `Create*.Bat`.
- WPF and WinForms each carry a FlaUI driver and element of their own, beside
  `Bruinet.Maui.FlaUI` (5 800 lines), which already has the call-model side of FlaUI: one-attempt
  lookups, `FlaUIErrors` (an element gone is stale, not missing), `InstanceKey` from the runtime
  id, `WindowCapture`, window placement.
- Stride has no element handle at all: every read is `GetElementState(automationId)` over the
  pipe, and the protocol has `Wait` commands that wait inside the game on a budget of their own.

Native Android is the template: commit 054313ad moved it in one step (170 files, +6 600 / −4 500)
onto its own interfaces, the Core calls layer, generated controls and readiness probes.

## 3. AD-010 reconsidered

AD-010 says each call-model stack owns its interfaces until "the proven shape moves down to Core,
one layer at a time". The leaf layer moved down (2026-09-26) and the control calls did
(`ControlCalls<TElement>`, 2026-10-22). What it still keeps per stack: the element, driver, scope,
page and context interfaces, collections, pages, item keys.

**What three stacks show now.** Compared member by member at 802337ce:

- **Scope**: `IMauiElementScope` and `INativeAndroidElementScope` have the same members, and
  `IHtmlElementScope` too: `Context`, `DefaultLocatorStrategy`, `Page`, `ProbeReadiness`,
  `IsReady`, `WaitReady`, `WaitFirst`, `TryFindElement`, `FindElement`, `FindElements`. They differ
  only in the types they return (and MAUI's `DescribeMiss`).
- **Page**: `IMauiPage`, `IHtmlPage`, `INativeAndroidPage` have the same eleven members (`Name`,
  `IsLoaded`, `WaitLoaded`, `AssertLoaded`, `IsBusy`, `WaitBusy`, `AssertIdle`, `GetTitle`,
  `WaitTitle`, `AssertTitle`, `TakeScreenshot`), plus `Url` on Html and `Activity` on Android.
- **Element and driver**: really differ. MAUI's element carries some forty platform verbs (flyout,
  toolbar, dropdown, scroll steps, the bridge); Android's carries `ResourceId`, `Checked`,
  `Scroll(direction)`; Html's carries the DOM. Nothing would be gained by one Core element.

So the draft's goal of "one element, one driver, one scope and one page contract in Core" was half
wrong: the element and driver should stay per stack for good, and the scope and page are proven
and the same. Porting three more stacks under AD-010 as it stands writes copies four, five and six
of identical scope and page contracts, which AD-010 itself says would then "have to be undone in
the move down".

**Proposal: amend AD-010, do not drop it.**

1. **The element-free part of the scope, page and context moves down to Core now**, before any old
   stack moves, as the third layer. Core gets `IScope` (`DefaultLocatorStrategy`, `ProbeReadiness`,
   `IsReady`, `WaitReady`, `WaitFirst`), `ITestPage : IScope` (the eleven page members) and the context
   members that do not name an element. Named in U1: `IScope` and `ITestPage` (not `IPage`, Playwright's page interface), in
   `Bruinet.Core.Scopes`; the context's element-free members are `Name` and `NearMiss`, on
   `ITestContext`.
2. **The typed half stays per stack**: each stack's scope interface derives from Core's and adds
   `Context`, `Page`, `TryFindElement`, `FindElement`, `FindElements` with its own element type.
   No generic `IElementScope<TElement, TContext, TPage>`: three type parameters on every page
   object read worse than one derived interface per stack.
3. **The element and the driver are per stack for good.** AD-010's "until it moves down" no longer
   applies to them; the amendment says so.
4. **Composition and UAT find pages by Core's `ITestPage`**, so `[TestPage]` on page bases and
   `TestPages.IsFirstModelPage` (finding a page by an interface's name) can go.
5. **The calls layer becomes public** (Q5): `ControlCalls`, `IControlHost` and the leaf types they
   use go into Core's PublicAPI files, and Core's `InternalsVisibleTo` for the stacks goes. Every
   stack, the three new ones included, is written against Core's published surface. A change to
   `IControlHost` is from then on a breaking change in the CHANGELOG.

Why this order: the porting work then lands every old stack on Core's contracts directly, and WPF
becomes the fourth proof of the shape instead of the fourth copy. AD-010's "Broken when" keeps its
point (no adapters, no Core changed ahead of a deliberate move): this move *is* the deliberate one.

What it costs: a pure rename in Html, MAUI and native Android (each stack interface derives from
Core's; no behaviour changes), the PublicAPI files, and a run of their suites.

Alternative considered: port first on stack-owned interfaces (`IWpf*`, `IWinForms*`, `IStride*`,
AD-010 as it stands), move down afterwards. Rejected: six copies, then a move that touches six
stacks instead of three.

## 4. A shared FlaUI layer

`Bruinet.FlaUI` (AD-026 R4, as `Bruinet.Appium` is under the MAUI and Android Appium stacks):
what MAUI's Windows head, WPF and WinForms all need from FlaUI, taken out of `Bruinet.Maui.FlaUI`.

| Moves to `Bruinet.FlaUI` | Stays in `Bruinet.Maui.FlaUI` |
| --- | --- |
| `FlaUIErrors`, the one-attempt lookup over an `AutomationElement`, `InstanceKey` from the runtime id, `LocatorExtensions` (AutomationId, Name, ClassName, XPath) | The UIA bridge (`Bridge/`, `Bruinet.Uia.Contracts`), `FlaUIDeclaredElement`, MAUI's Shell chrome and verbs |
| `WindowCapture`, `Windowing/` (`AppWindow`, `QuietWindow`, `WindowPlacement`, physical pixels) | |
| Launch and attach of a desktop app (`DesktopApp`) | The idle check: MAUI asks its bridge; WPF and WinForms get their own in U3 and U4 |

WPF and WinForms then keep a thin element each (their patterns: `ExpandCollapse`, `Value`,
`RangeValue`, `SelectionItem`, `Toggle`, the WinForms `DataGridView` grid pattern).

Open question Q1 below asks whether WPF and WinForms should then be one stack.

## 5. Steps

Each step ends green on its suites, with a commit, as `.my` topics do. Baselines first: several
suites fail before any change.

| Step | What | Done when |
| --- | --- | --- |
| **U0** | Baselines: run the WPF, WinForms and Stride UI and UAT suites twice; record failures and tests over 5 s in `baseline.md`. The WPF login baseline had 2 failing tests in September. | `baseline.md` lists each suite's result and slow tests |
| **U1** | AD-010's third layer: Core's `IScope`, `ITestPage`, context members; Html, MAUI, native Android derive from them; Composition and UAT find pages by `ITestPage`; the calls layer public (PublicAPI files, the stacks' `InternalsVisibleTo` removed, XML docs on every public member); AD-010 amended. | The three suites as green as before; `SolutionTests` and Architecture tests updated |
| **U2** | `Bruinet.FlaUI` taken out of `Bruinet.Maui.FlaUI`; MAUI's Windows head runs on it. | MAUI Windows suite as green as before |
| **U3** | WPF on the call model (below). | WPF suites at baseline or better; an R0 proof |
| **U4** | WinForms on the call model, the same way. | WinForms suites at baseline or better; an R0 proof |
| **U5** | Stride protocol: one-attempt reads, identity, no in-game waits (below). | `Bruinet.Stride.Automation.Tests` green |
| **U6** | Stride on the call model. | Stride suites at baseline or better |
| **U7** | `Bruinet.Core.Legacy` deleted, AD-029 superseded, `NoCallModelStack_ReferencesTheFirstModel` becomes "no project references Legacy" then goes; docs, heads table, CHANGELOG, skills. | The solution builds without Legacy; every suite run once |

### Per stack (U3, U4, U6): as native Android did

1. **Interfaces**: `IWpfElement` (one attempt, `InstanceKey`, no `timeoutMs` on lookups),
   `IWpfDriver : IDiagnosticDriver`, `IWpfElementScope : IScope`, `IWpfPage : ITestPage`,
   `IWpfTestContext : ITestContext`, `IWpfScope<TSelf>`, `IWpfContainer`.
2. **Element adapter** on `Bruinet.FlaUI`: element-not-available is stale, a missing element is
   null, never a wait.
3. **Readiness**: the page's `ProbeReadiness` (the window in front, the page's root, a busy
   signal); containers probe their parent then their root.
4. **Control bases as hosts**: `ControlBase.tpl.cs` implements `IControlHost<IWpfElement>`; the
   `Run*` family as one-line delegations to `ControlCalls` (R8); capability bases
   (`Clickable`, `Focusable`, `Range`, `Selector`, `Toggle`, `Scrollable`, editable text) implement
   Core's capability interfaces.
5. **Controls generated**: every control a `.tpl.cs` with `*Core` methods,
   `tools/Scripts/CreateWpf.Bat`; type names stay WPF's (`TextBox`, `PasswordBox`, `TreeView`),
   members are MAUI's (AD-013).
6. **Containers, collections, items**: `ListBox`, `TreeView`, `TabItem`; WinForms `DataGridView`,
   `GroupBox`, `TabControl`; item keys as Android's.
7. **Pages and fixture**: `PageObjectBase` without `PageLoadException`, `AppRoot` with `WaitFirst`,
   the fixture on `ITestContext`; `UatTargetConventions` keeps `WPF`.
8. **Tests**: page objects and UAT pages moved to the new members; `Bruinet.ControlContracts`
   gets WPF and WinForms bindings, so every Core capability member is checked on the new stack.
9. **Prove it**: the suite twice, the baseline compared, an R0 proof (put a known, fixed sample-app
   bug back; the suite must fail on it), `BRUINET_CALL_LOG` read once.
10. **Docs**: the platform guide, AD-013's stack list, the heads table, CHANGELOG, a `wpf-control`
    skill or the html/android control skills widened.

### Stride (U5, U6)

The pipe is the driver; the call model needs three things of it.

- **One-attempt reads**: `GetElementState` already is; the `Wait` command type goes (no compat
  code, per dev mode), its uses become polls in `ControlCalls`.
- **Identity**: `ElementState` gets an instance key (the UI element's id in the scene), so a
  replaced element is stale, not the same.
- **Children**: `FindElements(parentId, locator)` in `StrideUIHandler`, for containers and lists.

Then the element is a snapshot handle over the pipe (`StrideElement` holding the key; every read
one `Query`), and the rest is the per-stack list above. The context also names its window
(`IRecordable`), which closes the open recorder item in the `.my` index.

## 6. Risks

- **Timing**: lookups stop waiting, so tests that relied on nested waits adding up need a
  `timeoutMs` (as MAUI's long-list search did). U0's slow-test list says where to look.
- **Desktop only**: WPF and WinForms suites need a desktop and stay out of CI (ci.yml says so);
  every proof is a local run.
- **FlaUI extraction** (U2) touches the MAUI Windows head, which is the owner's most used desktop
  head. It is a move, not a rewrite; run its full suite.
- **Stride sample**: the game must be rebuilt with the new `Bruinet.Stride.Automation`; the old
  path in `samples/TestRunner.ps1` is already wrong.

## 7. Open questions, with proposals

| # | Question | Proposal and why |
| --- | --- | --- |
| Q1 | WPF and WinForms as one stack (`Bruinet.Windows` on `Bruinet.FlaUI`, two control sets) or two? | **Decided 2026-11-09: two control packages over `Bruinet.FlaUI`.** Controls the two toolkits share under UI Automation (`Button`, `CheckBox`, `TextBox`, `ComboBox`) may move into `Bruinet.FlaUI` in U4, once WPF's generated controls show the overlap. Why: after U2 the driver, element core, pages and fixture are shared; each keeps its widget-named controls, as a control source earns its own package (AD-026). Merging the control sets would put WinForms' `DataGridView` beside WPF's `TreeView` for users of neither. |
| Q2 | Does U1 happen at all, or AD-010 as it stands? | **Decided 2026-11-09: U1 first.** AD-010 amended ("The scope and page layer moves down next, the element never"); AD-029's ruled-out bullet points here. |
| Q3 | The WPF and WinForms apps load `Bruinet.Uia.Contracts` (the UIA bridge) for gestures and semantic verbs, as MAUI's Windows app does (AD-008)? | **Decided 2026-11-09: not in this plan.** UIA's own patterns cover what the suites do today; a follow-up if a gesture is asked for. |
| Q4 | Order: WPF or WinForms first? | **Decided 2026-11-09: WPF first.** Its suite is smaller (25 facts), it has a known baseline, and it shares XAML habits with MAUI's Windows head; WinForms then reuses everything but the controls. |
| Q5 | Calls layer public? | **Decided 2026-11-09: public**, against the proposal to keep it internal: every stack is written against Core's published surface, and Core stops listing each stack. Section 3 point 5; AD-010 amended. |

U0 done 2026-11-09: [baseline.md](baseline.md). WPF 2 of 16 fail, WinForms 34 of 65, Stride 3 of 30,
the same on both runs; every UAT scenario passes.

U1 done 2026-11-09: Core's `IScope` and `ITestPage` (`Bruinet.Core.Scopes`), `ITestContext.Name` and
`NearMiss`; the Html, MAUI and native Android interfaces derive from them (18 declared members each
gone from their PublicAPI); pages found by `ITestPage`; the calls layer public, Core's
`InternalsVisibleTo` for the stacks removed. CI's set run locally (`coverage.ps1 -Headless -Browser`):
29 projects, 0 failures. Not run: the native Android UI suite (emulator) and MAUI on a device.

U2 done 2026-11-09: `Bruinet.FlaUI` (`DesktopApp`, `UiaElements`, `FlaUIErrors`, `LocatorExtensions`,
`PhysicalPixels`, the windowing) with `Bruinet.FlaUI.Tests`; `FlaUIMauiDriver` holds a `DesktopApp`,
`FlaUIMauiElement` calls `UiaElements`, their members unchanged. Public, as the calls layer is (Q5);
`AppWindow`, `QuietWindow`, `WindowPlacement`, `WindowCapture` and `NativeMethods` stay internal.
The MAUI suite on Windows: 417 of 434 pass. The 13 collection failures (`ProductCollectionTests`,
`PagedListTests.RowOnPageThree`, `RowTapTests`: no rows materialized, rows without Invoke) fail on
the code before U2 too, and `PagedListTests.ScrollToEnd_LoadsEveryPage` and
`ExpanderTests.Expander_ExpandThenCollapse_HidesContent` are flaky on both (3 runs each). Not
looked into: they are MAUIs, not this plans.

U3 done 2026-11-10. `Bruinet.Wpf` on the call model, ported from native Android's shape; decisions made:

- The pattern element is shared: `UiaElementBase` in `Bruinet.FlaUI` (Click through Invoke, Toggle,
  SelectionItem or ExpandCollapse; text through Value; ranges through RangeValue; ScrollItem, Scroll). WPF's
  element adds only its typed lookups; WinForms' will too. Pointer and keyboard only for DoubleClick,
  RightClick, Hover, LongPress and `SendKeys`, through `PhysicalInput` (AD-005 unchanged); the
  architecture test allows that one file and checks MAUI's element does not derive from it.
- No scroll-to-find on a control: WPF keeps scrolled-away elements in the tree. Collections scroll a page
  per attempt (Scroll pattern), rows are `ListItem` (or `DataItem`) by default, keyed by first text.
- A page is known by `ReadyLocator` (default: AutomationId = page name); busy by `BruinetBusy` or
  `BusyIndicators`. `NavigateTo`, `NavigateBack` and `Refresh` throw `NotSupportedException`.
- Control set: `Button`, `CheckBox`, `ToggleButton`, `RadioButton`, `TextBox`, `PasswordBox` (written,
  never read back), `Label`, `ComboBox`, `ListBox`, `TabItem` (`ITabControlObject`, `Title`),
  `Slider`, `ProgressBar`, `ScrollView`, `Expander`, `TreeViewItem`. The old `TreeView` is gone.
- Suite changes: `ShellPage` is known by `AppTitleText` (its old default, an AutomationId `ShellPage`
  that does not exist, cost every test its 10 s page-load budget); `LoginPage.WaitNotBusy` is one
  `WaitFirst`; the busy test asserts `AssertExists(false)`, since a collapsed element is not in the tree.

Results: unit tests 37 of 37 (new: `FakeApp`, `CallModelTests`, `ControlTests`); UI suite 16 of 16
twice, 17 s and 20 s (baseline 2 of 16 failed, 2 m 53 s); R0: username rule loosened to 2 characters,
`Login_WithShortUsername_ShowsValidationError` failed on it, restored. Left: a `wpf-control` skill.

U4 done 2026-11-10. `Bruinet.WinForms` ported from `Bruinet.Wpf` by renaming (Q1 holds: two packages; the
shared element was already `UiaElementBase`, so no control moved into `Bruinet.FlaUI`). Measured on the sample
app first (`dumpwf`, `probewf`, see below), then decided:

- `TrackBar` and `NumericUpDown` have no RangeValue: their number is Value text (the numeric box's in its
  child `Edit`), bounds and step from the page object (`ValueRangeControlBase`, as Android's ranges).
- A Value write to a `NumericUpDown` is not committed until focus leaves it, and the app's own reset to the
  old number then did not refresh the text (Clear showed 9000 over 8080). Committed with its spinner buttons:
  Up then Down (Down then Up at the maximum). Pattern-only, works on a locked desktop.
- `RichTextBox` reads back with a closing `\r`: trimmed (`ShownText`).
- `ComboBox` selects by writing its Value: WinForms takes it as choosing the item (Login then reports the
  role), ignores an unknown text, and each combo call blocks about 2 s, so one write instead of three.
- `DateTimePicker`: the Value write is ignored and the calendar refuses Invoke on a locked desktop, so the
  date is typed field by field in the culture's short date order (`dd-MMM-yy` here), two passes so a day is
  not clamped, no Enter (it would press Login). Confirmed by parsing the shown date. **Not verified**: the
  desktop was locked all night, and Windows refuses synthetic keys then; `PressKey` now says so.

Results: unit tests 48 of 48; UI suite 62 of 65 twice (1 m 43 s, 1 m 49 s; baseline 31 of 65, 1 m 24 s). The
3 left all set a date and fail only on the refused keys. The two date tests now assert the date, not just
some text. R0: Clear no longer reset the port, `Form_Clear_ResetsAllFields` failed on it (8080 vs 9000),
restored.

Expected next action: U5, the Stride protocol.
