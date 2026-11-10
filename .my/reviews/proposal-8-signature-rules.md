# Proposal 8: the signature rules

Drafted 2026-11-10 at `08834828`, for the owner to approve rule by rule. Nothing here is made yet.

## Outcome

Eight rules make every control and exception read the same way on all six stacks.
Most are cheap: only two touch Tomorrow, and one of those (`IsExists()` to `Exists()`) is 61 mechanical edits there and 68 in Bruinet.
Two of the review's points turn out to be rules already, not faults: `Is…()` reads once by design, and `expected: null` skips on purpose.

## The rules

| # | Rule | What changes, and who pays |
| --- | --- | --- |
| 1 | `Get…` waits for its control and takes `int? timeoutMs = null`; `Is…` and `TryGet…` read once and take none. | Core's `GetDate`, `GetTime`, `GetProgress` gain the parameter; every stack already has it. Custom implementers only. |
| 2 | A text control declares `string? GetText(int? timeoutMs = null)` on `ITextControlObject`. | Added to the interface; every stack's base has it. Custom implementers only. |
| 3 | `expected` is `bool?`, defaults to `true` on interface and class alike, and the interface says `null` skips the check. | Defaults and doc comments, through the `.tpl.cs` generators. No caller changes. |
| 4 | `IsExists()` is `Exists()`, beside `WaitExists` and `AssertExists`. | 68 calls in Bruinet, 61 in Tomorrow; one regex row. |
| 5 | Every capability interface ends in `Object`: `IPressableControl<T>` is `IPressableControlObject<T>`. | 2 uses in Bruinet, none in Tomorrow. |
| 6 | An element exception carries one `Locator? Locator`; `LocatorInfo`, `LocatorString` and `ControlLocator` go. | `ElementNotFoundException`, `AssertionException`; 2 uses in Bruinet, none in Tomorrow. |
| 7 | No Bruinet name shadows a test framework's or the runtime's: `AssertionException` is `ControlAssertionException`, `Comparison.Equals` is `Comparison.EqualTo`. | 25 files throw or catch the exception; 46 `Comparison.Equals` in `.tpl.cs` attributes; none in Tomorrow. |
| 8 | Every `LocatorStrategy` has its `Locator.By…` factory. | `ByDataAutomationId` and `ByControlTypeAndName` added; nothing breaks. |

## Why these and not others

Rule 1 keeps R7, which is right: a read that waits must say how long, and a read that does not wait must not pretend to.
Rule 3 keeps `null` because the step binder turns an empty table cell into `null`, so a data-driven step can leave a check out; removing it would break that documented use.
Rule 4 takes the name the `Wait` and `Assert` verbs already use; `IsPresent()` was the alternative and would make three names for one check.
Rule 7 renames rather than aliases: an NUnit consumer with both namespaces imported cannot write `AssertionException` at all today.

## Unknown

- Html's date and time inputs return `DateOnly` and `TimeOnly` and do not implement `IDateControlObject` or `ITimeControlObject`. My proposal: they implement both, converting, and keep their own typed `GetDate`; it is a separate commit under rule 1.
- Whether `AssertionException.ControlLocator` is ever set where only a string is known; if so, rule 6 keeps a `Locator?` that is null there.
- Projects other than Tomorrow that use Bruinet (the upgrade plan's Q6) were not checked.

## How it is made

One commit per rule, each with its change-list row, in the order 8, 2, 1, 3, 5, 6, 7, 4: additive first, Tomorrow's rename last.
Rules 1 to 3 go through the `.tpl.cs` generators, so all six stacks change in one regeneration; `GeneratedFilesTests` proves the `.gen.cs` files match.
Each commit builds Tomorrow's two suites in a worktree before it is pushed, as proposals 1 to 9 did.

Next action: the owner approves the rules (all, or by number), then rule 8 goes first.
