# Frainet

Frainet is for running software as a dark factory: agents write, test and review the code, people
design the line and judge what comes out. A loop can only run dark when its spec is exact and
something other than a person tells it whether the work meets it. Frainet is the factory: the
loops, and what runs the agents in them. Frainet lives in Bruinet's repository for now and may
become its own later.

## The four parts

| Part | Does |
| --- | --- |
| **Bruinet** | Tests: runs the scenarios and the specs' examples |
| **Lirenet** ([../Lirenet](../Lirenet/README.md)) | Specs: the format, `lirenet check`, `lirenet trace` |
| **Gaminet** | Migration (not built yet) |
| **Frainet** | The factory: the loops that use the other three |

The spec code started here and moved to Lirenet. For one release `frainet check` and
`frainet trace` still work: they run `lirenet` with the same arguments, give its output and its
exit code, and say on the error stream that the command moved. Frainet reaches Lirenet as a
command, never as code. It finds `lirenet.dll` beside itself, in `../Lirenet` (built with
Frainet's own build), or where the `LIRENET` variable says.

## The idea behind it

Every loop turns plan, build, deploy and feedback, and the build of one loop is many turns of the
loop inside it: tasks in a capability, capabilities in a module. A loop goes dark when its
feedback is an exact check. The specs loop runs across all of them, feeding each plan, so the
spec is where the factory's light stays on longest. The loops are written out in
[.my/loops/](.my/loops/02_loops_generic.md); the research behind the specs is in [.my/specs/](.my/specs/).

## Commands

```bash
dotnet build Frainet.slnx
```

Today's loop over a site's modules is `tools/Scripts/site-loop.ps1` in Bruinet's repository.

## How it grows

It starts very small, on Bruinet's Todo sample, which already has journeys, tiers and a coverage
report. Nothing is built ahead of a sample that needs it. The plan is [.my/plan.md](.my/plan.md).
