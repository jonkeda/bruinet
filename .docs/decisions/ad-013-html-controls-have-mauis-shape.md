# AD-013: Html Controls Have MAUI's Shape

Every stack's control objects are made the way MAUI's are: a `.tpl.cs` template with `*Core`
methods, and a `.gen.cs` that `bruinet controls generate` writes from it. Each stack's `ViewBase` has
MAUI's `Run*` helpers, name for name and signature for signature (R8), so one generator serves them
all. The capability bases implement the same `Bruinet.Core` control interfaces, so a member a MAUI
test or a UAT phrase uses has the same name, parameters and meaning on every stack. Every member is
one synchronous call.

| Stack | Script in `tools/Scripts/` | Its own |
| --- | --- | --- |
| Html, Blazor | `CreateHtml.Bat` | the `Control` suffix (`ButtonControl`), kept by the owner 2026-11-10 |
| Native Android (2026-10-07) | `CreateNativeAndroid.Bat` | Android's widget names (`EditText`, `Spinner`) |
| WPF (2026-11-10) | `CreateWpf.Bat` | WPF's names (`TextBox`, `PasswordBox`, `TreeViewItem`); a Core method acts through a UI Automation pattern of the shared `UiaElementBase` (`Bruinet.FlaUI`) |
| WinForms (2026-11-10) | `CreateWinForms.Bat` | its names (`TrackBar`, `NumericUpDown`, `DateTimePicker`, `TabPage`, `TreeNode`); the same patterns |
| Stride (2026-11-10) | `CreateStride.Bat` | a Core method is one command to the game over the automation pipe |

A control *type* carries its platform's name: a type names what the app is made of, while the
members and UAT verbs stay the same. UAT finds a control by Core's `IElementObject<>` as well as by
name, so a name need not end in a known suffix.

**Why:** hand-written Html controls approximated MAUI's surface by hand and drifted
(`AssertTextContaining` beside `AssertTextContains`, missing `Enter` and `AssertSelectedText`, so
UAT phrases failed on Html). The async members wrapped the synchronous calls and added nothing.
Evidence: [review-html-vs-maui.md at 7162ac7](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/html/review-html-vs-maui.md), sections 1.3, 2 and 4.

**Ruled out:** hand-written public control members where a `*Core` method can generate them;
`Run*` helpers whose names or signatures differ from MAUI's; async control, page or container
members (`*Async` wrappers, async interfaces); a public member whose name differs from MAUI's for
the same behaviour.

**Broken when:** an Html, Blazor, native Android, WPF, WinForms or Stride control with `*Core` methods is a plain `.cs`,
or its `.gen.cs` differs from what its `Create*.Bat` writes; the generator needs a stack-specific
branch; a `Task`-returning public member appears on a control, page or container; a UAT phrase
fails on Html, native Android, WPF, WinForms or Stride with "available methods" for a method MAUI has.
