# Native iOS in Bruinet: plan for the Mac

Status: ready to start, 2026-11-16. Executor: the agent on the Mac. Coordination:
[the handoff board](100-Handoff.md). Parent plans: [the plan](../000-Plan.md),
[the Contacts pilot](../030-Contacts-Pilot.md), [generic steps](../040-Generic-Steps.md).

## Purpose

Build `Bruinet.NativeIos`: native iOS (UIKit apps through Appium XCUITest) as a Bruinet
platform that mirrors `Bruinet.NativeAndroid`. Then, after the handoff, run the Contacts pilot's
iOS step (pilot step 5) on `exact-bouw-ios` and on Construction for iOS.

Paths below are relative to the Construction checkout, with Bruinet in its `Brinell`
submodule. These plans live in the Bruinet repository at `.my/native/`, on `move`; pull
`move` in the submodule to see the latest board.

## Authority and required reading

Read before the first change, and follow over this plan where they disagree:

1. `Brinell/AGENTS.md` and the reading it requires.
2. `Brinell/.docs/decisions/`: AD-001 (Core stays platform-neutral), AD-014 (one scenario file
   serves every head), AD-027 (the Scraper), AD-037 (the MAUI runner; DevFlow judges no gate).
3. `Brinell/docs/platform-guides/native-android.md`, `docs/run/native-android.md`,
   `docs/getting-started/quick-start-native-android.md`.
4. `Brinell/.github/skills/android-control/SKILL.md` and `android-ui-test/SKILL.md`.
5. The construction repository `AGENTS.md`, for the iOS build notes and the Bruinet rules.

## The mirror rule

The native Android API is the contract. iOS has the same shape and the same names wherever the
concept is the same, so a page object and a feature file read alike on both. A difference
exists only where UIKit differs, and each one is written down in the iOS platform guide under
"What is iOS's own".

| Native Android | Native iOS |
|---|---|
| `src/Bruinet.NativeAndroid/` | `src/Bruinet.NativeIos/` |
| `Appium/AppiumNativeAndroidDriver`, `AppiumNativeAndroidElement`, `NativeAndroidByExtensions`, `NativeAndroidTestContext` | `Appium/AppiumNativeIosDriver`, `AppiumNativeIosElement`, `NativeIosByExtensions`, `NativeIosTestContext` |
| `Interfaces/INativeAndroid*` (driver, element, scope, page, container, test context) | `Interfaces/INativeIos*`, the same set |
| `Collections/`, `Containers/`, `Pages/AppRoot`, `Pages/PageObjectBase`, `Testing/NativeAndroidTestFixtureBase` | The same folders and types, `NativeIos` in the name |
| `NativeAndroidLocator`, `NativeAndroidEvidenceCapture`, `NativeAndroidTestContextOptions` | `NativeIosLocator`, `NativeIosEvidenceCapture`, `NativeIosTestContextOptions` |
| `Bruinet.Appium/AndroidHost`, `AndroidRecorder` | `Bruinet.Appium/IosHost`, `IosRecorder` |
| `tools/Scripts/CreateNativeAndroid.Bat` and the `.tpl.cs` to `.gen.cs` generator | A `CreateNativeIos` script (shell, for the Mac) on the same generator |
| `tests/Bruinet.NativeAndroid.Tests` (fake-app unit tests) | `tests/Bruinet.NativeIos.Tests`, one test file per Android test file |
| `tests/Bruinet.NativeAndroid.UITests` (gallery fixture, pages, features, `bdd.config.md` with `Target NATIVEANDROID`) | `tests/Bruinet.NativeIos.UITests`, `Target NATIVEIOS` |
| `samples/Bruinet.Samples.NativeAndroid.App` (.NET for Android activities) | `samples/Bruinet.Samples.NativeIos.App` (.NET for iOS view controllers) |
| Skills `android-control`, `android-ui-test` | Skills `ios-control`, `ios-ui-test` |
| Guides `native-android.md` (platform, run, quick start) | `native-ios.md`, the same three |

Controls are named after UIKit's, as Android's are named after Android's widgets:

| Family | Controls |
|---|---|
| Buttons and text | `Button`, `Label`, `TextField`, `SecureTextField`, `TextView`, `SearchBar` |
| Toggle, range and selection | `Switch`, `Slider`, `Stepper`, `ProgressView`, `SegmentedControl`, `PickerWheel`, `DatePicker`, `TabBar` |
| Containers, dialogs and collections | `ScrollView`, `NavigationBar`, `Toolbar`, `Alert`, `ActionSheet`, `TableView`, `CollectionView` (rows as `ListItem`), `ImageView` |

## Platform rules to settle in phase I2

Write each in the platform guide when it is decided.

- **Locators:** a string locator is an accessibility identifier. Prefer identifiers, then
  predicate strings, then class chains. Avoid XPath: on XCUITest it is the slowest route.
- **Readiness**, in Android's order: the app in front (a system alert or Springboard is a
  not-ready reason that names itself), the page's ready element, then the busy signal: a view
  with accessibility identifier `bruinet_busy`, as on Android.
- **Rows** are keyed by a chosen label's text, else the cell's label, else position. `Item(key)`
  scrolls the table to the row.
- **Gestures** are named and element-relative, as on Android. A route iOS lacks throws
  `RouteUnavailableException`.
- **Text:** a placeholder is never read as a field's value; a secure field is confirmed by length.
- **The host** uses `xcrun simctl` (boot, install, uninstall, erase, open a URL, grant privacy).
  `src/Bruinet.Maui.DevFlow/IosLaunch.cs` already wraps simctl; reuse its approach.

