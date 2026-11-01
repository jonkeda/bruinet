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
- a project sets `TreatWarningsAsErrors` to false, or adds an analyzer rule to `NoWarn` instead of the globalconfig;
- a rule is turned off without a reason on its line;
- a UI test file is added to the RS0030 exemptions to make it build, rather than given the control member it lacks;
- generated code (`.gen.cs`) is fixed by hand instead of in the generator.
