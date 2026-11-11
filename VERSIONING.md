# Versions and releases

Bruinet's packages are versioned together, one version for all, and released from a GitHub release.
This page says what `.github/workflows/release.yml` and `ci.yml` do; it replaced a longer strategy that no
workflow followed (review 11, proposal 10).

## A release

Publishing a GitHub release tagged `vX.Y.Z`, or `vX.Y.Z-suffix` for a prerelease, runs `release.yml`.

- It packs every packable project under `src/`, and the `bruinet` tool, with the tag's version (`tools/Scripts/pack.ps1`).
- It proves the packages before anything is pushed: a consumer restores and builds every library from the packed
  files alone, for each target framework (`tools/Scripts/check-packages.ps1`).
- It pushes them to this organisation's GitHub Packages feed and attaches them to the release.
- The repository is private, so nothing goes to nuget.org.

## Between releases

Each CI run on `main` packs and checks the packages too, as `0.1.0-ci.<run number>`, and keeps them as the run's
artifacts. They are for trying a change, not for depending on.

## What a version promises

The version follows SemVer from 1.0. Before 1.0 a minor version may break the API.

- A break is listed for consumers in `.github/skills/api-upgrade/references/changes.md`, with the pattern to find
  it and the fix; CI's `check-api-changes.ps1` fails a change that breaks the declared API without a row.
- `BREAKING-CHANGES-POLICY.md` says what counts as a break.
- `CHANGELOG.md` says what each release changed.

## Using the feed

A consumer adds the organisation's GitHub Packages feed with a token that can read packages, and maps `Bruinet.*`
and `MauiHeadless` to it in `nuget.config`'s package source mapping.