## Phases

Each phase ends with its check. Commit and push after each phase; update the board.

### I0: Set up the Mac

1. Xcode and its command-line tools; one iPhone simulator runtime; Appium 2 or later with the
   `xcuitest` driver; .NET SDK 10.0.400 with the iOS workload.
2. Clone the construction repository with submodules, a sibling `MauiCore` checkout, and
   `exact-bouw-ios` beside it. Brinell on branch `move`.
3. Build a debug simulator build of `exact-bouw-ios` and of Construction for iOS (follow the
   iOS note in the construction `AGENTS.md`).

Check: `appium driver list --installed` shows xcuitest; `dotnet build Brinell/Bruinet.sln`
succeeds; both apps install and launch on the simulator.

### I1: Skeleton and unit tests

Create the project, interfaces, test context options, locator, page and fixture bases, and
collections, per the mirror table, with `PublicAPI` files. Mirror the Android fake-app unit
tests.

Check: `Bruinet.NativeIos.Tests` passes; the public API matches Android's type for type, apart
from the differences listed in the guide.

### I2: Driver, element, readiness, host

The XCUITest driver and element, the `By` extensions, readiness, evidence capture (screenshot
and page source), `IosHost` and `IosRecorder` in `Bruinet.Appium`. Settle the platform rules above.

Check: unit tests for locators, readiness and error mapping pass, as Android's do.

### I3: Controls

Templates and the `CreateNativeIos` script, then the controls. Build the controls the Contacts
pages need first: `Button`, `Label`, `TextField`, `TextView`, `SearchBar`, `SegmentedControl`,
`Switch`, `NavigationBar`, `TabBar`, `TableView`, `CollectionView`, `Alert`, `ActionSheet`. Then
the rest of the families. Each control implements the Core interfaces its Android twin does.

Check: the generated files build; every control has the Core interfaces of its Android twin.

### I4: Gallery sample and UI tests

The sample app mirrors Android's screens (Gallery, Login, Controls, Dialogs, Invoices,
Navigation) with accessibility identifiers equal to Android's resource ids. The UI-test project
has a gallery fixture, gallery pages and the UAT scenario test class with `Target NATIVEIOS`.

The gallery features should be shared, not copied: `controls.feature.md` and
`sign-in.feature.md` run on both platforms. Their metadata names one target today
(`Target NATIVEANDROID`, tag `@android`). Check how the runner filters by target; if a feature
cannot name both, propose the smallest engine change on the board before making it.

Check: both gallery features pass on the iOS simulator and still pass on Android.

### I5: Docs, skills, decision

Write the three iOS guides, the `ios-control` and `ios-ui-test` skills, a decision record "native
iOS mirrors native Android" (use the `decision-record` skill), and the changelog entry.

Check: `docs-upkeep` finds no broken links; the guides list every "iOS's own" difference.

### I6: Generic list and dialog steps

Starts when the board says the Core interfaces from [generic steps](../040-Generic-Steps.md)
have landed. Implement them on `TableView`, `CollectionView`, `Alert` and `ActionSheet`, and add
a gallery scenario that uses `shows only` and `I choose … in …`.

Check: that scenario passes on iOS and Android.

### I7: Scraper source for iOS

Starts when the board says the Android Appium source for the Scraper has landed. Add the iOS
source in the same shape, reading the XCUITest page source.

Check: a capture of the gallery's Controls screen drafts a page object that builds.

### I8: The Contacts pilot on iOS (after the handoff)

Starts when the board shows "Handoff to Mac: ready". Do pilot step 5 from
[the pilot](../030-Contacts-Pilot.md): the native iOS head for `exact-bouw-ios` (fixture with
seven taps, environment pick and deep link; the seven Contacts pages with the same class and
control names as Android; the missing steps), and Construction for iOS. Construction on iOS
gates on Appium (AD-037); DevFlow may be used for the inner loop only.

Check: the Contacts journeys pass twice in a row on both iOS apps; the pilot log has the hours.

## Coordination with the Windows machine

Both machines work in the same construction repository and the same Brinell `move` branch.

- **Ownership.** The Mac owns everything named `NativeIos`, `Ios*` in `Bruinet.Appium`, the iOS
  sample, guides and skills, and later the iOS head projects. The Windows machine owns Core,
  `Bruinet.Bdd`, `Bruinet.NativeAndroid`, the Scraper's Android source, the journeys project and
  the MAUI and Android heads. A change outside your own area goes on the board first.
- **Shared files** (`Bruinet.sln`, `Directory.Packages.props`, `CHANGELOG.md`, doc indexes):
  pull right before editing, make a one-purpose commit, push at once.
- **Git.** `git pull --rebase` before work; small commits; push after each phase. Commit Bruinet
  changes in the submodule on `move` and push them, then record the new submodule commit in
  the construction repository in a separate commit.
- **The board** ([100-Handoff.md](100-Handoff.md)) is the only status. Update your own row
  when a phase starts or ends, and add a line under "Events" for anything the other machine
  waits on.

## Stop and ask

Stop and write the question on the board when:

- a change to Core or `Bruinet.Bdd` seems needed;
- the mirror rule cannot hold for a type or a name;
- `exact-bouw-ios` needs a code change (identifiers or the environment setting);
- a check fails for a reason outside this plan.

## Out of scope

Other Contacts heads, modules other than Contacts, CI pipelines, real devices (simulator only),
and the iOS version of the Construction mock-backend UI tests.
