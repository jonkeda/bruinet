# AD-011: UAT Step Vocabulary Is Declared By Attributes

Every UAT step is declared by one of three attributes: `[BddGiven]`, `[BddWhen]` and
`[BddThen]`. They derive from the abstract `BddStepAttribute` in `Bruinet.Bdd` (package `Bruinet.Bdd.Abstractions`),
which is the type discovery reads. There is no other way to declare a step, and no attribute
for `And` or `But`: those take the keyword of the step before them, so a check written after
`But` is a `[BddThen]`.

What a step is follows from the method it sits on, not from the attribute:

- On a control method it is a **control verb**. The built-in vocabulary lives on the
  `Bruinet.Core` control interfaces (`IClickableControlObject`, `IEditableTextControlObject`,
  `IToggleControlObject`, `ISelectorControlObject`, `ITextControlObject`, `IElementObject`);
  an app adds its own on its control types. `CommandId`, `Literal` and `HasLiteral` belong to
  control verbs.
- On a fixture method, or a method of a step-definition class (`[BddStepDefinitions]` or
  `BddStepDefinitionsBase`), it is a **step definition** (called a phrase in the older ADs).
  Discovery rejects `CommandId`, `Literal` and `HasLiteral` there, naming the method.
- On a page-object method it is a **page phrase**, valid on that page only (AD-019).

The engine discovers the vocabulary by reflection over those attributes and projects it into
every catalog it needs: `Bruinet.Bdd` the step definitions, `Bruinet.Uat` the control verbs and
page phrases (AD-021).

The three intrinsic page-graph verbs (`I am on the {page} page`,
`I should be on the {page} page`, `I should see {text}`) are the engine's own and stay
registered directly in the runtime and spec catalogs - they bind to no control method. So is
`{app} uses the app`, registered only when the fixture hosts several apps (AD-014).

**Ruled out:**

- Hand-written phrase tables. There is no second place that lists the control phrases. The
  runtime catalog, the spec catalog and any preview all come from the same
  `UatCatalogBuilder.DiscoverBindings` pass, so a phrase and the surface that runs it cannot
  drift.
- A second attribute for control verbs (the former `[BddStep]`).
- A writable long form (`[UatPhrase(keyword, phrase)]`) next to the short forms, and a form
  without a keyword.
- `[UatAnd]` / `[UatBut]` attributes.
- Keeping the `Uat` names (`[UatGiven]`, `[UatPhraseClass]`) beside the `Bdd` ones: the rename was
  one step, with no alias period.

**Broken when:** a step is declared by any attribute other than `[BddGiven]`, `[BddWhen]` or
`[BddThen]`; a control phrase is added by editing a `catalog.Register(...)` call for a control
verb instead of attributing the control method; or a projection (runtime, spec, presenter
preview) re-lists control phrases by hand instead of calling the discovery.
