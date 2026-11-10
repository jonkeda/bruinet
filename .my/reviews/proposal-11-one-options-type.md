# Proposal 11: one options type per platform

Drafted 2026-11-10 at `2014b0f0`, for the owner to approve. Nothing here is made yet.

## Outcome

Each platform keeps one type a test author sets, its `…TestContextOptions`, and a settings file binds straight to it.
Today the same setting has two or three homes (Maui's app path is in `MauiOptions` and `MauiDriverOptions`), the file's values are copied by hand in each fixture, and some never arrive: WPF's fixture hard-codes the timeouts whatever the file says.
Thirteen types go, two of them dead already (`BruinetBlazorConfiguration` and `BlazorOptions` are never loaded).
Tomorrow sets its options in code and has no settings file, so its cost is close to none.

## Today

Per stack there is a file root (`Bruinet{X}Configuration`), a section in it (`{X}Options`), and the context's own options (`{X}TestContextOptions`); the fixture copies the section into the context options.
Html adds `HtmlOptions` and Core's `BrowserOptions` in the file and `HtmlTestContextOptions` in code; Maui adds `MauiDriverOptions`, which repeats seven of `MauiOptions`' fields and carries its own `Timeouts` and `Logger` beside the context's.
Which value wins cannot be read from the types: the context uses its own timeouts, the driver its own unless unset.

## The design

| # | Rule | What changes |
| --- | --- | --- |
| 1 | One shared base, `TestContextOptionsBase` in Core: `Timeouts`, `Logger`, `Name`, `NearMiss`, `Artifacts`. | Each stack's `…TestContextOptions` derives from it and keeps only its own fields; six copies of five fields become one. |
| 2 | A settings file binds to the context options itself: `bruinet.{x}.config.json` holds the same names as the type. | `Bruinet{X}Configuration`, `{X}Options`, `HtmlOptions` and `BrowserOptions` go; the fixture loads the file, then its `CreateTestContextOptions(loaded)` adjusts it in code. |
| 3 | Code wins over the file, the file over the defaults; the run switches (`BRUINET_HEAD`, `BRUINET_CALL_LOG`, `BRUINET_TIMEOUT_SCALE`) apply on top, as now. | Written on the base type and in a new guide, `docs/guides/configuration.md`. |
| 4 | A head's own settings nest, without copies of the shared ones: `MauiTestContextOptions.Driver` is `MauiDriverOptions` minus `Timeouts` and `Logger`. | The driver takes the context's timeouts and logger; Android's `NativeAndroidDriverOptions` becomes `NativeAndroidTestContextOptions` the same way. |
| 5 | What is not the test context stays apart: `WebAppHostOptions` hosts the app under test, `BunitTestContextOptions` derives from `HtmlTestContextOptions`. | No change. |
| 6 | Dead types go. | `BruinetBlazorConfiguration`, `BlazorOptions`; `BlazorTestFixtureBase` uses `HtmlTestContextOptions` as Html's does. |

## Who pays

- Projects with a `bruinet.{x}.config.json`: the file's shape changes (the `Maui`, `Wpf`… section unwraps into the root, `Browser.Headless` becomes `Headless`); Bruinet's own samples have three such files, Tomorrow none.
- Projects that override a fixture's `CreateTestContextOptions()`: it receives the loaded options instead of reading `Configuration`.
- Projects that build options in code, as Tomorrow does: shared fields keep their names, so nothing changes unless they set `MauiDriverOptions.Timeouts` or `Logger`.
- Inside Bruinet: six fixture bases, six context option types, four file templates and the samples' three files.

## Why this and not the alternatives

Keeping the file types and documenting the precedence was the cheaper choice, but it keeps two homes for every setting and the hand copy that already lost WPF's timeouts.
Binding through Microsoft.Extensions.Configuration was the other: it would bring sections, environment variables and layering, and a dependency every test project inherits; Bruinet's `TestSettings` already covers the suites' own values.

## Unknown

- Whether projects other than Tomorrow keep `bruinet.{x}.config.json` files (Q6); the change list's row gives the before and after of each.
- Whether the Stride and WinForms fixtures read anything from their files that their context options lack; checked by the build when the copies are removed.

## How it is made

One commit per rule, 6 and 1 first (they remove and share), then 4, then 2 and 3 together per stack, the samples' files with them.
Each commit with its change-list row; Tomorrow's two suites build in a worktree before it is pushed.

Next action: the owner approves the rules (all, or by number).
