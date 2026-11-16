# Remaining work after the proof of concept

Work parked on 2026-11-16 so the proof of concept stays small. Nothing here blocks the small
journey. Each item is picked up after the proof of concept, or earlier only if the small journey
runs into it. Part of [the Contacts pilot](030-Contacts-Pilot.md).

| Item | What is known | Where it waits |
|---|---|---|
| Bruinet search past a WebView2 on Windows | A UI Automation descendant search that reaches a WinUI WebView2 leaves its scope and never reaches elements after it. The fix walks child by child when the scope hosts a WebView2. It needs a Bruinet test that fails without it. | Patch with fix, sample page and test in [parked/webview2-uia-search.patch](parked/webview2-uia-search.patch); apply with `git apply`. Only the WinUI class name is verified, not WPF or WinForms. |
| Rich-text comment on MAUI Windows (J1) | Needs tap opt-ins on the comment's edit icon and the editor's Save, and typing through the WebView2 debugging port. J1 passed twice with the Bruinet fix. | Uncommitted in Construction: the two `AutomationTap` opt-ins with their pin test, and `WebViewDevTools` and `QuillEditorControl` in the MAUI head. |
| Filter sheet (J2) | Fails on MAUI Windows: the Reset button is not found within the filter sheet. | Not investigated. |
| Client picker in the project form (J3) | Fails on MAUI Windows: the picker row is "no longer in its collection, or not realized". | Not investigated. |
| Share as a card (J4) | Not tried. Native Android has no share. | Not started. |
| MAUI on Android (pilot step 3) | DevFlow cannot log in through the in-app web view today; see step 0 check 6. | After the proof of concept. |
| Generic list and dialog steps in Core | Needed by every platform; see [generic steps](040-Generic-Steps.md). | Mac phase I6 waits on this. |
| Scraper Android source | Appium source for the Scraper. | Mac phase I7 waits on this. |
| Flaky Bruinet test | `PagedListTests.ScrollToEnd_LoadsEveryPage` failed once and passed once, with and without the fix. | Not investigated. |
