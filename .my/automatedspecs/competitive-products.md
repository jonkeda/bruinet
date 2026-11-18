# Automated specs: what other products do

Status: **research**, first pass. Date: 2026-11-18.

The question: Lirenet wants a spec whose parts become tests by themselves. A field table with
"required" ticked makes the "empty is refused" test; a flow makes the navigation tests; a rule gets
an example that runs (Lirenet's design, `.my/spec/design/automation/`). **Who else
turns a spec into tests, from what kind of spec, and how exact is the result?**

This adds to `.my/develop/competitors.md` (2026-10-26), which asked a wider question
(readable specs, a check before building, a trace after). The products there are not described
again; they appear below only for what they do about *generating tests*.

| | |
| --- | --- |
| How | Eight web searches on 2026-11-18, plus what the two earlier documents found. No product was installed or tried |
| Quality of the sources | Mixed. Playwright, Kiro, Tricentis and FitNesse are from their own documentation or blogs. Most comparisons of AI test tools are written by competing vendors; treat them as claims |
| Not found | Tessl's current state (the search gave nothing); whether Gauge, Concordion, GraphWalker, Yest and TestCompass are still actively kept; any prices |

## 1. In short

**Nobody found does what Lirenet's design describes: several spec styles in one plain file, each
turned into tests by a fixed rule, with every test traced back to the row or rule it proves.**
The pieces exist, each in a different product family:

| Family | Spec it starts from | Tests made by | Exact? | Closest to which Lirenet style |
| --- | --- | --- | --- | --- |
| Executable specifications | Tables and scenarios a person writes | A fixed rule, plus hand-written glue code | Yes | Example table, decision table, Gherkin |
| Model-based testing | A flow or state model | A fixed rule (path coverage) | Yes | Flow, state diagram |
| Contract testing | An API description (OpenAPI) | A fixed rule, or generated inputs | Yes | Field table (the nearest thing to it, for an API) |
| Spec-driven agent tools | Requirements in sentences | A model | No: differs per run | One-line rule (EARS) |
| AI test tools | The running app, or plain English | A model | No | None: they have no spec |
| Requirement suites with AI | A user story in Jira | A model, reviewed by a person | No | None: text test cases, not runnable |

Three conclusions:

1. **The exact generators all need a formal source, and each takes one kind only.** A table tool
   does tables, a model tool does flows, a contract tool does an API. Lirenet's "pick the style by
   the question" is the new part, not any single generator.
2. **The tools that start from readable text all use a model to make the tests**, so the same spec
   gives different tests each time and nothing proves the tests cover the spec. That is the gap
   `lirenet trace` closes, and none of them has it built in.
3. **For screens there is no equivalent of contract testing.** An API has OpenAPI, and tools that
   test every field's type, required mark and limit from it. A screen has nothing of the kind.
   Lirenet's field table (or the Wireframe as its source) would be that: the strongest idea to
   borrow is how those tools work, not any UI tool.

## 2. Executable specifications: tables that run

| Product | What it does | Next to Lirenet |
| --- | --- | --- |
| FitNesse | Wiki pages with tables. The **decision table** is its default: each row is a case, input columns go in, output columns are compared. A "fixture" class per table calls the system | The oldest working form of "a table is the test". But every table needs its own fixture code, and the wiki is its own server. Recommended today only for teams that already have it |
| Cucumber, Reqnroll | Gherkin; a `Scenario Outline` with an `Examples` table runs once per row | Bruinet's UAT is this already. Step code per sentence |
| Gauge | Markdown specs; a data table at the top runs the whole spec per row | Markdown like Lirenet's. One vendor's 2026 review calls it hard to justify for a new project; not checked |
| Concordion | A Markdown or HTML document whose marked words are checked; the result is the document, coloured | The best-looking "living document". Java, glue code per document |
| Robot Framework | Keyword tables; a template makes one test per data row | Widely used and kept. Not searched this time |

What they share: **a person writes every row, and a programmer writes the glue.** None derives a
test from a fact ("Name is required") without a row saying so. Lirenet's design removes both: the
tick is the row, and the glue is one generic check per column, written once in Bruinet.

**Borrow:** FitNesse's decision-table shape (inputs left, expected right, one row one case) for
T4; Concordion's result as the spec itself with each row green or red, which is what the trace
should look like to an owner.

## 3. Model-based testing: flows that run

| Product | What it does | Next to Lirenet |
| --- | --- | --- |
| GraphWalker | Open source. A graph of states and actions; it walks paths to a coverage goal and calls a method per node and edge | The nearest to "the flow diagram makes the navigation tests". The model is its own file format, and each node needs code |
| Tricentis Tosca | Enterprise. A reusable model of the application's screens; tests are built from the model, not from scripts | The same idea as page objects plus specs, at company scale. Proprietary formats, a steep learning curve by its reviewers' account |
| Conformiq Designer | Generates tests from behaviour models and requirements, with coverage analysis | Strong in telecom and embedded; a heavy tool |
| Yest (Smartesting), TestCompass | Graphical workflows with decision tables; tests from a readable model | The closest in spirit to mixed styles (a flow plus tables). Little independent information found |

What they share: the model is drawn in the tool, not written as text in git, and the model is a
second artefact beside the requirements. In Lirenet the flow would be read from the screens'
`@Target` links (see `330-Salt-vs-Wireframe.md`), so there is no separate model to keep.

**Borrow:** the coverage vocabulary (every screen visited, every link taken, every pair of links)
as the definition of "the flow is tested" for P4 and T6.

## 4. Contract testing: the model for a field table

| Product | What it does | Next to Lirenet |
| --- | --- | --- |
| Specmatic | Open source. Reads an OpenAPI file (also AsyncAPI, gRPC, GraphQL, WSDL) and runs it as tests against the service, with no test code; the same file is a stub for the other side; checks that a new version does not break the old | **The closest working example of an automated spec.** One file, no glue, tests from the declared facts, a compatibility check between versions |
| Schemathesis | Open source. Property-based: from each field's type and limits it generates many valid and invalid inputs and checks the service's answers | What "max 100" in a field table should become: values at 100, at 101, empty, odd characters |
| Pact, Dredd | Consumer-driven contracts; a description checked against a server | Older forms of the same |

These work because the source is exact and the checks are generic: *required means a request
without it is refused*, for every field of every API. That sentence is Lirenet's T1 for a screen.

**Borrow:** the whole shape. A field table is a contract of a screen. Generate the valid case, one
invalid case per constraint, and the boundary values. And Specmatic's backward-compatibility
check: "this change to the spec removes a required field" as a finding in a pull request.

## 5. Spec-driven agent tools: readable, but made by a model

| Product | What it does about tests | Next to Lirenet |
| --- | --- | --- |
| Kiro (AWS) | Requirements in EARS; in the design step it derives "correctness properties" and from them property-based tests. Newer: a requirements analysis that flags ambiguous and contradicting criteria before design | **The most serious competitor.** It has both of Lirenet's checks in some form. But a model does both; the property tests run in its IDE only and are optional by default (AWS's own slides); and passing them does not show every requirement is covered |
| GitHub Spec Kit | Spec, plan, tasks; tests are tasks the agent writes. Community extensions add spec-to-task traceability and paired test specs | No built-in check from spec to tests |
| OpenSpec | A folder per change with proposal, specs, design, tasks | Lighter than Spec Kit; the same gap |
| Playwright test agents | Three agents since version 1.56: a **planner** explores the running app and writes a Markdown test plan under `specs/`; a **generator** turns the plan into Playwright tests, checking selectors live; a **healer** repairs failing tests | New since the Frainet document, and the same pipeline as Bruinet's `spec-from-app` and `scenario-from-spec` skills. Free, from Microsoft, in the most used test tool. Its "spec" is a test plan, with no rules, no ids and no check; its healer changes tests to make them pass, which Bruinet's triage forbids for a changed app |
| Tessl | Spec as the source, code made from it | Nothing found this time |

