# Checks for AI-supported .NET development

What teams that build .NET with coding agents use to keep quality, besides tests, and which of it
suits the loop: the agent works unattended and the checks at the end decide whether its work
stands. Written 2026-11-01 from general industry practice, not from measurements of our own.

## The outcome

Teams that do this well do not trust any single check, and least of all the agent's own tests.
They stack cheap deterministic checks that run on every loop, a few costlier ones at the gate, and
an independent review that is not the agent marking its own work. Tests stay the core; the rest
exists because an agent writes code fast, writes tests that agree with its code, and repeats
itself.

## What an agent gets wrong that tests miss

- Tests that pass but assert little, or were changed to match the new behaviour.
- Duplicated code instead of a shared helper, and growing methods.
- Layer and dependency rules crossed (a web type in the domain, a forbidden API).
- A public API changed by accident, which matters for a library such as Bruinet.
- Packages that do not exist or are vulnerable, and secrets in config.
- Work that is correct but not what was asked.

## The checks teams use

Order is when we would build it: 1 first and cheapest; empty means not at all.

| Check | .NET tools | Catches | Order |
| --- | --- | --- | --- |
| Compiler and analyzers as errors | `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, Meziantou, Roslynator, SonarAnalyzer.CSharp | sloppy code, async and disposal mistakes, style drift | 1 |
| Forbidden APIs | Microsoft.CodeAnalysis.BannedApiAnalyzers (`BannedSymbols.txt`) | `Thread.Sleep`, `DateTime.Now`, driver calls outside page objects | 2 |
| Format | `dotnet format --verify-no-changes` | noise in diffs | 3 |
| Architecture tests | NetArchTest.Rules, ArchUnitNET | layers and references crossed | 8 |
| Coverage of the diff | coverlet + ReportGenerator, a diff-coverage gate | new code without tests |  |
| Mutation testing | Stryker.NET, on changed files only | tests that would pass whatever the code does | 9 |
| Approval and snapshot tests | Verify | outputs that changed silently (generated code, reports, page objects) | 10 |
| Property-based tests | FsCheck, CsCheck | edge cases nobody wrote | 12 |
| Public API | PublicApiAnalyzers (`PublicAPI.Shipped.txt`), `EnablePackageValidation` | breaking changes to shipped packages | 7 |
| Supply chain | NuGetAudit, central versions, package source mapping, lock files, Dependabot or Renovate | vulnerable and invented packages | 4 |
| Security scanning | CodeQL, gitleaks or GitHub secret scanning | injection patterns, keys in commits | 5 |
| Complexity and duplication | CA1502/CA1505 thresholds, Sonar's duplication | the agent copying instead of reusing | 11 |
| Independent review | a second agent with the diff and the brief only (Copilot review, CodeRabbit, a Claude review agent), a person at the gate | the wrong thing built well | 13 |
| Guard on tests | flag diffs that change a test and its code together, or delete or skip tests | tests bent to fit | 6 |

## Progress

| Order | Check | State |
| --- | --- | --- |
| 1 | Analyzers as errors | built 2026-11-01 (AD-033): every project clean; about 600 findings fixed, the rest's rules off with why |
| 2 | Banned APIs | built 2026-11-01 (AD-033): the UI and UAT suites; 74 existing uses all in paths forbidden-apis.md exempts, so exempted by path |

## How teams arrange them

The common shape is three rings, by cost. The inner ring runs on every agent turn, often as a hook
before the agent may stop: build with analyzers, format, the tests the change reaches. The step
ring runs when a piece is done: architecture tests, diff coverage, approval tests, the API check.
The gate ring runs before the work is accepted: the full suites, mutation testing on the changed
files, security and dependency scans, and the independent review.

What makes it work for agents is that every check is a command with a pass or fail and a readable
reason, so the agent can fix its own failures before a person looks. A check that only a person
can read belongs at the gate, not in the loop.

## What we have and what is missing

Bruinet already has warnings as errors, nullable, an `.editorconfig`, CI with unit and headless
suites, coverlet, and test selection that runs what a change reaches. Tomorrow has architecture
tests, an architect review agent and the `/step` gates. Neither has analyzer packages beyond the
SDK's, banned APIs, diff coverage, mutation testing, an API baseline for Bruinet's packages, or a
guard on test changes.

## Proposal

1. Turn up the analyzers in `Directory.Build.props` (`AnalysisLevel`, `EnforceCodeStyleInBuild`, Meziantou) and add BannedApiAnalyzers with the rules we already enforce by review: cheap, every loop, catches the most.
2. PublicApiAnalyzers and package validation on Bruinet's shipped projects, since a first preview release is planned.
3. A loop hook that runs build, format and the selected tests before the agent may finish, using `Select-Tests.ps1` and Bruinet's test selection.
4. Stryker.NET on the changed files at the gate, starting with `Tomorrow.Game` and Bruinet.Core, where the logic is: the best answer to "the agent's tests prove nothing".
5. A test-change guard.
6. Last, an independent review agent that sees the brief and the diff, not the agent's reasoning: the largest recurring token cost (a review reads 30-150k tokens), so it waits until the cheap checks run.

Diff coverage is left out: mutation testing says more about the same tests. Open: whether Sonar is worth a service (proposal: no, the analyzer packages cover most of
it locally).
