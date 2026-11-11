# Quality settings: review and proposals

Written 2026-11-11 at `b2ea4570`. Read from the source only; nothing was built or run, so "would fire" below is a reading, not a measurement.

## Outcome

The settings are in good order: no project turns `TreatWarningsAsErrors` off, none disables `Nullable`, there is no `WarningsNotAsErrors`, and all 20 pragmas in `src` and `tools` carry a reason.
Legacy is small: six rules, six projects, and by my reading it does real work in one of them.
The debt is elsewhere: 19 complexity suppressions that all wait for the same fix, and three places where AD-033 says more than the build or CI holds.

## Who is on Legacy

`Quality.props:34-36` adds `Quality.Legacy.globalconfig` to any project that targets netstandard or .NET Framework. It turns off six rules (`Quality.Legacy.globalconfig:5-10`): CA1510 to CA1513 (the `ThrowIf…` helpers), CA2249 and CA2263.

| Project | Targets | Does Legacy matter |
| --- | --- | --- |
| `src/Bruinet.Mocking.Data` | netstandard2.0, net8, 9, 10 (`.csproj:5`) | Yes: `SqliteTemplate.cs:31` and `:36` would get CA1510 on the .NET targets |
| `src/Bruinet.Mocking.Data.SystemWeb`, `src/Bruinet.Mocking.Analyzers` | net48; netstandard2.0 | No: the newer API is absent, so the rules have nothing to offer |
| `tools/Bruinet.Bdd.VisualStudio`, `…CodeLens`, `tests/Bruinet.Mocking.Data.Net48.Tests` | net48 | No, for the same reason |

## The suppressions, sorted

| What | Where | Verdict |
| --- | --- | --- |
| 20 `#pragma warning disable` (15 `src`, 5 `tools`) | CS8765 ×4 in `Mocking.Data` (`LoggingDbConnection.cs:19,142`, `ReplayingDbConnection.cs:21,137`); CA2201+MA0012 `ReplayDataReader.cs:59`; EF1001 `SpatialReplay.cs:1`; MA0054 `SpeechSecrets.cs:252`; CA1507 `UatRuntime.cs:139`; MA0056 `WpfTestFixtureBase.cs:40`, `WinFormsTestFixtureBase.cs:40`; CS0618 ×5 in `MauiHeadless`; CS0067 ×2, VSTHRD003 ×2, VSTHRD010 in the VS extension | Still needed; each has its reason |
| 2 of those are file-wide, with no restore | `SpatialReplay.cs:1`, `MauiHeadless/Hosting/HeadlessBehaviors.cs:6` | Small fix: AD-033:25 says "around the line" |
| 17 `SuppressMessage`, all CA1502 "Known debt" (13 `src`, 4 `tools`); 2 more in `samples` | e.g. `Bruinet.Bdd/BddBinder.cs:249`, `Bruinet.Cli/Lsp/BddLanguageServer.Config.cs:101` | Each needs its method split first |
| `NoWarn` CS1591, CS1710 | `src/Directory.Build.props:15` | Needed; CS1591 until the public summaries exist |
| `NoWarn` BRUINET010 | `Quality.props:16` | Needed, but it reaches tests and samples too, which are consumers |
| `NoWarn` NU5110, NU5111 | `tools/Bruinet.Cli/Bruinet.Cli.csproj:13` | Needed; reason given |
| `NoWarn` BRUINET001 | `tests/Bruinet.Mocking.Tests/…csproj:6` | Reasoned, but an analyzer rule in `NoWarn` is what AD-033:39 calls broken |
| RS0030 off by path, 5 sections | `.editorconfig:63-77` | Needed; `:76-77` names three test files as debt to move onto controls |
| 23 rules off with a group reason only | `Quality.globalconfig:37-53`, `Quality.Legacy.globalconfig:5-10` | Small fix: AD-033:10 and :40 ask a why on each line |
| CA1707, CA1051, MA0025 off everywhere | `Quality.globalconfig:7,31,34` | Their reasons name tests and samples only; a build tells whether `src` can have them back |

The test projects hold one pragma (`tests/MauiHeadless.Tests/Framework/HeadlessAppTests.cs:113`), no `SuppressMessage`, the one `NoWarn` above and one rule in `Quality.Tests.globalconfig:4`.
The only `TreatWarningsAsErrors=false` is a script's own build that reads RS0016 and RS0017 from the log (`tools/Scripts/update-public-api.ps1:33`); that is fine.

## Where AD-033 says more than is held

- **Code style is not an error.** AD-033:3-4 says code style from `.editorconfig` runs as errors; every style and naming rule there is `suggestion` (`.editorconfig:36,47-58`), and CI's format step is whitespace only (`ci.yml:76`).
- **The test guard and the API-change check run on pull requests only** (`ci.yml:24`); a direct push skips both.
- **CI runs for `main` only** (`ci.yml:4-7`); on any other branch only gitleaks runs (`security.yml:6`).
- **CodeQL does not fail anything.** It runs weekly or by hand (`security.yml:29`) and reports to code scanning, which must be on for the repository.
- **The complexity count is out of date.** AD-033:23 says 17 methods and "no new one is added that way"; the repository has 19, two added the next day for the VS extension (`389dc6b4`).
- Mutation testing and coverage are outside CI's gates, as AD-033:20-21 says; package validation has no baseline yet (`src/Directory.Build.props:17-19`). Both are as written.

## Proposals

1. Narrow Legacy to `Bruinet.Mocking.Data`, or give `SqliteTemplate` a null check both targets build and delete the file. Small.
2. Give each of the 23 group-reason lines its own why, or change AD-033 to allow a reason per group. Small.
3. Decide code style: raise the rules you mean to `warning` in `.editorconfig`, or take "code style" out of AD-033's first sentence. Small to medium.
4. Move BRUINET001 for `Bruinet.Mocking.Tests` from `NoWarn` to a globalconfig line, and scope BRUINET010 to `src` and `tools`. Small.
5. Correct AD-033's count to 19 and add a remaining row for the splits; add the two restores. Small.
6. Try CA1707, CA1051 and MA0025 in `Quality.Tests.globalconfig` only, in one build. Small, if `src` comes out clean.
7. Run the test guard and API check on pushes too, if work keeps landing without pull requests. Small.

## Not settled

- Whether Legacy is a no-op in the five single-target projects, and that only two sites fire in `Mocking.Data`: one build without the file tells.
- Whether code scanning is on for the repository, so whether CodeQL reports anywhere.
- How many findings CS1591 hides in `src`.
