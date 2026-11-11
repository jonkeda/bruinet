# Migration Notes

These notes help migrate old Bruinet or Oravey-era material to the current
Bruinet source layout.

## Naming

Replace old namespaces and project names with current `Bruinet.*` names.

| Old pattern | Current pattern |
| --- | --- |
| `Oravey.UITestFramework.*` | `Bruinet.*` |
| numbered docs such as `02-framework-overview.md` | docs under named folders |
| `src/Bruinet.*` | `src/Bruinet.*` |
| `tests/Bruinet.*` | `tests/Bruinet.*` |

## Docs

- Active docs live under `docs/`.
- Do not copy old links forward unless the target exists.
- Prefer short current docs over large copied legacy pages.

## Tests

- Use xUnit `Assert`.
- Remove FluentAssertions references.
- Replace sleeps with waits for state.
- Move repeated helper behavior into controls or fixtures.

## Artifacts

Move hardcoded output paths to the Bruinet artifact provider where possible.
New output should use `TestResults/<run-id>/suites/<suite>/`.
