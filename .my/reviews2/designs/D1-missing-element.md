# D1. A missing element: null or false

Design for R2-T39 (review 04 P8, review 08 P5). On `reviews` after the rebase onto `origin/move` at `930610b1`.

**Approved by the owner as proposed, 2026-11-11.**

## Decision asked

What `Is…()` answers when its element is not there. **My proposal: false, and the members become `bool`.**
The owner says yes, or picks B, before R2-T39 starts.

## What holds today

- Core's interfaces declare ten members `bool?` and document "null if element not found": `IsVisible`, `IsEnabled` (`IElementObject.cs:19-27`), `IsClickable`, `IsReadOnly`, `IsExpanded`, `IsFocused`, `IsIndeterminate`, `IsRefreshing`, `IsSelected`, `IsChecked`.
- The generator writes every `Is…()` as `return …Core(TryFindElement()) == true;` (`tools/Bruinet.Cli/Generate/Generators/IsWaitAssertGenerator.cs:574`). So all ten answer false for a missing element, on all six stacks, while their type says null is possible.
- The contract asks null for `IsVisible` and `IsEnabled` (`tests/Bruinet.ControlContracts/ElementContract.cs:37`, `:45`) and null for `GetAttribute` (`:109`). Html skips all three with a stale reason (`tests/Bruinet.ControlContracts.Html/HtmlBindings.cs:74-85`); no other stack binds them.
- `Wait…` and `Assert…` are not in question: they resolve the element first and fail by name when it is gone, except `[AbsenceTolerant]` ones (`AbsenceTolerantAttribute.cs`), which ask about presence and already handle absence.
- Consumers: no code in tomorrow, kofflard or construction compares an `Is…()` to null or reads `.Value`/`.HasValue`; construction has two `!= true`/`SpinUntil` uses on `IsVisible()` that compile and mean the same under both options.

## Options

| | A. False, `bool` | B. Null, as documented |
| --- | --- | --- |
| Generator | Unchanged, return type written `bool` | `TryFindElement() is { } e ? …Core(e) : null` |
| Interfaces | Ten members `bool`, docs say false | Unchanged |
| Contract | `Assert.False(Missing.IsVisible())`, `IsEnabled` | Unchanged |
| A consumer's `Assert.False(x.IsVisible())` on a gone element | Passes, as today | Fails (xUnit's `Assert.False(bool?)` fails on null) |
| A caller's code | One answer to handle | Three states everywhere, mostly `== true` |

Why A: "is it visible" has a plain answer when it is not there, the stacks and the consumers already rely on it, and the type then stops promising a null nobody returns. Absence stays askable through `Exists()` and `AssertExists(false)`.

What A gives up: telling "missing" from "present and hidden" in one call. A test that cares calls `Exists()` first, which is what the contract's own `Missing.Exists()` row shows.

`GetAttribute` on a missing element is separate: today Html and Maui throw `ElementNotFoundException`, the contract says null. Under A it keeps its `Get…` rule: a value read of a missing element throws, as every other `Get…` does. The contract's row changes to `Fails(…)`.

## Work under A (R2-T39)

1. `IsWaitAssertGenerator.GenerateIsMethod`: write `bool` as the return type when the Core method returns `bool?`; the body stays.
2. The ten interface members in `src/Bruinet.Core/Interfaces/` become `bool`; their summaries say "false when the element is not found".
3. `PublicAPI.Unshipped.txt` in Core and every stack: the `bool?` lines become `bool` (`tools/Scripts/update-public-api.ps1`).
4. Regenerate every stack (`bruinet.ps1 controls generate`, the six `Create….Bat`); commit only the `.gen.cs` that changed in content.
5. `ElementContract`: `Assert.False` for `IsVisible` and `IsEnabled` on `Missing`; `GetAttribute` on `Missing` fails. Delete the three skips and `MissingDiffers` in `HtmlBindings.cs`.
6. `.github/skills/api-upgrade/references/changes.md`: one Made row (members are `bool`; code that read `.Value` drops it).
7. Docs that say "null if not found" for these members: `docs/controls/`, `docs/guides/test-writing.md` (search `null if`).
8. Check: Core, Html, bUnit and MauiHeadless unit and contract tests; Html contracts on both heads. Desktop and Android runs come with R2-T48, R2-T49.

## Under B instead

Steps 1 (null body), 5 unchanged contract, 3 none, plus every stack's `IsVisibleCore` etc. checked for a null element answer, and each consumer searched for `Assert.False(…Is…())` on a control that may be gone (none found today, but the answer flips silently).
