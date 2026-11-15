# The spec format

A spec says what one journey of a product must do, so that it can be built and checked without
anyone knowing the code. It is written as if the code did not exist yet, even when it is worked out
from an app that runs. The example is [TOD.02 Create a todo](../specs/todo/TOD.02.spec.md).

## The file

One journey per file: `specs/<product>/<journey id>.spec.md`, such as `specs/todo/TOD.02.spec.md`.
A journey id is a module's letters and a number (`TOD.02`); a site with several products may put the
product's letters in front (`B7.HRS.01`). It starts with `# Spec: <journey id> <journey name>` and a table:

| Field | Holds |
| --- | --- |
| Product | The product's name |
| Journey | The journey's id and name |
| Where | The places a person uses it: devices, the web |
| Status | `draft`, or `accepted <date>` once the owner said it is what the product means |

Then these sections, in this order. Leave out a section with nothing in it, except Intent, Rules
and Examples.

## Intent

Two or three sentences: who does what, and why it matters to them. No rule goes here; it is the
reason the rules exist, so a question the rules don't answer can be answered from it.

## Rules

A table of `Id` and `Rule`. The id is the journey's id, `R` and a number: `TOD.02.R1`. Numbers are
never reused. Each rule is one sentence about what a person can see or do, exact enough to be
false: a number, a message's words in quotes, a state the screen shows. A line above the table may
say what holds for every rule.

## Background

Optional. A `gherkin` block of Given steps that hold for every example, such as the account that is signed in. Its
steps go in front of every example.

## Examples

One `### R<n> <short name>` per rule, at least one example each. A short paragraph says why the
rule is there, in the person's terms. Then a `gherkin` block with `Given`, `When`, `Then` and
`And`, in the words on the screen: the buttons' names, the messages as shown, the values typed.
More than one example of a rule go in the same block, separated by a blank line.

Examples that start the same way may go in a **chapter**: a `### Chapter: <name>` heading, an optional paragraph, and
a `gherkin` block of Given and When steps, no Then; then its examples as `#### R<n> <short name>`, each with the
steps that follow the chapter's (often only Then). A reader reads an example as the Background's steps, the
chapter's, then its own. An example at the `###` level after a chapter is outside it.

## Choices

Bullets of what was decided to leave free, so nobody builds a limit for it: "Two todos may have the
same title."

## Not in this journey

Bullets of what a reader might expect here and is elsewhere, with that journey's id, or not planned.

## Open questions

Bullets, each a question and `Proposal:` with an answer and why. An answered question leaves this
section and becomes a rule, an example or a choice. A spec is `accepted` only with none left.

## The check

`frainet check <spec or folder>` checks all of the above that can be checked without a model: the
title and table, the sections and their order, the rule ids, a sentence per rule, an example per
rule with a paragraph and Given, When and Then in order, a proposal per open question, and the
words below. It names each finding with its line.

## The trace

`frainet trace <spec> <tests folder>... [--runnable]` reads the test sources as text, without building or running
them, and lists each rule with the tests that name it. A C# test names a rule with
`[Trait("Rule", "TOD.02.R3")]`, a scenario with the tag `@TOD.02.R3`. It fails on a rule no test
names, and on a test naming a rule the spec does not have. Naming a rule in a test is the builder's
work, not the spec writer's: the spec stays free of tests.

With `--runnable` the spec's own examples count as the tests of their rules: a suite that runs its specs (Bruinet's
runnable specs) runs each example as a test case named after its rule (`TOD.02.R3 …`), and `--results` matches it by
that name. What makes a step run lives in the suite's code, never in the spec; the spec only has to use words the
suite's vocabulary knows.

## What a spec never holds

- Code: no class, field, file, test, table or project names, and no internal states.
- How a rule is built, or which test proves it; tying rules to tests is a check's job.
- What an agent may decide alone; that is a rule of the factory, the same for every spec.
- Vague words that cannot be false: "fast", "should", "appropriate", "user-friendly", "etc.",
  "and so on", "as needed".
