# Remaining work after the proof of concept

Work parked on 2026-11-16 so the proof of concept stays small. Nothing here blocks the small
journey. Each item is picked up after the proof of concept, or earlier only if the small journey
runs into it. Part of [the Contacts pilot](030-Contacts-Pilot.md).

| Item | What is known | Where it waits |
|---|---|---|
| Bruinet search past a WebView2 on Windows | A UI Automation descendant search that reaches a WinUI WebView2 leaves its scope and never reaches elements after it. The fix walks child by child when the scope hosts a WebView2. It needs a Bruinet test that fails without it. | Patch with fix, sample page and test in [parked/webview2-uia-search.patch](parked/webview2-uia-search.patch); apply with `git apply`. Only the WinUI class name is verified, not WPF or WinForms. |
| Rich-text comment on MAUI Windows (J1) | Needs tap opt-ins on the comment's edit icon and the editor's Save, and typing through the WebView2 debugging port. J1 passed twice with the Bruinet fix. | The two `AutomationTap` opt-ins and their pin test are uncommitted in Construction. `WebViewDevTools` and `QuillEditorControl` are committed in the MAUI head. |
| Filter sheet (J2) | Fails on MAUI Windows: the Reset button is not found within the filter sheet. | Not investigated. |
| Client picker in the project form (J3) | Fails on MAUI Windows: the picker row is "no longer in its collection, or not realized". | Not investigated. |
| Share as a card (J4) | Not tried. Native Android has no share. | Not started. |
| MAUI on Android (pilot step 3) | DevFlow cannot log in through the in-app web view today; see step 0 check 6. | After the proof of concept. |
| Generic list and dialog steps in Core | Needed by every platform; see [generic steps](040-Generic-Steps.md). | Mac phase I6 waits on this. |
| Scraper Android source | Appium source for the Scraper. | Mac phase I7 waits on this. |
| Flaky Bruinet test | `PagedListTests.ScrollToEnd_LoadsEveryPage` failed once and passed once, with and without the fix. | Not investigated. |
| Native Android pages and other tabs | The contact list and pull-to-refresh locators use ids that each tab's page also has. They work now because Contacts is the tab shown. The list is ready when the toolbar title is the English "Contacts", so another language breaks it. | `Journeys/Exact.Bouw7.NativeAndroid.UITests/Pages/ContactPages.cs`; scope them to the shown page before J1. |
| Native Android list after saving | The list shows a new contact only after a pull to refresh. The step pulls until it shows. Ask whether this is the app's intended behaviour. | Parity question for the Contacts module. |
| Native iOS head needs `exact-bouw-ios` | The head reads the iOS app's test client from a sibling `exact-bouw-ios` checkout (or `BOUW_IOS_REPO`) and runs the app built into `~/.bruinet/bouwios-dd`. A CI run needs both. | `Journeys/Exact.Bouw7.NativeIos.UITests/IosAppClient.cs` and `NativeIosJourneyFixture.cs`. |
| Native iOS pages use English text | `exact-bouw-ios` has no accessibility ids, so the pages find controls by English label and XPath. Another simulator language breaks them. | `Journeys/Exact.Bouw7.NativeIos.UITests/Pages`; ask for accessibility ids in the iOS app, or scope by language, before J1. |
| Mac asks of Windows | Rerun the native Android gallery suite, and build and test `Bruinet.Docs.Samples` and the docs tests. | Not done in the proof of concept. |
