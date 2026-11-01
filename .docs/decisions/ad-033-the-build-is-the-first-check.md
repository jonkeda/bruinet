# AD-033: The Build Is the First Check

Every Bruinet project builds with the SDK's analyzers at `latest-recommended`, code style from
`.editorconfig` and Meziantou's analyzers, all as errors. `Quality.props` turns them on; the root's
and `src/`'s `Directory.Build.props` import it. Frainet stands alone and is left out.

| Decision | What holds |
| --- | --- |
| **A rule is on or off, never a warning** | Under `TreatWarningsAsErrors` a finding stops the build. No project turns that off; a rule that does not fit is turned off for all of Bruinet. |
| **Off with a reason, in one place** | `Quality.globalconfig` lists every rule turned off, each with its why on its line. `Quality.Tests.globalconfig` adds the few for test projects, `Quality.Legacy.globalconfig` the newer-API rules for projects that also target netstandard2.0 or .NET Framework. |
| **What stays on finds defects** | Culture (CA1305, CA1310), lost inner exceptions (MA0054), ignored results (CA1806, MA0134), wrong docs, platform reach (CA1416), token forwarding (CA2016) and the rest of the recommended set. Naming the code base chose, style, and performance hints with no measured cost are off. |
| **Machine output is invariant** | What Bruinet writes, compares or types uses `CultureInfo.InvariantCulture` and ordinal comparison; only text a person reads in a sample app uses the current culture. |
| **The UI suites' forbidden APIs are banned** | Projects named `*UITests*` or `*.Uat` get `BannedSymbols.UiTests.txt` (RS0030): sleeps and delays, the contexts' `Driver`, `AppElement`, `Find*`, `ContainerRoot` and `TryGetItemRoot`, each with what to use instead. `.editorconfig` exempts by path only what forbidden-apis.md already exempts: library code, the driver and bridge framework tests, fixtures and their host loops, row plumbing, and the named known debt. |
| **Whitespace is checked, line ends are not** | `dotnet format whitespace Bruinet.sln --verify-no-changes` runs in CI after the build; run it without `--verify-no-changes` to fix. `.editorconfig` sets no `end_of_line`: git sets line ends on checkout, so a check of them fails on another OS. |
| **A known vulnerability fails the restore** | `NuGetAuditMode` all at level low, transitive packages too. An accepted advisory is a `NuGetAuditSuppress` by its URL with the reason beside it, never `NoWarn` on NU190x. Every package comes from nuget.org through `packageSourceMapping`; versions are central. Dependabot proposes updates weekly. No lock files: the MAUI and Android workloads make them change with the SDK, and central versions already pin what Bruinet asks for. |
| **Secrets and security beside CI** | `.github/workflows/security.yml`: gitleaks over the whole history on every push and pull request; CodeQL's C# security-and-quality queries weekly (it needs code scanning on for the repository). A placeholder gitleaks flags goes in `.gitleaksignore` by fingerprint, with what it is. Run `gitleaks git . --redact` locally before a push. |
| **Tests are not taken away silently** | `tools/Scripts/check-test-guard.ps1` fails when a change deletes a test file, has fewer tests or scenarios, adds a skip, or has fewer assertions; `-Reason` makes the findings notes. It notes assertions rewritten beside a code change. CI runs it on pull requests, the reason taken from a "Tests removed because:" line in the description; run it before a commit. |
| **A shipped package's API is declared** | Every packable library under `src/` has `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` (PublicApiAnalyzers): a public member the files do not list, or a listed one that is gone, stops the build (RS0016, RS0017). API only the .NET targets have goes in `PublicAPI/net/`. `tools/Scripts/update-public-api.ps1 <project>` updates the unshipped file, generated members included; its diff is the API change to review. Until the first release everything is unshipped; the release moves it to shipped and gives package validation its baseline version. |
| **The invariants that the code shows are tests** | `Bruinet.Core.Tests` `ArchitectureTests`, beside `SolutionTests`: Core reaches no platform (AD-001); Bdd reaches no UI and no xUnit (AD-021); TestData depends on nothing (AD-026); only Bruinet.Uat has Uat names (AD-021); Bruinet.Maui.FlaUI has no physical input (AD-005); the controls have no async members (AD-013). References are followed through the projects they bring; a private package does not count. No new project and no library: the rules read `.csproj` files and sources. |
| **Mutation testing at the gate, as a report first** | `tools/Scripts/mutate.ps1` runs Stryker.NET (a local tool, `.config/dotnet-tools.json`) on the files a change touched in Core, Bdd, TestData, Mocking.Data and Recording, against their unit tests; minutes, not part of the build or CI. A surviving mutant is a test to add or code no test needs. It reports by default (the scores were 14 to 41%); `-Break` makes it fail once they are up. The drivers and controls are left to the UI suites, which Stryker cannot run. |
| **The generator's output is approved** | `GeneratedFilesTests` in `Bruinet.Cli.Tests` regenerates every `.tpl.cs` in the repository and compares it with the committed `.gen.cs`: a hand edit, or a generator change nobody regenerated, fails. No snapshot library: the committed files are the snapshots. |
| **A one-off exception is a pragma with its reason** | `#pragma warning disable` around the line, with a comment saying why: an ADO.NET contract, a secret kept out of a message, a fixture's extension point. |

**Why:** with coding agents writing much of the code, the cheapest check is the one that runs on
every build and answers with a line to fix. Run on the code base, the analyzers found about 7,800
findings: style rules that disagree with how Bruinet is written, and about 600 real ones, most
culture-sensitive formatting on a Dutch machine. The first group was turned off and the second
fixed, so the build starts clean. Evidence: [.my/checks](../../.my/checks/README.md).

**Ruled out:**
- warnings that do not stop the build: an agent and a person both learn to ignore them;
- a suppression file per finding, or a baseline: the old findings were fixed or their rule turned off;
- Roslynator and SonarAnalyzer beside Meziantou: more overlap than new findings for now.

**Broken when:**
- a project sets `TreatWarningsAsErrors` to false, or adds an analyzer rule or an NU190x warning to `NoWarn`;
- a rule is turned off without a reason on its line;
- a UI test file is added to the RS0030 exemptions to make it build, rather than given the control member it lacks;
- generated code (`.gen.cs`) is fixed by hand instead of in the generator.
