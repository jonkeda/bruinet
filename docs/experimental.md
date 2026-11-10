# Experimental APIs

Some of Bruinet's public types are public only so Bruinet's own packages can share them. They are
marked `[Experimental]`, and using one from your project is a build error with the id below until you
accept it.

## BRUINET010

`Bruinet.Bdd`'s engine and runner protocol: the parser (`BddMarkdownParser`), the binder and its
catalogs, the callables, the config reader and writer, the scenario files and runner, and every
`Bruinet.Bdd.Runner` type. Uat, the `bruinet` command line, Presenter, Workspace and Narrator are
built on them. They change whenever those packages need them to, without a major version.

What is not marked is promised: the step-definition attributes and `BddStepDefinitionsBase`, the
hook attributes, argument converters, `BddTable`, `IBddClock`, `BddExecutionContext`, and the
document types a fixture sees (`BddDocument`, `BddScenario`, `BddStep`, `BddBoundScenario`,
`BddTagConventions`, `BddStepResult`).

If your project needs an engine type, say why in the project file and accept the risk there:

```xml
<!-- Reads feature files to list them per collection; BddMarkdownParser is Bruinet's engine. -->
<NoWarn>$(NoWarn);BRUINET010</NoWarn>
```

The [api-upgrade change list](../.github/skills/api-upgrade/references/changes.md) still gets a row
when one of these types changes, so a project that accepted the risk can follow.
