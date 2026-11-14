# Frainet

Frainet is for running software as a dark factory: agents write, test and review the code, people
design the line and judge what comes out. Its own part is the specs and the checks on them. A
loop can only run dark when its spec is exact and something other than a person tells it whether
the work meets it. Frainet lives in Bruinet's repository for now and may become its own later.

## What it does

- A spec format in small layers: intent, rules, examples, acceptance, each line with an id.
- Checks on a spec before anything is built: is every rule there, exact, and given an example.
- Checks on the work against the spec: every spec id reached by a test, every test pointing back.
- Bruinet runs the tests; Frainet says which spec lines they prove and which they leave open.

## The idea behind it

Every loop turns plan, build, deploy and feedback, and the build of one loop is many turns of the
loop inside it: tasks in a capability, capabilities in a module. A loop goes dark when its
feedback is an exact check. The specs loop runs across all of them, feeding each plan, so the
spec is where the factory's light stays on longest. The loops are written out in
[.my/loops/](.my/loops/02_loops_generic.md); the research behind the specs is in [.my/specs/](.my/specs/).

## Commands

```bash
dotnet run --project src/Frainet.Cli -- check specs
```

```bash
dotnet run --project src/Frainet.Cli -- trace specs/todo/TOD.02.spec.md ../samples/Todo/tests
```

```bash
dotnet test ../samples/Todo/tests --logger trx --results-directory TestResults/todo
dotnet run --project src/Frainet.Cli -- trace specs/todo/TOD.02.spec.md ../samples/Todo/tests --results TestResults/todo
```

```bash
dotnet test Frainet.slnx
```

The first checks every spec against the [format](docs/spec-format.md) and says what breaks it,
line by line. The second lists each rule of a spec with the tests that reach it, and fails on a
rule no test reaches. With `--results` (the `.trx` files of a run, or their folder) it gives each
rule a state, as F1 asks: not reached, not run, failing, or passing, and fails unless every rule
passes; a C# test is matched by its class and method, a scenario by its name. The last runs the
checks on the specs and on broken copies.

## How it grows

It starts very small, on Bruinet's Todo sample, which already has journeys, tiers and a coverage
report. Once Todo's spec and its checks are understood, Frainet moves to Tomorrow, one module at a
time. Nothing is built ahead of a sample that needs it. The plan is [.my/plan.md](.my/plan.md).