Guides on these tools describe the missing piece themselves: give each criterion an id, tag each
test with the id it covers, and fail the build on a criterion with no test or a test naming a
criterion that does not exist. That is `lirenet trace`, described as a script everyone has to
write.

**Borrow:** Kiro's requirements analysis as the model-assisted *draft* of findings, beside
Lirenet's exact check; Playwright's planner as a second source of drafts for a spec.

## 6. AI test tools: no spec at all

testRigor (plain English steps, for testers who do not code), Momentic (the same for engineers:
files, a command line, CI), mabl (low-code, enterprise), QA Wolf (a service: people and agents
write Playwright tests for you). Octomind's site did not resolve in October 2026 by one report.

They compete with Bruinet more than with Lirenet. A test in plain English *looks* like a spec, but
it is steps, not rules: it cannot say "Name is required" without a scenario that tries it, and
nothing checks the set of tests against what was asked. Most keep the tests in their own cloud;
only QA Wolf and plain Playwright leave you with code you own.

**Borrow:** nothing in the spec. Worth knowing as what a customer will compare the whole of
Bruinet and Lirenet against, on price and on how fast the first test exists.

## 7. Requirement suites with AI

Xray (Jira) turns a requirement into manual or Cucumber-style test cases for a reviewer to accept;
Tricentis qTest Copilot does the same from a requirement's criteria; several Marketplace apps do
it for Xray and Zephyr. Atlassian's Rovo can draft test cases but not manage them in Xray.

The output is text test cases for a person, in a tool outside git. They hold the trace Lirenet
holds (requirement to test to result), across a company, with sign-off. They do not make the test
run, and an agent or a night run cannot read them as files.

## 8. Where Lirenet would stand

