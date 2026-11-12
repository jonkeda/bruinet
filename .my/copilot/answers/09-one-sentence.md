# What is Bruinet really?

Taking the README's claims as true, Bruinet is not "another test framework". It is a **stack-independent test model**
(page objects, ControlObjects, Markdown scenarios) with interchangeable "heads" underneath, wrapped in a tool belt (Scraper,
SQL replay, test selection, narrated video, LSP, MCP). The drivers themselves (FlaUI, Appium, Playwright, Selenium, bUnit) are
borrowed; what Bruinet owns is the layer above them and the loop around them.

## The four sentences

**For developers:**
Write each screen's page object and control objects once, bind plain-Markdown Given/When/Then to them, and run the same
scenarios on a real device or browser at the gate and in-process (MauiHeadless, bUnit, or straight against services) in
the inner loop, with no driver calls, no sleeps and no database to set up.

**For engineering managers:**
One way of writing UI tests across your MAUI, WPF, WinForms, Blazor/web and Android apps, with scenarios product owners can
read, and with the costs that usually kill UI suites (hand-written page objects, slow devices, test databases, running
everything on every change) attacked by tooling instead of headcount.

**For a CTO:**
A .NET-only abstraction layer that decouples your acceptance tests from the UI technology and from the device, so the
suite survives a move from WPF to MAUI or from a browser to bUnit, at the price of betting on a young, broad, small-team
framework sitting between you and Playwright, Appium and FlaUI.

**For an investor:**
An open-source (MIT) .NET testing toolkit that makes UI tests cheap enough for coding agents to write, run and explain
(through an MCP server, a Scraper that drafts page objects, and narrated videos for review), in a niche that Microsoft has
left under-served since Xamarin.UITest and Coded UI died, but with no business model, no paying wedge and no evidence of
adoption visible in the README.

## Which was hardest, and what it says

**The investor sentence was by far the hardest.** The developer sentence wrote itself: the "one model, many heads" idea is
concrete and testable. The manager and CTO sentences needed only a trade-off. The investor sentence needed a *who pays and
why now*, and the README does not supply either. I had to pick one of several possible stories (agent-ready testing, the
MAUI testing gap, BDD for .NET, test-cost reduction), and each one leaves most of the product out.

That says the project is **engineering-led and breadth-first**. It is a strong answer to "how should a .NET team test
every UI it has?", but it has not decided which single customer it is for. The README lists nine "you want to" rows of
equal weight; a product has one first row.

## What appears genuinely novel, and what is common

| Novel or rare | Common in the industry |
| --- | --- |
| One scenario and page-object set across real device, in-process render (MauiHeadless) and service head | Page objects, Given/When/Then (Selenium, Reqnroll/SpecFlow, Cucumber) |
| Headless MAUI rendering as its own package | bUnit for Blazor components; Playwright for browsers |
| SQL replay so a UI run needs no database | Test impact analysis (Azure DevOps TIA, Launchable, nx/Bazel affected) |
| A Scraper that drafts page objects with no model call | Recorders and object repositories (TestComplete, Ranorex, Playwright codegen) |
| Narrated scenario videos, voiced, for review | Trace viewers and video (Playwright, Cypress) |
| Mixed scenarios driving Windows, web and Android at once | MCP servers for browsers (Playwright MCP) |

The single most novel thing is the **head** idea applied to MAUI: the same Markdown scenario judged by the device at a gate and
by an in-process render in the loop. Nobody else in .NET offers that for MAUI. The rest are known ideas, integrated
unusually well and unusually broadly.

## Strengths

- A clean layering (tests, page objects, controls, core, drivers) that really does isolate tests from the driver.
- It fills an obvious gap: MAUI has no first-party UI testing story worth the name, and FlaUI and Appium are raw drivers.
- Markdown scenarios are friendlier than Gherkin `.feature` files and need no Reqnroll code generation.
- The tool belt targets the actual reasons UI suites are abandoned: authoring cost, speed, data setup and flakiness.
- It is agent-ready by design (MCP, skills, AGENTS.md), which most incumbents bolted on late or not at all.

