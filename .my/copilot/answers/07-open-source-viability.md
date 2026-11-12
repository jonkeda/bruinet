# Bruinet review: open source viability

## Verdict

Released as it stands, Bruinet would get stars from people who admire it and very few teams who adopt it: it is a
one-person, 137k-line platform with eight drivers, a BDD dialect, an MCP server, a scraper, SQL replay and a video
narrator, and its front door asks a stranger to clone and build all of it. The parts that are really new (one page-object
model across MAUI, WPF, WinForms, web and Stride; MauiHeadless; the same Markdown scenario on a device, in process and
below the UI; SQL replay; agent tooling that returns structured failures) are good enough to win a niche, but the niche
is .NET MAUI and Windows desktop teams, not "everyone who tests UI". Reaching 1,000 active users is possible only if it
ships as a few small, separately useful packages on nuget.org, with MauiHeadless and the MAUI page-object layer as the
entry point, a five-minute `dotnet new` path, and a second maintainer. Without those, expect a few hundred stars, a
handful of real users, and an author doing support alone.

## What a new user sees today

### The first ten minutes, honestly

1. **Minute 0-1: the README.** It is dense and confident. In the first screen the reader meets heads, page objects,
   ControlObjects, MauiHeadless, bUnit, the service head, the Scraper, SQL replay, test selection, narrated recordings,
   language servers, an MCP server and agent skills. They cannot tell what Bruinet *is for* in one sentence. "A UI test
   framework for .NET" undersells it; the feature table oversells the breadth.
2. **Minute 1-3: Get started.** The first command is `git clone ... && dotnet build Bruinet.sln`. That is a contributor's
   path, not a user's. The clone URL points at `exact-mobile-apps/test-tooling`, which is private, and `release.yml`
   pushes packages to that organisation's GitHub Packages feed, "so nothing goes to nuget.org". So
   `dotnet tool install -g Bruinet.Cli` and the Quick Start's `dotnet add package Bruinet.Core` fail for anyone outside.
   On release this is fixable in a day, but as written the first two commands a stranger runs do not work.
3. **Minute 3-6: the Quick Start.** It lists eight packages to add "for package consumers" (MAUI, FlaUI, Playwright, WPF,
   WinForms, Mocking, Uat), shows a `LoginPage : PageObjectBase<LoginPage>` taking a `MauiTestContext`, and a test using
   an `AppFixture` that is never defined. The user does not learn how the app is launched, how the context is made, or
   which head they are on. This is the step where most evaluators close the tab.
4. **Minute 6-10: the Todo sample.** It is excellent as a reference and hopeless as a first sample: eleven test projects,
   five tiers, WireMock, SQLite, journeys, a mixed Windows/web/Android project, video scripts. It answers "what does a
   mature suite look like", not "how do I get one green test".

What they needed instead: a template (`dotnet new bruinet-maui`, `dotnet new bruinet-blazor`) that produces one page
object, one test and one `.feature.md` against a tiny app, green on `BRUINET_HEAD=headless` or `bunit` with no device,
browser or Windows dependency, in under five minutes.

### Learning curve

The concepts are sound but numerous, and several are Bruinet's own words: head, ControlObject, page object with a typed
`TParent`, container, component scope, test set, touch map, phrase, callable step, `Using | Users`, gate, tier, journey.
A Playwright or Selenium user knows page objects; a Reqnroll user knows Given/When/Then; nobody knows the rest. The
`coming-from-reqnroll.md` guide is the right instinct and should exist for Playwright, FlaUI and Appium users too.

The house style raises the curve further. The docs and changelog are written in a compressed, idiosyncratic register
("A suite built against an older Bruinet now reports that as its reason instead of failing every tool") that is precise
to the author and opaque to a newcomer. The rules ("never a driver or a sleep", "do not add FluentAssertions", banned
symbols for UI tests) are defensible, but a framework that forbids things before it has earned trust reads as
opinionated in the off-putting sense.

Estimated time to a first green test for a competent .NET developer with good docs: 30-60 minutes for web or headless
MAUI, half a day for Appium on Android, a day for a team's own WPF app with custom controls. Today, without templates
and with the package problem: most will not get there.

## What is genuinely novel, and what is common

| Area | Common in the industry | What Bruinet adds |
| --- | --- | --- |
| Page objects, waits, no sleeps | Every serious Selenium/Playwright shop | ControlObjects as a first-class, typed layer across stacks |
| Gherkin scenarios | Reqnroll, SpecFlow, Cucumber, Gauge | Markdown files, bound to page-object members, checked in the editor and at build |
| Cross-stack driver abstraction | Appium (mobile and Windows), Ranorex, TestComplete | One C# model over FlaUI, Appium, Playwright, Selenium and Stride, open source |
| Headless component tests | bUnit for Blazor | **MauiHeadless**: nothing equivalent for MAUI that I know of |
| Same scenario at several tiers | Rare; Serenity's screenplay comes closest | Device, in-process and below-the-UI heads from one file |
| DB isolation | Testcontainers, Respawn, fakes | **SQL replay** of a recorded run; unusual, and fragile in the obvious ways |
| Test impact analysis | Commercial tools, Azure DevOps TIA (retired), Launchable | A touch map in an open source UI framework |
| Recording to page objects | Playwright codegen, Selenium IDE, Ranorex/TestComplete recorders | Drafts typed page objects and controls, not scripts |
| AI tooling | Playwright MCP, many AI test startups (Testim, mabl, Functionize, QA Wolf) | An MCP server over a *structured* suite: bound steps, traces, "why did this go red since the last green" |
| Narrated video | Rare outside demo tooling | Scenario-to-narrated-video; novel but a nice-to-have |

