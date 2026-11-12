# Bruinet review: blind spots

## Verdict

The gap that would block adoption outright isn't on your list: nobody outside your organisation can get Bruinet. The repo is private and the packages go only to a private GitHub Packages feed (`release.yml`), so the README's `dotnet tool install -g Bruinet.Cli` fails for any outsider. Among real testing capabilities, the serious gaps are visual regression, web accessibility (WCAG/axe), standard reporting (JUnit, Cucumber Messages, HTML) with failure analytics, and distributed or cloud execution. Teams in regulated, enterprise or design-heavy settings will treat these as table stakes. Contract testing and HTTP replay are not missing: they exist (`Bruinet.Contracts`, `Bruinet.Mocking.Http`) but the README never mentions them, which is a problem of its own. Performance testing, governance suites and test-management integration can wait. Most teams would use other tools for them anyway.

---

## How I checked

I read the README, then searched `docs/` and `src/` for each capability before calling it missing. Where something exists but the README hides it, I say so. I assumed the README's claims are true, as the prompt asked.

## The gaps, ranked

| Gap | Severity | Blocks adoption? |
| --- | --- | --- |
| Distribution: private repo, private feed | Critical | Yes, for anyone outside the organisation |
| Visual regression | High | Yes, for design-heavy web and mobile teams |
| Reporting in standard formats (JUnit/Cucumber/HTML) | High | Yes, for teams with an existing CI or QA dashboard |
| Web accessibility (WCAG rules) | High | Yes, in the public sector, EU (EAA since 2025) and enterprise |
| Parallel and distributed execution, cloud farms | Medium-high | At scale, yes; small teams no |
| Failure analytics and flake history | Medium | No, but it drives churn after adoption |
| Test framework lock-in (xUnit only for UAT) | Medium | Yes, for NUnit/MSTest/TUnit shops |
| Cross-browser and cross-OS matrix | Medium | Partly |
| Test-management integration (Xray, TestRail, ADO Test Plans) | Medium | Yes, in regulated QA organisations |
| Performance testing | Low | No |
| Compliance and governance artefacts | Low-medium | Only in audited industries |

### 1. Distribution: the real blocker

- `.github/workflows/release.yml` says it plainly: "The repo is private, so nothing goes to nuget.org." The packages go to the organisation's GitHub Packages feed.
- The README's Get started clones `exact-mobile-apps/test-tooling` and installs `Bruinet.Cli` globally. Both fail for outsiders.
- There is no public sample feed, no versioned docs site and no community channel. The bus factor is visibly one author.
- **Compared to the competition:** Playwright, Appium, Selenium, FlaUI, Reqnroll and bUnit are all one `dotnet add package` away. TestComplete and Ranorex charge money but ship installers and vendor support.
- **Recommendation:** decide whether Bruinet is a product or an internal platform. If it is a product, publish the stable core to nuget.org (Core, Bdd, Uat, Html.Playwright, Maui, Cli) and keep the rest in preview. If it is internal, rewrite the README as internal documentation and drop the competitive framing.

### 2. Visual regression: genuinely missing

- I found no baseline image, pixel diff, masking or approval workflow anywhere in `src/`.
- The architecture makes this harder than it looks. The bUnit head's "screenshot" is HTML (`docs/guides/bunit.md`). MauiHeadless draws nothing (`maui-headless.md`: "Nothing is drawn"). Visual checks can only run on the device and browser heads, the slow ones.
- That cuts against the README's "same scenario on every head" promise. A `Then the page looks like the baseline` step can't run headless. The head model needs a way to say "this step needs pixels" and skip or defer it on lighter heads.
- **Compared to the competition:** Playwright has `toHaveScreenshot`, Cypress has plugins and Percy, and Applitools Eyes and Chromatic are whole businesses. TestComplete and Ranorex include image checkpoints.
- **Recommendation:** on Playwright, wrap Playwright's own screenshot assertion behind a ControlObject or page-level `MatchesBaseline()`. On FlaUI and Appium, add a perceptual-diff baseline store in the run folder, with masks declared on the page object. Add a head capability flag so a pixel step is reported as "not on this head" instead of silently passing. Offer an Applitools or Percy adapter only later.

