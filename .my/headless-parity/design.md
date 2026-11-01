# MauiHeadless parity: measuring what it catches

Status: **design**, 2026-11-01. Checked against the tree on `move`.

## Outcome

MauiHeadless cannot be proven "as good as a device": it draws nothing and does not compile `#if ANDROID` code, as
[maui-headless.md](../../docs/guides/maui-headless.md) says. What can be shown is a measured claim, kept true every night:

> On the sample apps, headless catches N% of the seeded bugs Windows catches; every bug only a device caught falls in a
> category "What Needs A Device" lists.

That claim decides how much an agent's inner loop may trust headless, and what the Windows gate before merge is for.

## What the sample tests already prove, and what not

The samples are the instrument this design needs: 313 tests in `tests/Bruinet.Maui.UITests` over the MAUI sample app, about
40 Todo scenarios in `samples/Todo/tests/Bruinet.Samples.Todo.Uat`, the Shell app's suite. CI runs them headless on Windows
and macOS.

- **They prove agreement on a correct app:** the same tests are green on both heads, so headless does not raise false failures there.
- **They do not prove equal bug catching:** two heads that are both green on a correct app can still differ on a broken one. Only a broken app shows that.
- **So:** the existing tests are reused as they are; what is missing is the broken apps (seeded bugs) and running them on both heads.

The samples are written to be tested. Construction's app is the check that the result holds for a real app (M5).

## Measurements

| # | What | Gives | Cost |
| --- | --- | --- | --- |
| M1 | Seeded bugs by Stryker.NET in the sample apps, run per head | catch rate per head, the device-only list | nightly sample |
| M2 | A dozen hand-made bugs, one per device-only category | the guide's list confirmed or corrected | once, then on MAUI upgrades |
| M3 | Real past bugs: a fix commit undone, both heads run | evidence on real bugs | once per bug found |
| M4 | Step-by-step comparison of both heads on the correct app | behaviour differences, the guide's table kept complete | each CI run |
| M5 | M1 on Construction's app, a sample of mutants | the claim holds outside the samples | once, then quarterly |

### M1. Seeded bugs

- Stryker.NET on `samples/Bruinet.Samples.Maui.App`, `samples/Bruinet.Samples.Maui.ShellApp` and the Todo app's MAUI project: app code only, not Bruinet.
- Per mutant, only the tests the touch map ties to its file (`bruinet tests affected --files <file>`); a mutant no test reaches is reported as unreached, not as missed.
- Headless runs every mutant; Windows a sample (start with 200, stratified by file); Android a smaller sample (50) on the emulator.
- Per mutant record `{id, file, line, mutator, headless, windows, android}` with killed, survived, timeout or unreached.
- Output: `parity-report.md` with the catch rate per head, the agreement matrix, and every mutant only a device killed, each with its category (M2's list) or "unexplained".
- Mutants are XAML-blind (Stryker mutates C#); XAML bugs come from M2.

### M2. Hand-made device-only bugs

One per category; each is expected to be caught on the device and missed headless.

| Category | Bug |
| --- | --- |
| Layout | a label's width fixed so its text is cut off |
| Platform handler | a custom handler that throws on Windows only |
| `#if ANDROID` | a wrong value in Android-only code |
| Platform-drawn control | a picker's popup that lists the wrong items |
| Gesture | a swipe threshold set too high |
| Timing | a `DispatchDelayed` callback that runs too late |
| Visibility | a row reported in the tree but off screen |
| Pixels | a wrong colour from a theme resource |

Plus the same number of ordinary logic bugs headless should catch, as the control group. A bug landing in the wrong column
corrects the guide.

### M3. Real past bugs

- Find fix commits in the samples' and Construction's history (`git log --grep "fix"` and the CHANGELOG), undo each fix on a branch, run its tests on both heads.
- Kept as a list in this folder: commit, bug, caught headless, caught on Windows.

### M4. Step-by-step comparison

- Run the MAUI suites on headless and Windows; compare per step: outcome, message, and once AI4's page state exists ([aiimprovements](../aiimprovements/plan.md)), the controls' values after each step.
- Each difference is a headless gap or a row missing from the guide's "Differences Between The Heads" table.

## Where it runs

- A `parity` command in `tools/Scripts` (`parity.ps1 -Mutants 200 -Heads headless,windows`) writing `TestResults/<run>/parity/`.
- Nightly on the Windows runner (headless all, Windows sample); Android by hand, as Android is not in CI.
- The report's headline number goes in the guide's "Which Head When" section, with its date.

## Order

1. M4 on today's suites: no new tooling beyond a result comparison; shows the current differences.
2. M2: a day; settles the guide's lists before numbers are quoted.
3. M1 on the MAUI sample app, Windows sample of 200: the headline number.
4. M1 nightly, and on the Shell and Todo apps.
5. M3 as bugs are found; M5 when Construction's suite runs on headless.

## Open questions, with proposals

| # | Question | Proposed | Why |
| --- | --- | --- | --- |
| Q1 | Stryker.NET or our own mutator | Stryker.NET | maintained, knows C#; we only need its mutants applied and the suite run per head |
| Q2 | How many Windows mutants are enough | 200, stratified by file | gives the catch rate to about ±3 points; more only if the gap is near the line |
| Q3 | What claim is good enough | headless ≥ 95% of Windows' catches, and no unexplained device-only mutant | the explained ones are what the Windows gate is for |
| Q4 | Share plumbing with AI20 (mutation testing for scenarios) | yes: the touch-map selection and the per-head runner | AI20 mutates what the page shows, M1 mutates app code; same runner |
