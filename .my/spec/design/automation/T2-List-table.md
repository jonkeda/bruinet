# T2 List table: automation

The style: [../200-Tables.md](../200-Tables.md#t2-list-table). Entry point **A, table checks**, with the
sort order as a unit test where the app sorts in code. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## List: Contacts

| Column | Shows | Sort | Filter |
| --- | --- | :---: | :---: |
| Name | the contact's name, as a link to its page | ☑ default, A→Z | ☑ |
| Type | Client, Supplier or Both | ☑ | ☑ |
| City | | ☑ | |

- Empty: "No contacts found"
- Page size: 25
- Row actions: Open, Delete
```

## What proves it, by layer

| Line | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| The columns, in order | | | **✓** the header | |
| Default sort | ✓ if the app sorts in code (Todo's TOD.01.5) | ✓ if the server sorts: the request's sort parameter | **✓** the rows come in that order | |
| Each sortable column sorts | | ✓ the request | **✓** one press per column | one column |
| Filterable columns | ✓ the filter function | | **✓** a filter shows only matching rows | |
| Empty text | | | **✓** with an empty test set | |
| Page size | | ✓ the request's page size | ✓ with more rows than a page | |
| Row actions | | | **✓** each row offers exactly these | |

## The simplest automation

**Nothing per column.** The list check finds the list on the page (the page's typed rows, such as Bouw7's
`HeimdallRows<DepartmentsPage, DepartmentsRow>`, or a `TableControl`) and checks:

| Check | How |
| --- | --- |
| Header is exactly these columns | The table's header text against the Column cells, `AssertExactly` |
| Default sort | Read the rows' cells in the default-sorted column; compare with the same values sorted the stated way |
| Each ☑ Sort | Press the header, wait for the rows (`WaitHasRows`), compare as above, both directions |
| Each ☑ Filter | Type the first row's value into the column's filter; every row shown has it |
| Empty text | Start from a state with no records (`[BddState]` or a test set), see the text |
| Page size | Start from a state with more records than a page, count the rows |
| Row actions | Each row's typed member exposes these actions, no others |

**Data**: the checks need rows to look at. On a fast head, a **test set** (strict JSON) with a few rows,
chosen by the list's state; on a shared environment such as Bouw7's acc, whatever the list holds,
read-only: sort and filter compare the list with itself and never need known values.

**Sorting in code** (Todo sorts in the view model): a unit test over the sort function, fed by an example
table (T7) of rows in and rows out. The UI check then needs only the default order once.

## What Bruinet needs

| Need | Exists? |
| --- | --- |
| Typed rows with a member per column | ✓ (Bouw7's `HeimdallRows`, typed rows on five pages) |
| Waiting for rows, reading cells | ✓ `TableControl.WaitHasRows`, cell text |
| Pressing a column header to sort | Partly: per table control; **a generic `SortBy(column)` verb** for the list check |
| A column's filter | Per suite (Bouw7's `FilterAbove(column)`), found by a `[BddControl]` pattern; **new**: the list check asks for it by name |
| Reading the list table, running the checks | **New**: the table check for lists |

## Limits

- What a cell *means* ("a link to its page") is checked only as far as the kind says: a link column
  is checked to navigate, not to which record. The flow (P4) checks where it leads.
- Sorting rules with ties or locale ("Ä" after "A") are a decision for the spec: the check compares with
  invariant culture unless the table says the language.

## Questions

- How is "the same values sorted the stated way" computed for dates and numbers shown as text? Proposal:
  the column's kind from the page object's row member (a date column parses as a date); text otherwise.
- Does a list check run against acc data it does not own? Proposal: yes, read-only checks only (header,
  sort, filter, row actions); empty and page size only on states the suite controls.
