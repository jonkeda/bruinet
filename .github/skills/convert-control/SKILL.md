---
name: convert-control
description: Convert a hand-written Bruinet MAUI control object (e.g. ImageButton.cs, Entry.cs, Label.cs) to the generatable .tpl.cs / .gen.cs format used under src/Bruinet.Maui*/Controls. Use when asked to "convert", "migrate", or "update to the new generatable format" a MAUI control object, or to make a control work with the generator (bruinet controls generate).
---

# Convert a control object to the generatable format

Turn a hand-written control (`Foo.cs`) into a template (`Foo.tpl.cs`) that holds constructors,
`*Core` methods and any members that cannot be generated, plus a generated partial
(`Foo.gen.cs`) holding the public API.

**The contract** - what the generator matches, what it emits, shortcuts, attributes, and what
fails generation - is in
[../maui-control/references/generator-contract.md](../maui-control/references/generator-contract.md).
Read it first. The rules every control follows (semantics, routes, one unit of work per public
call) are in [../maui-control/SKILL.md](../maui-control/SKILL.md). This skill is only the
conversion procedure.

## Steps

1. **Read the control and its base class.** Know which members are inherited before deciding
   what to convert. Base templates live in `src/Bruinet.Maui/Controls/Base/`.

2. **Rename to `.tpl.cs`** (`git mv`, to keep history) and make the class `partial`.

3. **Reparent to the Base hierarchy** if it still points at an old base: `Base.ViewBase<TScope>`,
   `Base.FocusableControlBase<TScope>`, `Base.ClickableControlBase<TScope>`, or the capability
   base that fits (see the `maui-control` skill's simple-control reference). Constructors are
   `public` on a concrete control, `protected` on an abstract base.

4. **Sweep every existing `*Core` method against the contract** before converting anything:
   `protected virtual`, element first. A near-miss now **fails generation** with an error naming
   the method, so nothing is lost silently; but fix them up front rather than one error at a time.

   ```csharp
   // before - fails generation: missing 'virtual'
   protected string? GetPlaceholderCore(IMauiElement? element)

   // after - generates GetPlaceholder / WaitPlaceholder / AssertPlaceholder
   protected virtual string? GetPlaceholderCore(IMauiElement? element)
   ```

   A method that is deliberately not public API gets `[SkipGeneration("reason")]`. Where a Core
   method needs element state but takes no element, add the parameter and read from it instead
   of re-finding the element.

5. **For each public method, pick one:**
   - Backed by a Core method -> delete the public method; make the Core `protected virtual`;
     add `int? timeoutMs = null` if the public signature had it.
   - Hand-written with no Core -> extract the body into a new `protected virtual *Core` that
     follows the naming rules.
   - Not expressible (below) -> keep it hand-written in the template.

6. **Fold guard calls into the Core bodies.** The generator injects no guard: emitted bodies are
   exactly `RunDoWithElement(element => { XCore(element); })`. Any `EnsureClickableCore` /
   `EnsureEnabledCore` call that lived in a hand-written wrapper moves inside the Core method.
   Guards are named `Ensure*`, stay `protected virtual`, and generate nothing.

7. **Remove nested units of work.** A Core method that calls a `Run*` helper or a public member
   of another control makes the generator warn. Replace `Run*` waits with
   `Confirm(read, done, timeoutMs)`; move part behaviour to the part (see the
   component reference).

8. **Generate and build** (from the Bruinet root; for an Html or Blazor control run
   `tools\Scripts\CreateHtml.Bat` instead):
   ```
   tools\Scripts\CreateMaui.Bat
   dotnet build Bruinet.sln -v:minimal /nr:false
   ```
   `CreateMaui.Bat` rebuilds the generator and regenerates every `.tpl.cs` under the `Controls`
   folders of `Bruinet.Maui` and `Bruinet.Maui.CommunityToolkit`. For a file elsewhere, run the
   CLI directly (see the contract).

9. **Diff the public surface against the original.** The generated API must match what the old
   class exposed: same names, parameters and return types.
   ```
   grep -E "public " Foo.gen.cs
   ```
   Extra members and missing members both show up here.

## Limits - do not try to generate these

- **Overloads that differ only by parameters** collide on the generated name; generation fails
  with a clear error. Rename one Core method, or keep one member hand-written.
- **Bespoke assertion logic** beyond the `[GenerateComparisons]` variants. Generated `Assert*`
  synthesizes a message naming the property and locator (the caller's `message` still wins). If
  an assert needs more, keep it hand-written and mark its Core `[SkipGeneration("reason")]`.

When you hit a limit, leave the member hand-written and say so. Do not drop it or change its
behaviour.

## Report

Say which members were generated, which stayed hand-written and why, and any public API that
changed shape. Then run the control's tests: `Bruinet.Maui.Tests` for unit tests, and tier 1 of
the UI tests (`--filter "Control=Foo"`) after rebuilding the sample app.
