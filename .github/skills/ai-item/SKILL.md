---
name: ai-item
description: Build one item of Bruinet's AI improvements plan (.my/aiimprovements/plan.md, AI1 to AI20) end to end - design note when the item needs one, test-first build, checks, an agent check where a fresh agent must solve a seeded bug with the new tools, an independent review, and the close (docs, CHANGELOG, MCP instructions, plan status, one commit). Use for "/ai-item AI3", "/ai-item next", "/ai-item status", or when asked to continue, build or check an item of the AI improvements plan.
---

# Build an AI improvement

The plan is [.my/aiimprovements/plan.md](../../../.my/aiimprovements/plan.md): its order table, one section per item with
its tools and a "Done when" line, and a status table at its end. This skill takes one item through six phases. It runs
unattended except at the gates named below; at a gate, stop and ask the owner, with your proposed answer and why.

## Arguments

- `/ai-item <id>`: that item, from the phase its status shows.
- `/ai-item next`: the first item in the order table whose status is not `done` and whose "Builds on" items are `done`.
- `/ai-item status`: the status table and the next item, nothing else.

## Before starting

1. Read `AGENTS.md`, the plan's section for the item, and the sections of the items it builds on.
2. Check the working tree: another session may be working in this checkout. Stage only files you changed, by path; never
   `git add .` or a folder.
3. Set the item's status to `design` or `build` in the plan's status table.

## 1. Design (only when the plan says the item needs a note)

The plan's "Per item" line names them. Write `.my/aiimprovements/<id>-design.md` (agent document: exact names, tables, no
narrative):

- the tools: name, arguments, result shape as JSON with one example;
- the code: project, folder, class per change; what is wrapped (an existing language server method) and what is new;
- the tests: name and project per test, and the "Done when" line as a test;
- open questions, each with a proposed answer and why.

**Gate:** open questions left, or a spike the plan calls for (AI7, AI11) → stop and ask. None → go on.

## 2. Build, test first

1. The MCP contract test first, in `tests/Bruinet.Cli.Tests`: call the tool through `BddMcpServer` as an agent would,
   assert the result's shape and one real value. Red.
2. The code. Wrap what the language server already does (`BddLanguageServer.*`, `bruinet/run/*`, `bruinet/step/*`) before
   writing anything new; a tool is a few lines in `BddMcpServer`'s `Tools` table and its switch.
3. Unit tests for anything new below the tool, in the project that owns it.
4. Green, then refactor while green. Commit when a piece stands green (message: `<id>: <what>`).

Tool descriptions are read by models: say what it returns and when to call it, in one or two sentences, like the existing
rows. Results stay small: cap long text, give `file:line` pointers instead of file contents.

## 3. Checks

All must pass before the agent check:

```bash
dotnet build Bruinet.sln -v:minimal /nr:false
dotnet test tests/Bruinet.Cli.Tests -v:minimal /nr:false
```

Plus the unit test projects the diff touches, and the UI suites it reaches on their fast head (bUnit, `headless`):
`bruinet tests affected --base <item's first commit>^` names them. A browser or device run only when the item changes a
head's driver. A timing test failing on a busy machine is noted in the review, not rerun.

## 4. Agent check

The item helps an AI only if an agent uses it to do better. Per item, `.my/aiimprovements/checks/<id>.md` holds:

| Field | What |
| --- | --- |
| Fixture | a seeded bug or task in a sample (Todo first), as a patch file beside it |
| Task | the prompt the agent gets, naming no tool |
| Success | how to tell it worked: the scenario green, the right file changed |
| Baseline | the tool calls and outcome without the item (measured once, on the commit before it) |
| Result | the same with the item |

Run it with a fresh general-purpose agent in a worktree with the patch applied and only the Bruinet MCP server, file
reading and editing, and `dotnet` allowed. Give it the task only, never the expected fix. Passes when it succeeds and needs
no more calls than the baseline (or succeeds where the baseline failed). A fail goes back to phase 2: usually a tool's
description or result shape, not the code.

Wave 1 items may share one fixture (a Todo bug a scenario catches); later items add their own.

## 5. Review

A fresh general-purpose agent gets the diff (`git diff <first commit>^..HEAD`), `AGENTS.md`, `docs/architecture/` and the
item's plan section, never your summary. It returns blockers, concerns and notes against: the architecture docs, the
`AGENTS.md` rules, tool results kept small, tests that prove the "Done when" line, nothing beyond the item's scope. Fix the
blockers; fix or record the concerns. Write `.my/aiimprovements/<id>-review.md`: what was built, the checks, the agent
check's numbers, what the review found and what was done about it.

**Gate:** none for wave 1. Waves 2 to 4: the owner reads the review and says go.

## 6. Close

In one commit, `<id>: close`:

- `docs/`: the guide that covers it (`docs/run/cli.md` for a command; a tool in "Copilot And Other Agents" of
  `docs/guides/visual-studio-preview.md`, where the MCP tools are listed).
- `CHANGELOG.md` under `[Unreleased]`: one entry, what a user can now do.
- The MCP server's instructions (the `instructions` string in `BddMcpServer`'s initialize answer) when the item changes how an agent should work.
- An AD through the `decision-record` skill only when the item ruled something out.
- The plan: status `done`, the commit, one line of what was learned; a follow-up as a new row only when it is real work.

Push `move` and the jonkeda mirror as the repository's push routine does. End with one line:
`Expected next action: /ai-item next (<id>)` or the gate's question.
