# The generator contract

A ControlObject is a template, `Foo.tpl.cs`, holding constructors, `*Core` methods,
`*Shortcut` methods and any hand-written members. `bruinet controls generate` (in
`tools/Bruinet.Cli`) reads it and writes the public API into `Foo.gen.cs`, a second `partial` of the same class. The template holds
the "how"; the generated file holds the "what".

This file is the single statement of the contract. The `maui-control`, `convert-control`
and `control-source-builder` instructions all point here.

## Generate

```
tools\Scripts\CreateMaui.Bat
```

Run from the Bruinet root. It rebuilds the generator CLI, then regenerates every `.tpl.cs`
under `src\Bruinet.Maui\Controls` and `src\Bruinet.Maui.CommunityToolkit\Controls`,
recursively. `tools\Scripts\CreateHtml.Bat` does the same for `src\Bruinet.Html\Controls`
and `src\Bruinet.Blazor\Controls`: the contract below holds for Html controls unchanged, with
`IHtmlElement` as the element type. For one file or folder elsewhere, run `bruinet controls generate` directly:

```
pwsh tools\Scripts\bruinet.ps1 controls generate <file-or-folder> [<file-or-folder>...]
```

Output is always `<name>.gen.cs` beside the input.

`CreateMaui.Bat` rewrites every `.gen.cs`, and about 70 whose content did not change still show
as modified because of line endings. `git diff --ignore-cr-at-eol --name-only` lists only the
files with a real change; restore the rest (Git Bash, from the Bruinet root):

```
comm -23 <(git diff --name-only -- '*.gen.cs' | sort) <(git diff --ignore-cr-at-eol --name-only -- '*.gen.cs' | sort) | xargs -r git checkout --
```

Then `git status` shows only the `.gen.cs` files you meant to change.

- Never edit a `.gen.cs` by hand. Never commit a `.tpl.cs` change without regenerating.
- **R5.** A class with Core methods or shortcuts is a `.tpl.cs`; a class with neither is a
  plain `.cs` (a page, an app container with only named children, a control that only inherits).
  This holds for MAUI, Html and Android alike. The templates with an empty `.gen.cs` that exist
  today (`Border.tpl.cs`, `Button.tpl.cs`, `SeekBar.tpl.cs` and others) predate R5: debt, not a model.
- A `.tpl.cs` class must be `partial`.

## What is matched

Generators run in this order; the first that matches a method claims it.

| Order | Generator | Matches | Emits |
| --- | --- | --- | --- |
| 1 | `ShortcutGenerator` | `*Shortcut` methods (see Shortcuts) | the part's members, forwarded |
| 2 | `IsWaitAssertGenerator` | `Is*Core` returning `bool?`/`bool`; `Get*Core` returning a value | `IsX`/`WaitX`/`AssertX`; `GetX`/`WaitX`/`AssertX` |
| 3 | `SetGenerator` | `Set*Core` with at least one value parameter besides `timeoutMs` | `SetX(value, timeoutMs)` |
| 4 | `ActionGenerator` | any other `*Core` returning `void`, except `Ensure*` | `X(...)`, returning the fluent type |

A `*Core` method is a candidate when **all** hold:

1. The name ends in `Core`.
2. It is `protected virtual` (not `override`: the base already generated the member).
3. The first parameter is the platform element (its type name contains `Element`;
   `IMauiElement` or `IHtmlElement`, nullable or not).

**A near-miss fails generation.** A `*Core` method with the element first that lacks
`protected` or `virtual` stops the generator with an error naming the method. Fix the
method, not the generator. If the exclusion is deliberate, mark it
`[SkipGeneration("reason")]`: it stays `virtual` and overridable, and nothing is emitted.

`Ensure*` methods are guards: never emitted, never reported. Name every guard `Ensure*`.

## The emitted shapes

| Template declares | Generator emits |
| --- | --- |
| `protected virtual bool? IsCheckedCore(IMauiElement element)` | `bool? IsChecked()`, `bool WaitChecked(bool? expected = true, int? timeoutMs = null)`, `TScope AssertChecked(bool? expected = true, string? message = null, int? timeoutMs = null)` |
| `protected virtual string? GetTextCore(IMauiElement element)` | `GetText(int? timeoutMs = null)`, `WaitText(string? expected, ...)`, `AssertText(string? expected, ...)` |
| `protected virtual void SetTextCore(IMauiElement element, string? text, int? timeoutMs = null)` | `TScope SetText(string? text, int? timeoutMs = null)` |
| `protected virtual void ClickCore(IMauiElement element, int? timeoutMs = null)` | `TScope Click(int? timeoutMs = null)` |

- Extra parameters after the element are copied onto every generated signature, in order.
- **`timeoutMs` is forwarded only when the Core method declares it.** Declare
  `int? timeoutMs = null` last on every Core method whose public form takes a timeout (an
  interface signature usually requires it).
- A `Set*Core` whose only extra parameter is `timeoutMs` is an action, not a setter.
- Two Core methods that would emit the same public name (overloads differing only by
  parameters) fail generation. Rename one.
- The generated doc comments come from the Core method's `<summary>`, so write it for the
  public reader.

### The fluent return type

What actions and asserts return, resolved from the class declaration:

1. `[FluentReturn("T")]` on the class, when present.
2. `TSelf`, when the class declares it: containers and collections return themselves.
3. The class itself, when it passes itself to its base as a type argument
   (`MediaElement<TScope> : ComponentObjectBase<TScope, MediaElement<TScope>>`).
