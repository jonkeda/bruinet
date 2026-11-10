# Proposal 5: the shared bases, Core half

Drafted 2026-11-10 at `bc1be389`, for the owner to approve rule by rule. Nothing here is made yet.

## Outcome

Five stacks (Html, WPF, WinForms, Android, Stride) carry the same object, page-root, row, collection and container bases, copied: once the stack's name is taken out, WPF's and Stride's `ObjectBase` differ in no line, and WPF's and WinForms' collection bases in none.
About 450 to 500 lines of copies can become about 200 in Core, with each stack keeping a thin class of the same name, so a consumer's page objects and rows do not change.
MAUI stays out: its collection is a different algorithm (row strategies, three kinds of key, grouped lists) that shares only member names, and its object base is eight lines.
The order is fixed, because each step's base derives from the one before.

## The rules

| # | Rule | What moves |
| --- | --- | --- |
| 1 | `ObjectBase<TContext>` in Core: the context, budgets, scope name, `RunProbe` and `RunFirst`. | 40 lines from each of five stacks; each keeps `ObjectBase : ObjectBase<I{X}TestContext>`. `RunProbe` and `RunFirst` become `protected`, so they enter the declared API. |
| 2 | `IElementScope<TElement>` in Core: `TryFindElement`, `FindElement`, `FindElements`; each stack's element scope extends it. | Lets Core code look things up without a hook per stack. |
| 3 | `IItemRootSource<TElement>` in Core: `TryGetItemRoot(ItemKey)`; each stack's collection interface extends it. | Five five-line interfaces shrink to their declarations. |
| 4 | `AppRootBase` and `ItemObjectBase` in Core for the five, on rules 1 to 3. | About 18 and 36 lines a stack; each keeps its sealed `AppRoot` and its `ItemObjectBase<TSelf>`. |
| 5 | The container and collection bases in Core, the container first. | About 150 lines of container and 90 of collection a stack; see the choice below. |
| 6 | MAUI keeps its own bases, and the reason is written on them. | Nothing moves. |

## The choice in rule 5

The collection base derives from each stack's container base, so a Core collection base needs the container base in Core first; C# gives one base class.

- **A, my proposal: move the container base, then the collection base.** The container bases are near copies too (about 150 lines each); with both in Core, each stack keeps about 20 lines. It needs a Core parent-scope constraint in place of `IXScope<TParent>`, which is the real design work of this proposal.
- **B: a `CollectionCalls<TElement, TItem>` helper**, as `ControlCalls<TElement>` already is: the logic moves, each stack keeps its public members as one-line forwards, about 45 lines a stack. Smaller and safer, but the container copies stay.

The hooks either way: the rows (`Children()` on Stride), reading a row's key (the stack's `ItemKeys.Of`), one scroll step (`ScrollPage` on WPF and WinForms, `Scroll` on Android, none on Html and Stride), and two optional fast paths (Html's lookup by `data-item-key`, Android's `UiScrollable` sweep).

## Who pays

- A project's page objects, containers, collections and rows: nothing, since every class they derive from keeps its name and namespace.
- A project that calls `RunProbe` or `RunFirst`: none can today (they are `private protected`); after rule 1 a derived class can.
- A project with a stack of its own built on these bases: it derives from the Core bases instead of copying them, which is the point.
- Inside Bruinet: five stacks per rule, their API files, and the unit tests that pin the bases, which should pass unchanged.

## Unknown

- Whether the Core parent-scope constraint of option A fits Html's `ContainerBase`, which also exposes `ContainerRoot` and a CSS fast path; found when it is written.
- How much of MAUI's `RootedScopeBase` could later share rule 1's base; left out here on purpose.

## How it is made

One commit per rule in order 1, 2, 3, 4, then rule 5 as two commits (container, then collection) under A or one under B, each with its change-list row and a run of the five stacks' unit tests and Tomorrow's two suites in a worktree.

Next action: the owner approves rules 1 to 4 and 6, and picks A or B for rule 5.