## Weaknesses

- **Breadth over depth.** Eight drivers, six stacks, a game engine, a presenter, two editor extensions and a video narrator
  are a lot of surface for one framework; each surface is a place to be second-best.
- **The lowest common denominator problem.** An abstraction over FlaUI, Appium and Playwright tends to lose what makes each
  driver good (Playwright's auto-waiting, tracing and network control; Appium's gestures). The README does not say how
  Bruinet keeps those.
- **Head parity is a promise that is hard to keep.** A green MauiHeadless or bUnit run is not a green device run (layout,
  focus, platform renderers, timing). The README says "the device stays the judge", which is right, but teams will skip the gate.
- **BDD's known failure mode.** Markdown scenarios read well, but product owners rarely write or maintain them; phrase
  libraries rot just as Reqnroll step libraries do.
- **Stride and narrated video** look like the author's own needs rather than a market's; they dilute the story.

## Risks

- **Bus factor and maintenance.** Keeping pace with Playwright, Appium 2/3, MAUI, .NET's yearly releases and Visual Studio's
  extension model is a full-time team's job.
- **Dependency on drivers it does not control.** An Appium or FlaUI break becomes a Bruinet break.
- **Microsoft or Playwright moving in.** If Microsoft ships a real MAUI test story, or Playwright adds desktop or MAUI
  support, the core wedge narrows.
- **Adoption cost.** A team adopting Bruinet adopts its page-object rules, its controls, its runner conventions and its tools at once.
- **AI-native competitors.** Products such as Mabl, Testim, Functionize, Momentic, QA Wolf and the Playwright MCP agents
  sell "no page objects at all"; Bruinet sells better page objects. That may be the right bet (deterministic, reviewable),
  but it must be argued, not assumed.

## Competitors, where relevant

- **Playwright (.NET):** wins outright for web only; Bruinet's value on web is the shared model and the bUnit head.
- **Appium, FlaUI, Selenium:** drivers, not competitors; Bruinet sits on them.
- **Reqnroll/SpecFlow:** the closest BDD rival in .NET; Bruinet's Markdown and LSP are friendlier, Reqnroll is far better known.
- **bUnit:** a head here, not a rival.
- **MAUI UITesting / Xamarin.UITest:** largely abandoned; this is Bruinet's opening.
- **Cypress:** web and JavaScript only; not a real overlap.
- **TestComplete, Ranorex:** the commercial cross-technology incumbents (desktop, web, mobile, object repositories,
  recorders); the closest in scope, and paid, which is both a threat and a pricing reference.
- **Xray, TestRail:** test management, not execution; an integration target (traceability of scenarios to requirements), not a rival.
- **AI-driven testing (Mabl, Testim, Functionize, Momentic, QA Wolf, Playwright MCP agents):** compete for the same budget
  with a different promise.

## Recommendations

1. **Pick one wedge and lead the README with it.** My proposal: "UI testing for .NET MAUI, fast in the loop and judged on the
   device", because it is the most novel claim and the least served market. Web, WPF and WinForms become "and also".
2. **Show proof, not features.** Publish one number per claim: suite time on device versus headless, flake rate, page-object
   lines drafted by the Scraper and kept, tests skipped by selection.
3. **State head parity explicitly.** A table of what MauiHeadless and bUnit can and cannot prove compared with the device.
4. **Say how the abstraction keeps each driver's strengths**, and give an escape hatch that does not break the rules.
5. **Move Stride, Presenter and narration out of the headline** into an "extras" section.
6. **Decide the business model early** if an investor is ever the audience: open core (Scraper, selection, SQL replay as paid),
   support, or a hosted agent-testing service. Without one, the honest investor sentence is "a strong open-source project".
