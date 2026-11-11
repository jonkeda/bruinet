# Skills and agent instructions checked against the code

Written 2026-11-11 at `b2ea4570`. Read whole: `AGENTS.md`, `.github/copilot-instructions.md`, the one file under `.github/instructions`, the two prompts, the one agent, the 13 skills with their 12 reference files, and the MCP server's instructions (`tools/Bruinet.Cli/Lsp/BddMcpServer.cs:144`).
Every backticked name, path, link, script and tool in them was searched for in `src/`, `tools/`, `tests/`, `samples/` and `docs/`, and the names were read against `api-upgrade/references/changes.md`.
Nothing was built or run, and nothing but this file was written.

## Outcome

The names hold up well: of about 950 identifiers, two no longer exist and one is the wrong type to throw.
The 51 MCP tools the documents name all exist, and so do all six generator scripts and every `tests\` project in the command lists.
The damage is elsewhere: four paths that were deleted, a Scope section from another repository, three lists of the skills that disagree, and six rules whose copies say different things.
About 1,400 words can go without losing a rule, and one stale file of 1,306 words beside them.

## Names that are gone or wrong

| Where | Says | Correction |
| --- | --- | --- |
| `maui-control/SKILL.md:110`, `references/routes.md:82` | An action with no route throws `NotSupportedException` | `RouteUnavailableException`. `src/Bruinet.Core/Exceptions/RouteUnavailableException.cs:5` says a plain `NotSupportedException` "is still retried", and `Poller.IsFatal` (`Poller.cs:77`) ends the call only for the derived type, so a control written by the skill waits out its budget |
| `copilot-instructions.md:80-81` | Pointer input is opt-in, gated by `BRUINET_ALLOW_POINTER_INPUT` | Delete both lines. No code reads the variable, and `docs/guides/troubleshooting.md:76` says it does nothing. Copy `AGENTS.md:90-94` instead |
| `generator-contract.md:113` | Flag `Equals` | `EqualTo` (changes.md, `52a837fa`); line 106 of the same file already says `Comparison.EqualTo` |
| `AGENTS.md:23-24`, `:130`; `copilot-instructions.md:11`, `:104` | `docs2/` is the archive | Delete the mentions. The folder went in `5927e38d`; "Five trees" at `AGENTS.md:119` becomes four |
| `AGENTS.md:124`, `copilot-instructions.md:12`, `docs-upkeep/SKILL.md:26` | `.my/` is indexed in `.my/README.md` | The index went in `b2ea4570`. Drop the three mentions, or restore the index |
| `ai-item/SKILL.md:17-19` | Coverage steps come from `.my/coverage/plan.md` | That file went in `b2ea4570`. The steps and their status are in `.my/aiimprovements/plan.md` ("Coverage steps", C4 to C8) |
| `ai-item/SKILL.md:3` | Items AI1 to AI20 | The plan has AI21 |
| `AGENTS.md:28-31` | Work stays inside `Bruinet/`; parent BodyCam follows `../.github/copilot-instructions.md`; Exact.Construction, `.cnv2` | Delete the section. There is no `Bruinet/` folder, the parent is Tomorrow and has no such file. `copilot-instructions.md:110` carries the same `.specs` and `.cnv2` leftovers |
| `maui-ui-test/SKILL.md:132`, `:154-155` | MauiHeadless builds live in `samples/*.MauiHeadless`, which "links its sources" | The sample apps build for plain `net10.0` themselves (`Bruinet.Samples.Maui.App.csproj:9`, `docs/run/MAUI.md:28`). Say "from each sample app's own `net10.0` target" |
| `html-ui-test/SKILL.md:85` | The Todo web suites have no bUnit head yet | They have one: `samples/Todo/tests/Bruinet.Samples.Todo.Web.UITests/TodoWebHead.cs:17`. Delete the clause |
| `convert-control/SKILL.md:98-110` | Worked example from `Controls/ClickableControlBase.cs` | That file is gone; the example is the history of a finished conversion. Cut it |
| `html-control/SKILL.md:9`, `android-control/SKILL.md:9`, `generator-contract.md:4` | `Bruinet.Generator` writes the public API | It is a namespace inside `tools/Bruinet.Cli` now (AD-028). Say `bruinet controls generate`, as `generator-contract.md:21` already does |

## The same rule in more than one place

The copies agree unless the last column says otherwise.

| Rule | Stated at | Copies agree? |
| --- | --- | --- |
| The list of skills | `AGENTS.md:38-60` (10), `copilot-instructions.md:18-21` (5), `.claude/skills/` stubs (11), `.github/skills/` (13) | No. `AGENTS.md` lacks `api-upgrade`, `decision-record` and `docs-upkeep`; the last two have no Claude stub, though `AGENTS.md:35` says Claude reads every skill through one |
| A UI test or a scenario, not both | `AGENTS.md:200-202`; `maui-ui-test/SKILL.md:30-40`; `html-ui-test/SKILL.md:33-43`; `android-ui-test/SKILL.md:27-37` | Yes, word for word: 140 words, three times |
| The never-list for MAUI tests | `AGENTS.md:64-79`; `maui-ui-test/SKILL.md:158-181`; `maui-ui-test/references/forbidden-apis.md:5-24` | No. Only the skill and its reference forbid `ScrollingRoot`; only the reference lists sleeps and try/catch |
| A raised timeout | `maui-ui-test/references/forbidden-apis.md:15`; `maui-ui-test/SKILL.md:105-108` | No. The reference forbids it flat; the skill allows it for an operation long by nature, with a comment |
| A class with no Core methods is a plain `.cs` (R5) | `maui-control/SKILL.md:100`; `generator-contract.md:40-41`; `container.md:46`; `html-control/SKILL.md:40-41`; `android-control/SKILL.md:53-55` | No. The Android skill says such a control "is still a template with an empty generated partial" |
| Hand-written members that span parts | `generator-contract.md:192`; `component.md:112`; `html-control/SKILL.md:50-53` | No. The contract says plain calls with no `Run*` wrapper; the Html skill says they call the `Run*` helpers |
| XML documentation | `csharp-comments.instructions.md:11-24`; `AGENTS.md:137-141`; `copilot-instructions.md:112-115`; `maui-control/SKILL.md:126-139` | No. `maui-control/SKILL.md:134` asks for `<summary>` on every member; the policy deletes it on private and internal ones |
| Build and test commands | `AGENTS.md:147-155`; `copilot-instructions.md:28-36` | No. Only the first builds `Bruinet.Maui.FlaUI`; only the second runs `Bruinet.Uat.Tests` |
| No sleeps; wait for state | `AGENTS.md:101-105`; `copilot-instructions.md:60-71`; `routes.md:76-77`; `generator-contract.md:150`; the three `forbidden-apis.md` | Yes (AD-004) |
| xUnit `Assert`, no FluentAssertions | `AGENTS.md:111`; `copilot-instructions.md:93-94`; `maui-ui-test/SKILL.md:91-92`; `unit-tests.md:57`; `html-ui-test/SKILL.md:65-66`; `android-ui-test/SKILL.md:62` | Yes (AD-006) |
| bUnit first, the browser judges | `AGENTS.md:160-163`, `:185-188`; `copilot-instructions.md:38-41`, `:98-100`; `html-ui-test/SKILL.md:21-31`, `:82-97`; `html-control/SKILL.md:62-77` | Yes (AD-030) |
| `select-tests.ps1` | `AGENTS.md:190-194`; `copilot-instructions.md:41-43`; `html-ui-test/SKILL.md:23-27` | Yes; its switches match the script's `param` block |
| The generator contract | `generator-contract.md` (says it is the single statement, line 8); `maui-control/SKILL.md:71-84`; `html-control/SKILL.md:43-57`; `android-control/SKILL.md:57-67`; `convert-control/SKILL.md:30-60` | Yes, apart from R5 and `Run*` above |
| One UI test process, no build under a run | `AGENTS.md:213-214`; `maui-ui-test/SKILL.md:26`; `maui-control/SKILL.md:147`; `html-ui-test/SKILL.md:29`; `android-ui-test/SKILL.md:25` | Yes |
| Never invent a phrase; never sign in for the user | MCP instructions; `bruinet-write-scenario.prompt.md:21-23`; `bruinet-scraper.agent.md:32`; `scrape-pages/SKILL.md:22-23`, `:62`; `narrated-review/SKILL.md:15` | Yes |

## Against the decisions

- **AD-005 against `copilot-instructions.md:80-81`.** The record says no setting brings physical input back on MAUI; the instructions name one.
- **AD-005 itself is behind the code.** Its line 9 still says `NotSupportedException`, where the call model (`.docs/contracts/call-model.md:36`) and AD-009 say `RouteUnavailableException`. Correct the record with the skill.
- **AD-032 against `AGENTS.md:165-183`.** The record's "who judges" table (line 57) puts MauiHeadless on every change while working, and `maui-ui-test/SKILL.md:22-25` follows it. `AGENTS.md` and `copilot-instructions.md` never mention `BRUINET_HEAD=headless`: their tiers start at a real Windows app. `maui-control/SKILL.md:32-36` and `convert-control/SKILL.md:96` skip the head as well.
- **R5 against the code.** Twenty-one MAUI templates and seventeen Android ones declare no Core method or shortcut (`Button.tpl.cs`, `Border.tpl.cs`, `SeekBar.tpl.cs`). `container.md:47-48` calls MAUI's "not a model"; the Android skill calls its own the rule.
- **`decision-record/SKILL.md:21-27` against the records.** It asks for four labelled fields on at most one screen. Two of the 36 records carry a "Decided" label, and five run over 1,000 words (AD-033 has 1,385).
- **Six stacks, three with skills.** AD-010 counts six stacks; WPF, WinForms and Stride have no control or test skill and no line in `AGENTS.md` beyond `:93-94`.

## What can be cut

| Cut | Words | Why nothing is lost |
| --- | --- | --- |
| `copilot-instructions.md` down to what only it says (the Visual Studio solution at `:44-46`, Error Handling, Package Rules, Git Safety) | about 500 of 769 | It tells the reader to start at `AGENTS.md` (line 14), which holds the rest |
| "A UI test or a scenario, not both" in the three test skills, replaced by one line pointing at `AGENTS.md:200-202` | about 380 | Word-for-word copies |
| The never-list table in `maui-ui-test/SKILL.md:162-168` | about 130 | `forbidden-apis.md` holds the fuller table; keep the two paragraphs after it |
| `maui-control/SKILL.md:71-84` (contract summary) and `:141-147` (Proof) | about 200 | The first restates `generator-contract.md`, the second restates workflow steps 3 and 6 |
| `convert-control/SKILL.md:98-110` (worked example) | about 100 | History; the file it names is gone |
| `AGENTS.md:26-31` (Scope) and the `docs2` lines | about 70 | Stale |
| `.github/PIPELINE.md` | 1,306 | It describes `.github/workflows/build-and-test.yml`, dated January 2026; the workflows are `ci.yml`, `release.yml` and `security.yml` |

## Size of each file

An agent starting any task reads `AGENTS.md` and, as it asks, `copilot-instructions.md`: 2,408 words before the task's skill.
Writing a MAUI control and its test loads about 9,000 more.

| File | Words | Lines |
| --- | --- | --- |
| `AGENTS.md` | 1,639 | 219 |
| `.github/copilot-instructions.md` | 769 | 128 |
| `.github/instructions/csharp-comments.instructions.md` | 257 | 36 |
| `.github/prompts/bruinet-scrape-pages.prompt.md` | 117 | 18 |
| `.github/prompts/bruinet-write-scenario.prompt.md` | 243 | 26 |
| `.github/agents/bruinet-scraper.agent.md` | 326 | 35 |
| MCP instructions (`BddMcpServer.cs:144`) | 238 | 1 |
| `ai-item/SKILL.md` | 1,122 | 112 |
| `android-control/SKILL.md` | 640 | 74 |
| `android-ui-test/SKILL.md` | 705 | 90 |
| `android-ui-test/references/forbidden-apis.md` | 166 | 19 |
| `api-upgrade/SKILL.md` | 801 | 79 |
| `api-upgrade/references/changes.md` | 3,434 | 91 |
| `convert-control/SKILL.md` | 803 | 111 |
| `decision-record/SKILL.md` | 430 | 49 |
| `docs-upkeep/SKILL.md` | 333 | 36 |
| `html-control/SKILL.md` | 665 | 78 |
| `html-ui-test/SKILL.md` | 1,021 | 105 |
| `html-ui-test/references/forbidden-apis.md` | 210 | 23 |
| `maui-control/SKILL.md` | 1,394 | 155 |
| `maui-control/references/collection.md` | 1,088 | 158 |
| `maui-control/references/component.md` | 1,047 | 174 |
| `maui-control/references/container.md` | 952 | 150 |
| `maui-control/references/generator-contract.md` | 1,552 | 211 |
| `maui-control/references/routes.md` | 836 | 107 |
| `maui-control/references/simple-control.md` | 514 | 94 |
| `maui-ui-test/SKILL.md` | 1,740 | 200 |
| `maui-ui-test/references/forbidden-apis.md` | 506 | 58 |
| `maui-ui-test/references/page-objects.md` | 414 | 70 |
| `maui-ui-test/references/unit-tests.md` | 344 | 64 |
| `narrated-review/SKILL.md` | 555 | 62 |
| `scrape-pages/SKILL.md` | 843 | 77 |
| **Total** | **25,704** | |

## For the owner

1. **Which exception does a MAUI control throw when it has no route?** My proposal: `RouteUnavailableException` in `maui-control/SKILL.md:110`, `routes.md:82` and AD-005, because the base type is retried until the budget ends. The five MAUI templates that throw the base type today (`Slider`, `Stepper`, `RadioButton`, `ContentDialog`, `Expander`) are then a code review of their own.
2. **Is R5 the rule or the exception?** My proposal: keep R5 (no Core method, no template) and change `android-control/SKILL.md:53-55` to match, since `generator-contract.md` calls itself the single statement. The 38 empty templates stay as debt, named as such.
3. **Should `copilot-instructions.md` stay a second rule book?** My proposal: shrink it to a pointer at `AGENTS.md` plus the four things only it says, so a rule changes in one file.
4. **Where does the "scenario or code test" rule live?** My proposal: `AGENTS.md` only, with one line in each test skill, because it is not a rule of one stack.
5. **Does `.my/` keep an index?** My proposal: no. Drop the three mentions, since the folder now holds seven folders and `b2ea4570` removed the file with the finished notes.

## Not in the assignment, same cause

- `.github/PIPELINE.md` (above) is read by any agent that searches `.github`.
- `.claude/agents/control-source-builder.md` (718 words) is a seventh instruction file; `generator-contract.md:9` names it. It was not checked line by line.
- `docs/architecture/stack.md:61` and `docs/guides/troubleshooting.md:76` already say `BRUINET_ALLOW_POINTER_INPUT` is dead; only `copilot-instructions.md` still uses it.

## Unknown

- `bruinet-write-scenario.prompt.md:21-22` marks a missing step with a `# no step yet: ...` line inside the fence. The words appear nowhere in `tools/Bruinet.Cli`, `src/Bruinet.Bdd` or `docs/`, so whether `validate_scenario` accepts such a line could not be settled by reading.
- The measured numbers were not checked: the tier costs in `AGENTS.md:175-179`, "about 2 s against about 40 s" at `:161`, "about 70" `.gen.cs` files with changed line endings at `generator-contract.md:29`, and the 12 s product search at `collection.md:126`.
- `ai-item/SKILL.md:110` sends the reader to "the repository's push routine" for the jonkeda mirror. No file in this repository describes one; `origin` is `exact-mobile-apps/test-tooling`.
- Names were checked for existence, and the ones in the tables above for meaning. A member that still exists but changed its behaviour would not show; the three `maui-control` kind references were read for that, the Android skills only by name.

Next action: the owner answers points 1 to 5; the corrections in the first table need no decision.
