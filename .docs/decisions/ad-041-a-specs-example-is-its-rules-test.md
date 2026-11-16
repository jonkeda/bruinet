# AD-041: A Spec's Example Is Its Rule's Test

**Decided:** a Lirenet spec runs as tests on the BDD engine: each example is a scenario named and tagged with its rule
(`B7.INV.01.R1 …`, `@B7.INV.01.R1`, traits `Rule` and `Spec`), with the spec's Background and its chapter's steps in
front. A rule is checked once: by its example, or by a C# test with its `Rule` trait where an example says it badly.
A step reaches a control through the vocabulary: a control's verbs, a control name (`[BddControl("the filter above
{column}")]` on a method), or an extension verb (`[BddVerbs]`, written once for a type); what binds lives in code,
never in the spec.

**Why:** a C# test that copies an example drifts from it unnoticed, and only a programmer can check the copy. Verbs
on a type and names on a component reach every page that has them, so the vocabulary grows by type, not by page.
Evidence: [design.md](https://github.com/exact-mobile-apps/test-tooling/blob/e3310f54/.my/runnablespecs/design.md);
Bouw7's B7.INV.01 runs from its spec alone, green on acc.

**Ruled out:**
- a C# test and a runnable example for the same rule;
- binding or code in a spec (a class, a method, a selector);
- a phrase per page for a verb that a type or a component could declare once;
- resolving a step to a control by a model at run time.

**Broken when:** `lirenet trace --runnable` reaches a rule twice; a spec names code; a suite has the same phrase on
several page objects that one extension verb or control name would cover.