### 3. Accessibility: present for one stack, missing for the rest

- **What exists:** `Bruinet.Maui.FlaUI/Bridge/AccessibilityAudit.cs` writes `accessibility-audit.md` on every MAUI run. It lists elements that only a pointer can reach. The idea is clever and genuinely unusual: it turns the test bridge into an accessibility audit.
- **What is missing:** WCAG rule checks for web (axe-core through Playwright is a few lines), contrast, labels, focus order, and landmarks. Android Accessibility Scanner and iOS audits are absent too. bUnit doesn't support accessible-name locators at all (`bunit.md`: "Not supported; use a test id").
- The test-id-first locator stance also works against accessibility. Playwright pushes `getByRole` because a role-based locator doubles as an accessibility check. Bruinet's page objects hide the locator choice, which is fine. But if the house style is test ids, Bruinet loses that free check.
- **Recommendation:** add axe on the Playwright head as an opt-in check per page object (`page.Accessibility.Assert(rules)`), with the results in the run folder. Make role and name locators first-class wherever the head can resolve them. This matters most in the EU after the European Accessibility Act.

### 4. Reporting and analytics: weak, and admitted to be

- **What exists:** an artifact layout (`TestResults/<run-id>/manifest.json`, `summary.md`), a `__result.json` per scenario, `.trx` from xUnit, a coverage upload in CI, test timings and the trace MCP tools.
- **What is missing:** `coming-from-reqnroll.md` says it outright: "Cucumber Messages and the HTML report: not yet." There is no JUnit XML, no Allure adapter and no shareable HTML report. Nothing tracks history, flake rate, duration trends or failure clustering across runs.
- Bruinet has unusually rich failure data: traces per step, `sinceGreen`, `why`, the step's log through `Bruinet.Logging`. But it only reaches an agent or an IDE. A QA lead in a browser sees a `.trx`.
- **Compared to the competition:** Playwright has its HTML report and trace viewer. Cypress Cloud, ReportPortal, Allure, Xray and TestRail all have dashboards. The trace viewer is the bar Bruinet has to meet.
- **Recommendation:** emit Cucumber Messages first, since Xray, Allure, ReportPortal and the Cucumber HTML formatter all read it. Then build a static HTML report from the run folder, with screenshots, the per-step trace and the narrated video linked. Keep one result per run in a small store so flake rate and the slowest scenarios are visible.

### 5. Parallelization, sharding and cloud farms

- **What exists:** xUnit collection parallelism, `repeat-run.ps1 -VaryOrder` to surface order dependence, SQL replay and HTTP replay, which remove shared state (a real enabler), and test selection from a touch map.
- **What is missing:** I found no sharding across machines in `src/` and no duration-balanced dealing as a Bruinet feature. The consuming repo has a `durations.json` but builds its own. There is no device-pool management and no BrowserStack, Sauce Labs, LambdaTest or AWS Device Farm configuration. Appium could point at a remote hub, but nothing is documented or tested for it. iOS needs a Mac with a local simulator.
- FlaUI and the Windows narrator tie the desktop and MAUI-Windows heads to interactive Windows sessions. CI for those heads means self-hosted runners with a desktop session, which is hard to scale.
- **Recommendation:** ship sharding (`bruinet shard --index i --total n`, balanced by recorded durations) as part of the CLI. Document a cloud Appium capability profile for one vendor. Leave the farm itself to vendors.

### 6. Lock-in and platform breadth

- UAT is xUnit only (`coming-from-reqnroll.md`: "MSTest, NUnit, TUnit: xUnit only"). Reqnroll's main pitch is runner neutrality, so this blocks part of the very audience the migration guide targets.
- Reqnroll lifecycle hooks are missing: `[BeforeTestRun]`, `[AfterFeature]` and others are "not yet". Migrating suites rely on them.
- The cross-browser matrix isn't shown. Playwright supports Firefox and WebKit, but nothing documents running a suite across browsers as one dimension.
- Much of the tooling is Windows-first: the narrator's default voice, Presenter, the VS extension and FlaUI. Web teams on macOS and Linux get a thinner product.

