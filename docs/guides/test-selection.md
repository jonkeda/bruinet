# Test Selection

A UI suite doesn't need to run whole after every change. Bruinet records which page objects and
controls each test used, and from that it picks the tests a change can reach. It prints each test
with the reason it was picked, and gives you one `--filter` to run them. During development you run
that pick. At a gate you run the whole suite, which also catches anything the pick missed.

## When To Use It

- **While editing and at a task's build check:** run the pick, on the bUnit head first, then once in
  the browser.
- **Not at a gate.** A gate runs the suite whole and writes a new touch map from that run.
- **Only on a suite that isn't flaky.** A picked test that fails at random teaches people to run
  everything again.

## The Touch Map

The touch map, `touch-map.json` beside the suite's project, lists every test and scenario with its
filter term (`FullyQualifiedName=` or `BddScenario=`) and the page objects and controls it used. It
comes from a full run's timings. Commit it.

```bash
$env:BRUINET_TIMINGS = "<folder>"; dotnet test <suite>
bruinet tests touch-map <folder> --project <suite>
bruinet tests timings <folder>    # timings.md beside them: where the run's time went
```

Scenarios name themselves in the timings. Plain `[Fact]` tests need `[assembly: TestScope]`
(`Bruinet.Uat`) in the suite. Without it their rows carry no test name, and they never get picked.
A new test or page object only gets picked once the map has been written again, so until then put
it in the filter yourself.

## What A Change Reaches

```bash
bruinet tests affected --project <suite> [--base <rev> | --files <paths>] [--always "Gate=Smoke"]
```

The command takes the files changed since `--base` (default `HEAD`), including uncommitted
changes and new files that git doesn't ignore. Each changed file reaches tests as follows:

| Changed file | Reaches |
| --- | --- |
| A page of the app | Tests that used its `data-testid`s, `id`s or `TestId="..."` parameters, and tests whose page objects open its route |
| A page object | Tests that used it |
| A feature file or test class | Its scenarios or tests |
| A library the suite references, the suite's test base, fixture or config, the app's layout, `Program.cs` or styles, or the root build files | The whole suite |
| Anything else | Nothing |

An id built at run time (`bench-@name`) counts by the part before the `@`. The route rule matters
when a test only touches a page's child components, because the page itself is still under that
test. Files the suite links from another project (`<Compile Include="..\Other\Fixture.cs" />`)
count as the suite's own files.

`--always` adds terms that run whatever changed, such as a smoke set. `--app` names the app's
project folder when the suite doesn't reference it. `--map` points to a touch map kept somewhere
else. `--json` prints the result for tools.

## The Scripts

- **Bruinet:** `pwsh tools/Scripts/select-tests.ps1` runs `affected` for the Blazor and Html suites.
  `-Run` runs the pick, on bUnit by default or in the browser with `-Head playwright`.
  `-WriteMap` runs a suite whole and writes its map.
- **Tomorrow:** `./tools/Select-Tests.ps1` does the same for the studio and game suites, and always
  adds `Gate=Smoke` to the pick.

Both scripts build bruinet into a temp folder, because the agents' MCP servers keep
`tools/Bruinet.Cli/bin` open.

## What It Misses

A test reaches the app only through ids and routes. A change that alters what a page shows without
touching the page, such as a rule in the domain or a seed row, runs that test only when the
changed file is in a project the suite references. A test that only resets state touches no page
at all, so it runs only when the suite's base or a library changes. On Tomorrow's studio suite, a
renamed test id was found only through the route rule. Misses at the gates aren't counted yet:
the plan is to compare a full run's failures with what the pick would have been.
