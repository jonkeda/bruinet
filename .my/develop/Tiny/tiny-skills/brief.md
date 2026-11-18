Create a brief for this capability from its plan item.

- Scenarios in Gherkin (`Scenario`, `Given`, `When`, `Then`), with steps a page object can perform; they become the acceptance tests.
- Keep the capability small: build what the plan item asks, and name what grows later instead of building it.
- The owner reads it in two minutes: at most 500 words.

Gate: every scenario parses as Gherkin, and the brief stays under 500 words (`brief-gate.py`).
