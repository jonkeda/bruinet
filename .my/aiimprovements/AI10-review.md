# AI10 review: put the app in a state

Date: 2026-11-02. Branch `ai10-states` (local, stacked on `ai11-http-replay`, not pushed: a PR waits on `move`).

## What was built

| Piece | Where | Proven by |
| --- | --- | --- |
| `[BddState("...")]`: a `Given` on a method that sets a state through test data or the service head, marked `IsState` in the catalog through both registration paths | `Bruinet.Core.Testing`, `BddPhraseDeclarations`, `BddStepDefinitionRuntime`, `BddCommandCatalog` | `McpRunTests.AScenario_StartsFromAState_ByNamingItInAGiven` |
| `list_states`: the declared states, their parameters and methods | `BddMcpServer` | `ListStates_GivesTheDeclaredStates_AndOnlyThem` |
| `set_state {state, page?, testData?}`: a Step-through of a scenario of its own (the state, the page check, the built-in `Then the run pauses here`), left paused for `step_state` and `step_screenshot` | `BddMcpServer`, `UatReflectionRuntime`, `UatSpecCommandCatalog` | `SetState_PutsTheAppInTheState_AndLeavesItPausedThere`; on Todo: from three-todos, paused on the Todo List page, its controls read |
| The guard: only text that binds to a declared state alone runs; `state`, `page`, `testData` one line with no `|` or backtick; `testData` a test set of the suite | `BddMcpServer.SetState` | `SetState_RefusesWhatCouldRunAnythingButTheState`, `SetState_OfAGivenThatIsNoState_IsRefused` |
| Two Todo states: `the app starts from the {testSet} test set`, `the server also has a todo {title}` | Todo `AppLifecyclePhrases`, `BackendPhrases` | listed and set through the MCP server on the built suite |

The plan named the tool `set_state {name, args}`; it takes the state as a `Given` says it (`{state}`), which `list_states`'
phrases fill in, plus `page` and `testData`.

## Checks

- CLI 386 (8 failing as before AI11 under `--artifacts-path`: generator and data validator), Bdd 282, Uat 251, Workspace 71.
- Todo UAT headless: 42 with the stub, 36 replayed, as before.
- Presenter: `cloud-voice-settings` fails on this machine's cloud voice setting (`Not set up.` expected); not touched by AI10.
- Workspace's preview snapshots were stale since AI11b's `@needs-stub` tags: updated (tags only).

## Agent check

Passed (`checks/AI10.md`): reading the Todo list in the three-todos state took 12 tool calls with `set_state` and 23
without, and without it the agent wrote a probe scenario into the suite. Its first try showed `set_state` without `page`
leaves nothing for `step_state`; the answer now says so. `step_state` reads no list rows: a follow-up.

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Blocker: `page` and `testData` went into the scenario unchecked (a newline added steps or metadata rows) | fixed: one line, no `|` or backtick; `testData` must be a test set; tested |
| Blocker: the runner binds the Given against every phrase; a more specific non-state Given could run | fixed: the text must bind to the state alone in the same catalog; tested |
| The state's text could carry a control character | fixed: refused |
| One shared file: a second call overwrote it under a paused Step-through; the preview's snapshot test found it | fixed: a file per call, deleted once the runner has read it; ignored in git |
| `list_states` with none was an error | fixed: an empty list |
| Only the default catalog is searched, not a head's own (`_byHead`) | recorded: a suite's head catalogs are for its other apps; the runner binds the suite's scenario with the default |
| The Step-through's result is kept for a file that is then gone | recorded |
| No Todo scenario uses the two states | recorded: a scenario would need new HTTP recordings and a journey id |
| The comment above `ListStates` | fixed |
