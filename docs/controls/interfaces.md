# Core Interfaces

Core interfaces define the contracts that page objects, controls, contexts, and
drivers use across platforms.

## Primary Concepts

| Concept | Responsibility |
| --- | --- |
| Control object | Represents one UI element or logical control |
| Page object | Represents one page, view, screen, or dialog |
| Locator | Describes how to find a UI element |
| Test context | Owns driver/session services for a test |
| Driver | Platform-specific element discovery and action backend |
| Artifact service | Screenshots, manifests, summaries, and diagnostics |

## MAUI Rooted Scopes

MAUI pages and container objects share rooted-scope behavior without sharing a
parent contract:

- `RootedScopeBase<TSelf, TSetResult>` owns root caching, stale recovery,
  strict descendant lookup, typed child factories, state, and readiness:
  `ProbeReadiness()` asks the parent (unless `AsksParent` is false), then checks
  the root, then `ProbeContentReadiness(root)`.
- `PageObjectBase<TSelf>` locates its root from the driver and has no `Parent`.
- `ContainerObjectBase<TParent, TSelf>` locates its root inside its parent and
  implements `IMauiContainerObject<TParent, TSelf>` with an explicit `Parent`.
- `AppRoot` is the one whole-app scope, for application or platform chrome
  outside every page (toolbar items, menus, Shell's flyout). Ordinary controls
  do not fall back to it.

Rooted scopes must provide a root locator and root-resolution implementation.
A page also validates cached roots through visible, usable bounds so navigating
away and back can replace a detached page instance.

## Interface Rules

- Add shared capability contracts to `Bruinet.Core`.
- Keep platform element types out of core interfaces.
- Prefer small capability interfaces over one large control API.
- Keep nullable and timeout behavior explicit.
- Add tests for new contracts and default behavior.
- Give a method that a UAT scenario should be able to say a `[BddWhen]` or `[BddThen]` phrase.
  That attribute is the whole registration - the engine discovers it, and there
  is no catalog to edit
  ([AD-011](../../.docs/decisions/ad-011-uat-vocabulary-is-attribute-declared.md)).

## Naming

- Interfaces use `I...`.
- Capability interfaces describe what a control can do.
- Platform implementations keep the platform name in the project, not in the
  core interface name.

## Implementation Checklist

When adding a new control capability:

1. Add or update the core interface.
2. Implement the capability in the relevant platform controls.
3. Add unit tests for shared behavior.
4. Add UI tests only if live driver behavior needs coverage.
5. Add a `[BddWhen]` or `[BddThen]` phrase if the capability belongs in the UAT vocabulary, and
   list it in [UAT Phrases And Flows](../guides/step-definitions.md).
6. Update [Control Object Index](index.md).
