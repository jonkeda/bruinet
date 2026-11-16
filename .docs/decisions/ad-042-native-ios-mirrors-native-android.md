# AD-042: Native iOS Mirrors Native Android

**Decided:** `Bruinet.NativeIos` (2026-11-16) is native Android's stack again for UIKit through
Appium and XCUITest. Android's public API is the contract: a page, context, control or collection
member that Android has keeps its name, parameters and meaning on iOS, and the two galleries
share their screens, identifiers and texts, so one set of feature files (`Target | NATIVEANDROID,
NATIVEIOS`, tag `@native`, AD-014) runs on both. Control types carry UIKit's names (AD-013). What
iOS does differently is listed once, under "What is iOS's own" in the
[native iOS guide](../../docs/platform-guides/native-ios.md); a member iOS cannot do throws
`RouteUnavailableException`.

**Why:** a team that tests both apps writes its scenarios once, and a test author who knows one
stack knows the other. XCUITest differs from UiAutomator2 in ways a test should not see (system
prompts in Springboard, off-screen table rows, slow swipes), so the stack absorbs them.
Evidence: the [plan](https://github.com/exact-mobile-apps/test-tooling/blob/1d545f07/.my/native/ios/000-Plan.md);
the shared feature files pass on the iPhone 17 simulator at `1d545f07`.

**Ruled out:**
- an iOS-only member where Android's member covers the behaviour, or a different name for it;
- a feature file per platform for a screen both galleries have;
- reaching XCUITest from a test (`mobile:` commands, `simctl`) to work around a difference;
- `mobile: scroll toVisible` to reach a row (it took minutes on a long table).

**Broken when:** a shared feature file fails on one native head with "available methods" for a
member the other has; a public member of `Bruinet.NativeIos` has no counterpart in
`Bruinet.NativeAndroid` and is not in the guide's "What is iOS's own"; the two galleries'
identifiers for the same screen differ.
