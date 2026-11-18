# Bruinet.Spec

The Spec module of Bruinet: reads spec files (`*.spec.md`), checks them against the format, and traces every rule of a
spec to the tests that reach it and the results that prove it. The commands are `bruinet spec check` and
`bruinet spec trace`; the format is described in `docs/spec/spec-format.md`.

It references no other Bruinet package: a team that writes specs and tests with plain xUnit takes this package alone.