### 7. Things not on your list

- **Authentication and session reuse.** Nothing like Playwright's `storageState`, or a documented way to log in once per run. Every real web suite needs this.
- **Network conditions:** offline, slow 3G, flaky backend. WireMock covers faults, but no step vocabulary for "when the network drops" ships across heads, and mobile apps live or die on it.
- **Device state:** permissions dialogs, deep links, push notifications, background and foreground, rotation, locale and time zone, dark mode. Some exists (mock sensors), but it isn't a documented capability matrix per head.
- **Localization and RTL.** I found no pseudo-localization run or RTL layout checks.
- **Security in tests.** HTTP replay redacts secrets (good). There is nothing for recorded SQL or video: narrated recordings of real screens can capture personal data.
- **Head parity evidence.** Bruinet's core claim is that one scenario runs everywhere. Nothing reports which scenarios passed on which heads and where the heads disagree. A head-parity matrix in the report would turn the novel idea into proof.
- **Mutation testing of the tests** (Stryker) and a quality score for page objects. You already have `TraitCheck` and a test guard, so this is a natural extension.
- **Upgrade tooling for consumers.** You have a change list and an `[Experimental]` boundary (good). With roughly 40 packages and one author, a published support matrix (which drivers and OS versions are tested) matters more than features.
- **Self-healing locators.** Bruinet deliberately rejects them, like it rejects retries (AD-009). That stance is defensible, but say so in the README: buyers of AI testing tools will ask.

## Strengths that reduce the gaps

- **Contract testing exists:** `Bruinet.Contracts` checks live exchanges against the DTOs the app reads, plus hermetic checks of writes against a mock (AD-035). It isn't Pact (consumer-driven, broker, provider verification), but it covers the "UI broke because the API changed shape" case, which is what UI teams actually hit. Put it in the README.
- **HTTP replay** (`Bruinet.Mocking.Http`) with secret redaction exists. It is also missing from the README.
- **Governance basics** exist: `Bruinet.Traits` (tier, owner, journey, covered row), `TraitCheck`, a CI guard against removed tests, API change lists, journey traceability in the Todo sample. That is more than most open-source frameworks ship.
- **The flake policy is principled:** flakes stay failures, and `-VaryOrder` and repeat runs surface them.

## Novel versus common

- **Common:** page objects, Gherkin-style scenarios, artifacts per run, WireMock, multi-driver abstraction (Selenium wrappers, Appium's own cross-platform pitch, TestComplete and Ranorex object repositories).
- **Uncommon to novel:** the same Markdown scenario on device, headless, bUnit and service heads; SQL replay for UI tests; the touch-map test selection for UI suites; narrated scenario video; an MCP server with `why`/`sinceGreen`; the accessibility audit derived from the test bridge.

Most of the missing capabilities are where the commercial competitors make their money: reporting, visual, cloud and analytics. You don't need to beat them there. You need adapters, so that Bruinet's novel core can sit beside Applitools, Allure/Xray and a device farm instead of replacing them.

## What to do, in order

1. Decide public versus internal, then fix distribution and the README to match.
2. Add the hidden features to the README: contracts and HTTP replay.
3. Emit Cucumber Messages and JUnit, then a static HTML report from the run folder.
4. Add visual baselines on Playwright, with a head capability flag so pixel steps are honest on headless heads.
5. Add axe on the Playwright head, and make role and name locators first-class.
6. Add sharding in the CLI, and document a cloud Appium profile.
7. Report head parity: which scenario passed on which head.
8. Later: NUnit/MSTest/TUnit for UAT, Reqnroll's run hooks, performance budgets (Lighthouse or timings thresholds), and test-management sync.