4. The single type parameter: a simple control returns its scope, `TScope`.

Generated setters on a container return the parent (`SetResult`), not `TSelf`.

### Comparison variants

A `Get*Core` emits equality only. Add `[GenerateComparisons(...)]` (from
`Bruinet.Core.Interfaces`) for more:

```csharp
[GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.Empty)]
protected virtual string? GetTextCore(IMauiElement element) => element.Text;
// AssertText, AssertTextContains, AssertTextEmpty, and the matching Wait* members
```

| Flag | Emits | For |
| --- | --- | --- |
| `EqualTo` | `AssertX`, `WaitX` | anything |
| `Contains`, `StartsWith`, `EndsWith` | `AssertXContains`, ... | strings |
| `Empty` | `AssertXEmpty` | strings, collections |
| `SequenceEquals` | element-wise equality | collections |
| `HasItem` | `AssertXHasItem(item)` | collections |
| `Count` | `AssertXCount(n)` | collections |
| `GreaterThan`, `AtLeast`, `LessThan`, `AtMost` | `AssertXGreaterThan(n)`, ... | numbers, dates; a null actual never passes |

### Absence

`[AbsenceTolerant]` on an `Is*Core` or `Get*Core` whose element parameter is nullable
means "this question has an answer when the element is missing":

- an `Is*` / `Wait*` / `Assert*` resolves the element optionally and passes null to the
  Core method, instead of failing with `ElementNotFoundException`;
- a `Get*` reads once and returns the Core method's answer, instead of waiting for the
  element.

Use it for existence-like reads (`IsOpen`, `IsShown`, `IsShowing`) and reads where "none"
is a valid state. Plain `IsVisible` is not absence tolerant: it answers false for a missing
element, but its `Wait` and `Assert` find the element first, so `AssertVisible(false)` never
passes on a missing one. A control that must
answer "not shown" declares its own absence-tolerant read.

## Inside a Core method

- The element parameter is already resolved and ready. Read and act on **that element
  only**.
- Guards are `Ensure*Core` calls inside the Core body, **before** the action. No guard is
  injected by the generator. Readiness that should be *waited for* (enabled a moment late)
  goes in `EnsureReadyForActionCore`.
- `ElementNotReadyException` from a Core method means "I did not act": the generated action
  resolves again and asks again within its budget. So throw it only before acting - never
  after the element was touched - and let any other failure propagate, which ends the call
  at once (AD-009).
- To wait for an effect, use `Confirm(read, done, timeoutMs)`, and when it is not
  confirmed throw `confirmation.Failure(Locator, "<action>", lastError => new ...)`. It never
  repeats the action, and reports a replaced element as `StaleElementException`. Never a
  `Run*` helper, never `Thread.Sleep`.
- Anything else below the call that takes a time limit - `element.ScrollIntoView(timeoutMs)`,
  `ScrollHelper.ScrollIntoView(element, timeoutMs)` - gets `CallRemainingMs`: what is left of the
  call's budget. Never a fixed number, so a call never outlasts its `timeoutMs`.
- Never call a public member of another control (a part, `Button(id)`, `Child<T>(id)`, an
  item): that public member is a whole unit of work of its own (readiness, poll, log entry,
  timeout), nested inside the one the generated wrapper runs.

The generator **warns** (it does not fail) when a Core method calls a `Run*` helper or a
public member of a part or child factory. Treat the warning as an error.

## Shortcuts (components)

A component exposes a part's member through a one-line shortcut:

```csharp
protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();
```

A method ending in `Shortcut` must be:

1. `protected`, and **not** `virtual`: it is a declaration, not behaviour to override;
2. expression-bodied;
3. exactly one call, `<Part>.<Member>(<arguments>)`, where `<Part>` is a property or field
   of the class.

A malformed shortcut fails generation. The public name drops `Shortcut`:

| Shortcut | Emits |
| --- | --- |
| `Is*Shortcut` returning `bool?` | `IsX`, `WaitX`, `AssertX`, each a single call on the part's own `Is`/`Wait`/`Assert` members |
| `Get*Shortcut` returning a value | `GetX`, `WaitX`, `AssertX`, forwarded the same way; `[GenerateComparisons]` on the shortcut forwards those variants too |
| anything else | the shortcut itself, made public under the new name (`PauseShortcut(int? timeoutMs = null)` -> `Pause(int? timeoutMs = null)`) |

The part's type is not inspected. If the part lacks the forwarded member, the generated
code does not compile and the error names it. The forwarded `Assert*` returns the
component, which compiles only when the part is scoped to the component (`new(this, id)`).

## Hand-written members

Keep a member hand-written, in the template, when:

- it spans two parts or a child and the root (plain calls in sequence, no `Run*` wrapper);
- it needs assertion logic beyond the comparison variants;
- it is a domain alias for a generated member (`TurnOn()` => `SetChecked(true)`).

Mark a Core method `[SkipGeneration("reason")]` when its public member is hand-written.

## After generating

1. Read the CLI output: near-miss errors stop generation; nested-unit-of-work warnings do
   not, but must be fixed.
2. Diff `Foo.gen.cs` against the public API you intended. A quick look:
   ```
   grep -E "public " Foo.gen.cs
   ```
   Every member you meant should be there, nothing extra, each returning the type you meant.
3. Build the project, then the solution: controls are referenced widely.
   ```
   dotnet build Bruinet.sln -v:minimal /nr:false
   ```
