# Bruinet.Spec

The Spec module of Bruinet: reads spec files (`*.spec.md`), checks them against the format, and traces every rule of a
spec to the tests that reach it and the results that prove it. The format is described in
[docs/spec/spec-format.md](../../docs/spec/spec-format.md).

It references no other Bruinet package: a team that writes specs and tests with plain xUnit takes this package alone. It
reads tests and results as files, so the tests may be written with any tool.

## Commands

From the repository root:

```bash
bruinet spec check samples/Todo/specs
bruinet spec trace samples/Todo/specs/TOD.02.spec.md samples/Todo/tests
dotnet test samples/Todo/tests --logger trx --results-directory TestResults/todo
bruinet spec trace samples/Todo/specs/TOD.02.spec.md samples/Todo/tests --results TestResults/todo
```

`check` says, line by line, what breaks the format in every spec of a folder. `trace` lists each rule with the tests
that reach it and fails on a rule no test reaches; `--runnable` counts the spec's own examples as the tests of their
rules, for a suite that runs its specs. With `--results` (a run's `.trx` files, or their folder) each rule gets a state:
not reached, not run, failing or passing, and the trace fails unless every rule passes. A C# test is matched by its
class and method, a scenario by its name.

A product's specs live with the product (`samples/Todo/specs/`, a suite's `Modules/<Module>/Specs/`), not here.
