# AD-014: One Scenario File Serves Every Head

An app with more than one head (the Todo sample's MAUI app and web app) has one set of `.feature.md`
scenario files. Each head has its own UAT suite, which runs those files - the web suite links
them from the MAUI one - through page objects and phrase classes of its own that use the same
names and phrases. `Head` in a suite's `uat.config.md` names its head; a scenario's head tags
(`@maui`, `@web`) say where it runs, and the engine skips it on any other head. A scenario with no
head tag runs on every head.

Within one scenario, several apps (an `Apps` table, `IBddSessionHost`, a step's subject per
AD-015) are apps of the suite's head and share its page and phrase types. The exception is a
suite whose `Head` is `mixed`: it runs only scenarios tagged `@mixed`, its apps name their own
`Head`, and each app binds against that head's catalog (`IBddSessionHost.CompositionFor`,
`BddCatalogSet`). Mixed scenarios live in the mixed suite, not beside the one-head files.

A tier below the UI is a head too, `service`, opt-in like `mixed`: see AD-020.

The same app built natively, outside MAUI, is a head of its own: `native-android` and
`native-ios` (the NativeAndroid and NativeIos platforms). `@native` is shorthand for both; it is a
tag, never a suite's `Head`. `@android` and `@ios` stay MAUI driver tags and never mean a native
app. Evidence: [000-Plan.md](https://github.com/exact-mobile-apps/test-tooling/blob/7c44c49a/.my/native/000-Plan.md).

**Why:** a journey is the same on both heads; two copies of its scenario text drift, and a
difference between heads should show as a failing scenario, not as two files that say different
things. Evidence (at 7162ac7): [review-html-vs-maui.md](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/html/review-html-vs-maui.md) section 4
(items 7, 8), [todo-blazor.md](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/html/todo-blazor.md) section 4.

**Ruled out:** a copy of a scenario file per head; head-specific wording for the same step; a
skip rule or environment variable standing in for a head tag; apps of different heads in one
scenario outside a `mixed` suite; `@android` or `@ios` to mark a scenario for a native app.

**Broken when:** a scenario file exists in two suites' folders; a step passes on one head only
because the other head's phrase class words it differently; a suite with `Head` runs a scenario
tagged only for another head; a session switch resolves page types from another head; a one-head
suite runs a `@mixed` scenario; a native suite skips a scenario tagged `@native`.
