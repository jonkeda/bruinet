# Reporting And Artifacts

Bruinet-owned outputs use the shared artifact provider in
`src/Bruinet.Core/Artifacts`.

## Environment

| Variable | Purpose |
| --- | --- |
| `BRUINET_TEST_RESULTS_DIR` | Overrides the `TestResults` root |
| `BRUINET_TEST_RUN_ID` | Reuses a run folder across projects |
| `BRUINET_TEST_SUITE` | Overrides the suite folder name |

## Folder Layout

Default layout:

```text
TestResults/
  <run-id>/
    manifest.json
    summary.md
    suites/
      <suite-name>/
        runner/
        logs/
        screenshots/
        uat/
        coverage/
        traces/
        videos/
        downloads/
        snapshots/
        attachments/
```

If no suite is configured, typed artifact folders are created directly under the
run directory.

The MAUI UI suite writes two reports into `attachments/` on every run: the gesture
accessibility audit (`accessibility-audit.md`) and per-class test timings
(`test-timings.md`, with the raw `test-timings.csv`). See the
[MAUI guide](../platform-guides/maui.md#run-artifacts).

## Provider

Use `DefaultTestArtifactPathProvider.Create(...)` to resolve folders. It finds a
repository root by walking up from the base directory until it finds a solution
file or `.git`.

Use `EnsureDirectories()` before writing multiple artifact types.

## Manifest And Summary

`TestArtifactManifestWriter.RecordArtifact(...)` records an artifact with:

- kind;
- name;
- status;
- path relative to the run directory;
- creation time;
- optional metadata.

The writer updates:

- `manifest.json`
- `summary.md`

Artifact indexing is best effort and must not fail the test run.

## UAT Reporting

UAT reporting uses `BddConfig.Reporting.OutputDirectory`. If omitted, it uses
the provider's `UatDirectory`.

Supported path tokens:

- `$(BruinetTestResults)`
- `${BruinetTestResults}`

Both resolve to the provider suite directory. UAT scenario result files are
registered as `uat-scenario` artifacts.

A step trace holds each step's text and, after a failed step, what every control on the page
showed. Password fields, the controls `TraceMask` lists and a secret step's quoted values are
written `***`, but anything else on the page is kept: `TestResults` is not for public upload.

## Source Files

- `src/Bruinet.Core/Artifacts/TestArtifactOptions.cs`
- `src/Bruinet.Core/Artifacts/DefaultTestArtifactPathProvider.cs`
- `src/Bruinet.Core/Artifacts/ITestArtifactPathProvider.cs`
- `src/Bruinet.Core/Artifacts/TestArtifactManifestWriter.cs`
- `src/Bruinet.Uat/UatScenarioTestBase.cs`
