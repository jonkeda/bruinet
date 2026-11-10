# AD-036: An Outline Has Named Examples Sections, And A Row Keeps Its Number

A scenario outline has one or more `### Examples` sections. `### Examples: <name>` names one; the bare
`### Examples` stays valid. A tag line above a section joins the outline's tags for that section's rows only, so a head
tag such as `@web` picks the rows a head runs. Rows are numbered across all sections, and row *n* runs as
`<name> [n]` whichever section holds it. The section's name shows in the preview and the editor's outline, not in the
scenario's name.

Every section needs a column for every placeholder (`UAT015`, naming the section). Two sections of one outline with the
same name are `UAT032`. A section without a table is the existing `UAT017`.

**Why:** someone coming from Reqnroll or SpecFlow expects several tagged Examples blocks, and uses them to group cases
by meaning and to run some rows on some heads only. Numbering across sections keeps a row's name stable: the scenario
addresses, `--filter` lines, touch maps and `durations.json` key on that name, and none of them break when a section is
named, renamed or split.

**Ruled out:**
- the section's name in the scenario's name (`<name> [<section> n]`): renaming a section would rename its rows and
  orphan their filters, touch maps and timings;
- numbering per section: two sections would both have a row 1, so a name would need the section again;
- tags only on the outline: a head could not pick some rows, and an outline would be split in two to do it;
- a new code for a section without a table: `UAT017` already says a section's table is missing.

**Broken when:** a row's name changes when a section is named or renamed; a
section's tags reach another section's rows; or an outline with two sections of the same name parses without `UAT032`.
