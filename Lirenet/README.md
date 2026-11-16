# Lirenet

Lirenet is the spec tool: it holds the format a spec is written in, checks a spec against that
format without a model, and traces a spec's rules to the tests that prove them. It is a tool of
its own, for a team that writes software by hand as much as for one with agents, and with Bruinet
or with another test tool: it reads tests and results as files. Lirenet lives in Bruinet's
repository for now and may become its own later.

It came out of Frainet, where the spec code started. Frainet is the factory that runs the loops;
Lirenet is what tells a loop, exactly, whether a spec is sound and whether the work meets it.

## What it does

- A spec format in small layers: intent, rules, examples, each rule with an id
  ([docs/spec-format.md](docs/spec-format.md)).
- `lirenet check`: is every rule there, exact, and given an example. Each finding has a code and a line.
- `lirenet trace`: is every rule reached by a test, and with a run's results, did every one of them pass.

## Commands

From this folder:

```bash
dotnet run --project src/Lirenet.Cli -- check ../samples/Todo/specs
```

```bash
dotnet run --project src/Lirenet.Cli -- trace ../samples/Todo/specs/TOD.02.spec.md ../samples/Todo/tests
```

```bash
dotnet test ../samples/Todo/tests --logger trx --results-directory TestResults/todo
dotnet run --project src/Lirenet.Cli -- trace ../samples/Todo/specs/TOD.02.spec.md ../samples/Todo/tests --results TestResults/todo
```

```bash
dotnet test Lirenet.slnx
```

The first checks every spec in a folder against the format and says what breaks it, line by
line. The second lists each rule of a spec with the tests that reach it, and fails on a rule no
test reaches; `--runnable` counts the spec's own examples as the tests of their rules, for a suite
that runs its specs. With `--results` (the `.trx` files of a run, or their folder) each rule gets
a state: not reached, not run, failing, or passing, and the trace fails unless every rule passes.
A C# test is matched by its class and method, a scenario by its name. The last runs Lirenet's own
tests: the checks on the Todo sample's specs and on broken copies of them.

## Where specs live

A product's specs are in the product's own folder, not in Lirenet's: the Todo sample's in
`samples/Todo/specs/`, Bouw7's in its `specs/`. Lirenet's tests read the Todo sample's.

## What it depends on

Nothing: not on Bruinet, not on Frainet. No project reference crosses this folder, so it can move
to a repository of its own as a copy.