The real novelty is the combination: a typed page-object and scenario layer that stays the same while the head moves
from device to in-process to service. Each part alone has a competitor; the stack does not, in .NET.

## Strengths for an open source release

- **A real gap in .NET MAUI.** Xamarin.UITest is dead, Microsoft's MAUI UI testing story is "use Appium yourself", and
  MAUI developers are short of anything structured. MauiHeadless plus MAUI page objects fills a hole people complain about.
- **Desktop Windows testing is underserved.** FlaUI is a driver; teams with large WPF and WinForms apps build their own
  page-object layer every time. Bruinet's WPF/WinForms layer is the thing they keep rewriting.
- **Quality of engineering.** Analyzers as errors, a declared experimental surface (`BRUINET010`), a versioning and
  breaking-changes policy, a changelog, CI, security workflow, central package management, source mapping. This is
  more discipline than most OSS projects have at 1.0.
- **Agent-ready from the start.** MCP tools that return failure kind, locator, binding and the diff since green are
  exactly what coding agents need, and very few frameworks have them. This is the most marketable story in 2026.
- **MIT licence**, which removes the first enterprise objection.

## Weaknesses and risks

- **Bus factor of one.** 1,129 of 1,133 commits are one author, over under a year. Adopters of a test framework are
  betting their suite on it for years; they will look at the contributor graph first.
- **Ownership.** The repository lives in a company organisation (`exact-mobile-apps`) and was moved from a company
  product ("Moved from Exact Construction" in the changelog). Before release the employer must agree to the release
  and the licence, and say whether it will keep funding the work. Unclear ownership kills enterprise adoption faster
  than any missing feature.
- **Scope.** Eight driver stacks, a BDD engine, a CLI, two IDE extensions with language servers, an MCP server, a
  Scraper, SQL replay with EF and System.Web hooks, a screen recorder, three voice backends, a desktop Presenter, and
  now Frainet (a "dark factory" spec tool) in the same repo. Each driver stack alone is a maintenance burden: Appium,
  Playwright, MAUI and Stride all break on their own schedules. One maintainer cannot keep eight stacks green in public.
- **Windows gravity.** FlaUI, the Windows voice, the Visual Studio extension, PowerShell everywhere. A macOS or Linux
  developer evaluating the web head will feel second-class.
