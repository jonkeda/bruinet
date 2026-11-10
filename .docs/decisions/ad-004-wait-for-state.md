# AD-004: Wait For State

Do not fix tests by adding arbitrary sleeps or longer delays. Wait for concrete
UI state, navigation completion, busy sentinel changes, text, visibility,
enabled state, request observation, or another observable condition.

The framework does the waiting, and does it one way, on every stack (MAUI first, 2026-09-19; the rules are the
[call model](../contracts/call-model.md)):

- **One unit per public call.** A call on a control, page, container, collection or
  row is one log entry/exit pair and one failure, with the action inside it.
- **Only the call's poll waits.** Readiness, lookup, visibility, scrolling and
  settling are single attempts inside that poll. Nothing below it sleeps or loops on
  its own, and the caller's `timeoutMs` is the whole budget.
- **A call waits for its scope chain.** Each attempt first asks the scope the control
  stands in, which asks its parent: a row, its collection, the page. A dialog or popup
  shown over a page answers for itself.
- **Every attempt finds its element again.** A replaced element is a signal
  (`StaleElementException`), never a reason to wait on the old one.
- **Actions, `Get…`, `Assert…` and `Wait…` wait; `Is…` and `Try*` members answer about now.**
- **Inside a Core method, wait for an action's effect with `Confirm`**, which never
  repeats the action.

A test that needs more time than the default passes its own `timeoutMs`. It does not
add a delay.

**Broken when:** a `Task.Delay`/`Thread.Sleep` is added to make a test pass, or a
wait loop is nested below a call's poll. See [call-model](../contracts/call-model.md).
