# BDD Glossary

Bruinet uses the words Cucumber and Reqnroll users know where the idea is the same one, and keeps
its own words where Cucumber has no equivalent.

| Bruinet | Cucumber / Reqnroll | Notes |
| --- | --- | --- |
| feature, scenario, scenario outline, background, examples, tags | the same | same idea, same word |
| step | step | a line of a scenario |
| step definition | step definition | a declared step text plus the method it runs: `[BddGiven]`, `[BddWhen]`, `[BddThen]` from `Bruinet.Bdd` (package `Bruinet.Bdd.Abstractions`) |
| step-definition class | binding class | a class with `[BddStepDefinitions]` or deriving from `BddStepDefinitionsBase`; discovered from the fixture's `TestComposition` scan |
| step text, `{placeholder}` | step text, Cucumber expression | a strict subset: no optional text, no alternatives (AD-016) |
| fixture | (context injection, `ScenarioContext`) | xUnit's word |
| callable step (`## Step:`) | (none) | a Markdown sequence of steps with parameters (AD-017) |
| control verb | (none) | a step declared on a control method (`I tap {control}`) |
| page phrase | (closest: a page-object step definition) | a step declared on a page object, valid on that page only |
| head | (none) | the driver a feature runs through: `maui`, `web`, `mixed`, `service` |
| subject | actor (Screenplay) | who performs a step: `Daan taps Save` |
| journey | (tags by convention) | the user journey a feature belongs to |

The attributes keep a `Bdd` prefix so a project can reference Reqnroll as well.

See [UAT Step Definitions And Flows](uat-step-definitions.md) for how to write them, and
[Coming From Reqnroll](coming-from-reqnroll.md) for the attributes side by side, what works
differently and what is not there.