- **Its own BDD dialect.** `.feature.md` is a better format, but it splits Bruinet from the Gherkin ecosystem (Cucumber
  reports, Xray and TestRail importers, Reqnroll's plugins, LivingDoc). Teams with Xray or TestRail traceability will
  ask how scenarios reach their test management tool; there is no answer in the README.
- **Too many packages.** Around 40 projects in `src`. A user cannot tell which three they need.
- **Lock-in perception.** "Never a driver" is a strength for maintainability and a red flag for evaluators: when they
  hit a control Bruinet does not model, they want an escape hatch, and the rules say there is none.
- **SQL replay will generate bug reports** out of all proportion to its users: query shape changes, provider quirks,
  non-deterministic SQL. Ship it later, or mark it experimental.

## Areas likely to attract contributors

- **ControlObjects for specific control libraries**: Syncfusion, Telerik, DevExpress, CommunityToolkit, MudBlazor,
  Radzen, Fluent UI Blazor. Small, self-contained, and each one brings that library's users. This is the best
  contributor funnel, and the reason to document "how to write a control" as the second guide.
- **MauiHeadless** fixes and control coverage: MAUI developers feel the pain and can test their fix in-process.
- **Scraper patterns** for more web frameworks and design systems.
- **MCP tools and agent skills**: the most fashionable area, and low-risk for contributors.
- **Reporting adapters**: Allure, Xray, TestRail, Azure DevOps test plans. Users need them; contributors can write them
  without knowing the core.

## Areas likely to scare users away

- The clone-and-build start and packages that are not on nuget.org.
- The Todo sample as the first thing they open.
- The README's breadth: narrated videos and Stride next to WPF reads as a personal laboratory, not a product.
- A house writing style that is hard to scan, and rules stated as prohibitions.
- The submodule-style usage the author's own apps use; it signals "this is meant to be vendored, not consumed".
- Any sign that the IDE extensions and CLI must match the package version exactly.
- Frainet in the same repository.

## Competitors, by who Bruinet would take users from

| Competitor | Where Bruinet wins | Where it loses |
| --- | --- | --- |
| Playwright for .NET | Typed controls, Markdown scenarios, bUnit head, cross-stack | Ecosystem, docs, trace viewer, codegen, Microsoft behind it; web teams have no reason to switch |
| Appium + hand-rolled page objects (MAUI) | Everything above the driver; the headless head | Appium's community and device clouds (BrowserStack, Sauce) |
| FlaUI + hand-rolled layer (WPF/WinForms) | A ready page-object and control layer | Nothing, except trust |
| Reqnroll | Markdown, editor binding, multi-tier heads | Gherkin ecosystem, reports, existing suites, a funded team |
| bUnit | Same page objects as the browser tests | bUnit alone is simpler for component tests |
| TestComplete, Ranorex | Free, code-first, in source control, agent tooling | Recorders, object repositories, vendor support, non-programmer testers |
| Xray, TestRail | Not competitors: integration targets | Bruinet has no bridge to them |
| AI testing products (mabl, Testim, QA Wolf, Playwright MCP agents) | Deterministic, code-owned tests an agent can repair with structured failures | They sell "no code"; Bruinet still requires a .NET developer |
| Cypress, Selenium | Not really in the .NET MAUI/desktop market | Web-only teams will never look |

## The community it would grow from

Not web testers: Playwright owns them. The likely first community is **.NET MAUI developers**, then **enterprise WPF
and WinForms teams** maintaining large line-of-business apps, then **Blazor teams** who already use bUnit. Its natural
venues are the MAUI and Blazor GitHub discussions, the .NET community standups, the MAUI and Uno Discords, and
conference talks at NDC and .NET Conf. Stride users are too few to matter, and the narrator and Presenter appeal to
consultancies doing UAT with clients, a small but loyal group.

## What would need to happen for 1,000 active users

"Active" means a suite that runs in CI at least weekly. In my estimate that needs roughly 10-20k NuGet downloads a month
of the entry package, 150-300 teams, and a few dozen contributors. Concretely:

1. **Settle ownership and funding** with the employer, in writing, and say it in the README.
2. **Publish to nuget.org** with consistent versions, and publish the CLI as a public .NET tool and the extensions on the
   marketplaces.
3. **Split the product.** Lead with three things: `MauiHeadless` (standalone, already Bruinet-free), `Bruinet.Maui` and
   `Bruinet.Html` page objects, and `Bruinet.Bdd` scenarios. Move the Scraper, SQL replay, test selection, narrator,
   Presenter and Stride to "labs" packages with an experimental label, and Frainet to its own repository.
4. **Rewrite the front door.** One sentence of positioning ("typed page objects and Markdown scenarios for .NET MAUI,
   Blazor and Windows desktop, that run on the device, in process, or below the UI"), one code sample, one command.
5. **Templates and a five-minute path**: `dotnet new bruinet-maui-headless`, `bruinet-blazor`, `bruinet-wpf`, each green
   with no device and no Windows-only dependency where possible.
6. **Samples in steps**: hello-world per stack (one page, one test, one scenario), then a "custom control" sample, then
   the Todo sample as the advanced reference. Add "coming from Playwright", "coming from FlaUI" and "coming from Appium"
   beside the Reqnroll guide.
7. **Docs in plain English**, edited by someone who is not the author, with a concepts page that defines head,
   ControlObject and phrase before anything uses them, and an escape hatch documented for the control Bruinet does not
   model.
8. **A Gherkin bridge**: import `.feature` files, and export results to Cucumber JSON or JUnit so Xray, TestRail and Azure
   DevOps can consume them.
9. **Cross-platform CI proof**: the web and headless heads green on Linux and macOS in public CI.
10. **A second and third maintainer** before the 1.0 announcement, ideally from a company that uses it, plus a
    `good first issue` list of ControlObjects for popular control libraries.
11. **Launch around MauiHeadless and the agent story**: a blog post and a talk on "headless MAUI tests your coding agent
    can fix", with a short narrated video made by Bruinet itself. That is the one demo no competitor can give.
12. **Stay small for a year.** Prune stacks that nobody outside uses after twelve months.

## Recommendations, in order

1. Decide the owner and the licence holder before anything else.
2. Fix the package feed and the Get started section; nothing else matters until a stranger's first command works.
3. Release MauiHeadless first, alone, as the wedge; it is useful without the rest and has no direct competitor.
4. Cut the visible scope of 1.0 to MAUI, Blazor/web and WPF/WinForms with page objects and scenarios; label the rest.
5. Write templates, three hello-world samples and three "coming from" guides.
6. Recruit a co-maintainer and seed contributor issues around ControlObjects for third-party control libraries.
7. Add Gherkin and JUnit/Cucumber-JSON interop so test-management users are not shut out.
8. Market the agent tooling as the differentiator, with a working demo, not a feature row.