| | Executable specs | Model-based | Contract | Agent tools | AI test tools | Lirenet's design |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| Spec is plain text in git | ● | ○ | ● | ● | ○ | ● |
| An owner can read it | ◐ | ◐ | ○ | ● | ● | ● |
| Tests made without a model | ● | ● | ● | ○ | ○ | ● |
| No glue code per spec | ○ | ○ | ● | ● | ● | ● (the aim) |
| Tests from facts, not only from written cases | ○ | ● | ● | ◐ | ○ | ● (the aim) |
| Every test traced to its rule or row | ◐ | ◐ | ● | ○ | ○ | ● |
| Works for screens | ● | ● | ○ | ● | ● | ● |
| Drafted from the running app | ○ | ○ | ◐ | ◐ | ● | ● |
| Exists and is used today | ● | ● | ● | ● | ● | ○ |

● good · ◐ partly · ○ poor. The last row is the honest one: every competitor ships, and Lirenet's
automation is a design.

## 9. Risks this shows

| # | Risk | Why it matters |
| --- | --- | --- |
| 1 | **Playwright's agents make "spec to tests" free and good enough** | Most teams will accept a model-made test plan and tests they can read. Lirenet's answer has to be what a model cannot give: the same tests every run, and proof of coverage |
| 2 | **Kiro adds an exact check** | It already has EARS, an analysis step and generated tests. An exact trace is a small step for them |
| 3 | **Generic checks are harder on screens than on APIs** | "Required is refused" has one meaning for an API. On a screen the refusal may be a message, a disabled button or a red border. The generic check needs the page object to say which, or it becomes glue code again |
| 4 | **Too many styles** | Every product above does one style well. Six styles means six generators to keep |

## 10. Questions

| # | Question | Proposal |
| --- | --- | --- |
| A1 | Which generator first? | The field table, built like a contract test (section 4): the clearest gain, and no competitor has it for screens |
| A2 | Try Playwright's planner on Bouw7 and compare its plan with a `spec-from-app` draft? | Yes, a half-day trial: it is the nearest free alternative and may be a useful drafter |
| A3 | Try Kiro on one Bouw7 journey, for its requirements analysis and its property tests? | Yes, to see what a model finds that `lirenet check` does not, and the other way round |
| A4 | Look further at Yest and TestCompass, the two that mix flows and tables? | Only if the flow generator (P4) is built; then for their coverage rules |
| A5 | Verify the unverified: upkeep of Gauge, Concordion and GraphWalker, Tessl's state, prices? | When a decision depends on one of them |

## Sources

Read on 2026-11-18 through search summaries, not in full.

- Kiro: [Requirements analysis (kiro.dev)](https://kiro.dev/blog/deep-spec-analysis/) ·
  [Kiro specs explained (codemyspec.com)](https://codemyspec.com/blog/kiro-specs-explained) ·
  [Kiro: spec files and property-based tests (developertoolkit.ai)](https://developertoolkit.ai/en/comparison/kiro/)
- Playwright: [Test agents (playwright.dev)](https://playwright.dev/docs/test-agents) ·
  [Planner, generator and healer in action (debbie.codes)](https://debbie.codes/blog/playwright-agents-planner-generator-and-healer-in-action)
- Spec-driven tools: [Spec-driven development in 2026 (codemyspec.com)](https://codemyspec.com/blog/spec-driven-development) ·
  [OpenSpec guide (jamasoftware.com)](https://www.jamasoftware.com/blog/openspec-guide/) ·
  [Spec-driven testing (testmuai.com)](https://www.testmuai.com/spec-driven-testing/)
- Model-based testing: [Model-based testing tools (browserstack.com)](https://www.browserstack.com/guide/model-based-testing-tool) ·
  [TestCompass (ministryoftesting.com)](https://www.ministryoftesting.com/software-testing-tools/testcompass) ·
  [Tosca alternatives (contextqa.com)](https://contextqa.com/blog/tricentis-tosca-alternatives/)
- Contract testing: [Specmatic (github.com)](https://github.com/znsio/specmatic) ·
  [Schemathesis property testing (qaskills.sh)](https://qaskills.sh/blog/schemathesis-openapi-property-testing)
- Tables: [FitNesse decision table (fitnesse.org)](https://fitnesse.org/FitNesse/UserGuide/WritingAcceptanceTests/SliM/DecisionTable.html) ·
  [BDD tools for 2026 (contextqa.com)](https://contextqa.com/blog/top-10-behavior-driven-development-testing-tools/)
- AI test tools: [QA Wolf, Momentic, testRigor (wetheflywheel.com)](https://wetheflywheel.com/en/comparisons/qa-wolf-vs-momentic-vs-testrigor/) ·
  [AI tools for testing in 2026 (bugbug.io)](https://bugbug.io/blog/test-automation-tools/ai-tools-for-testing.md)
- Requirement suites: [qTest Copilot (tricentis.com)](https://tricentis.com/products/unified-test-management-qtest/copilot) ·
  [qTest AI functions (docs.tricentis.com)](https://docs.tricentis.com/qtest-saas/content/manager/ai_integration/ai_functions.htm) ·
  [Xray and Rovo (community.atlassian.com)](https://community.atlassian.com/forums/Rovo-questions/XRay-test-management-and-Rovo/qaa-p/3214538)
