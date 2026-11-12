---
mode: agent
description: Write or extend a Bruinet BDD feature file from a user story, using only steps the suite has.
tools: ['bruinet']
---

Write the scenario the user describes as a Bruinet feature file (`*.feature.md`), in agent mode
with the `bruinet` MCP server.

1. Call `list_suites`, and pick the suite whose project should hold the file. Its `Features` folder
   is where the file goes.
2. Call `list_steps` for that suite, with a `query` of the words of each step you need. Use only
   the phrases it gives, filling in their `{placeholders}`. A step from a namespace needs the
   file's `Using` metadata. A control verb runs on the page the scenario is on.
3. Call `list_pages` for the `{page}` and `{control}` names, and `list_test_sets` for the
   `TestData` metadata.
4. Write the file in the format of the files next to it: `# Feature:`, the Metadata table, then
   `## Scenario:` sections with a `gherkin` fence each.
5. Call `check_scenario` with the file's path and your text. Fix every error it reports, in
   its own words, and call it again until it reports none.
6. When a step the story needs has no phrase, do not invent one. Leave the step in, mark it in a
   comment line in the fence (`# no step yet: ...`), and tell the user which step definition is
   missing. **Create step definition** in the editor makes its stub.

Propose the file; don't write it until the user agrees.
