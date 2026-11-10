---
name: api-upgrade
description: Keep and apply the list of Bruinet public API changes that projects using Bruinet must follow - renamed or moved types, changed signatures, types made internal, removed members. Use when a Bruinet change edits any PublicAPI.Unshipped.txt (add its row), when a project that uses Bruinet is moved to a newer Bruinet and no longer builds, or when asked what a consumer has to update after a Bruinet API change.
---

# Bruinet API changes and what consumers update

Bruinet's public API is already used by other projects. Each change to it is written once, in
[references/changes.md](references/changes.md), as a row a consumer can act on: what to search for
and what to write instead. This skill has two uses: adding a row when Bruinet changes, and walking
the rows when a consumer moves to a newer Bruinet.

The review behind the first rows is `.my/reviews/bruinet-public-api-review.md`.

## When Bruinet's API changes

A change is an API change when it edits a `src/*/PublicAPI.Unshipped.txt` by removing or altering a
line, or adds a member to an interface or an abstract member to a class: whoever implements or
derives from it no longer compiles. Other added lines need no row. An interface member added with a
default body breaks no one; give it a row anyway that says so, since the check below cannot tell.

`tools/Scripts/check-api-changes.ps1` fails when a change removes or alters a
`PublicAPI.Unshipped.txt` line, or adds an interface or abstract member to a type that was already
public, and `references/changes.md` is untouched in the same change. Run it before a commit; CI runs
it on pull requests.

1. In the same commit, add a row to `references/changes.md` under `## Made`, newest first. If the
   change was listed under `## Planned`, move that row and fill in what was really done.
2. Write the row for the consumer, not for Bruinet:
   - **Find**: a search pattern that hits the old usage in consumer code (a type name, a `using`,
     a method call). One that also hits unrelated code is worse than two narrow ones.
   - **Write instead**: the new name or call, exact. `none - go through the page object` is a
     valid answer for a type made internal.
   - **Commit**: the Bruinet commit that made the change.
3. Update Bruinet's own samples and the suites in `tests/` and `samples/` in that commit; they are
   the first consumers and prove the row's replacement compiles.
4. Add the change to `CHANGELOG.md` under `### Changed` or `### Removed`, one line, pointing at
   nothing: the changelog says what changed, this skill's list says what to do about it.

A row is never deleted. A change undone later gets a new row.

## When a consumer moves to a newer Bruinet

1. Find the Bruinet commit the consumer was on and the one it moves to (for a submodule:
   `git diff --submodule=log <path>` in the consumer; for packages: the two versions' tags).
2. Take the rows in `## Made` whose commit lies between the two
   (`git merge-base --is-ancestor <row commit> <new>` and not an ancestor of `<old>`).
3. Oldest first, for each row: search the consumer for **Find**, apply **Write instead**. A row
   with no hits needs nothing; say so and move on.
4. Build the consumer's test projects. An error that no row explains means a row is missing: find
   the Bruinet commit that changed the line (`git log -S"<old name>" -- "src/*/PublicAPI.Unshipped.txt"`),
   add the row in Bruinet, then apply it.
5. Run the consumer's smoke tests. A renamed type that compiles can still be bound by name
   somewhere: step phrases, `bdd.config`/`uat.config`, settings JSON sections, `[TestPage]` names.
   Search those files for the old names too.
6. Report per row: hits, files changed, and any row that could not be applied.

Rows under `## Planned` are not made yet. Never apply them to a consumer; they are there so a
consumer's owner can see what is coming.

## Known consumers

Listed so a breaking change can be checked against them before it is pushed. Add a project here
when it starts using Bruinet.

| Consumer | How it takes Bruinet | Where it uses it |
| --- | --- | --- |
| Tomorrow | submodule `external/test-tooling` on `move` | `tests/Tomorrow.App.UITests`, `tests/Tomorrow.Studio.UITests` |
| Bruinet's own samples | project references | `samples/Todo/tests/*`, `samples/*` |

## Rules

- No row, no break: a commit that removes or alters a `PublicAPI.Unshipped.txt` line without a row
  here is not finished.
- One row per thing a consumer searches for. A rename of twenty controls by one rule is one row
  with the rule and a regex, not twenty.
- Do not keep the old name as a forwarding type unless `BREAKING-CHANGES-POLICY.md` requires it for
  a released version; while every `PublicAPI.Shipped.txt` is empty, the row is the migration.
