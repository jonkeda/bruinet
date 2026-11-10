# UAT Scenario Grammar

Status: **Implemented**. The parser is
[BddMarkdownParser.cs](../../src/Bruinet.Bdd/BddMarkdownParser.cs); this page writes down the
format it accepts, so the format is a contract and not whatever the parser happens to do.

Three layers, each owned by one piece of code:

1. **The document grammar**: the shape of a `.feature.md` or `.steps.md` file. Owned by the
   parser, and fixed.
2. **The phrase language**: the `{placeholder}` templates a step line binds to. Owned by the
   catalog ([BddCommandCatalog.cs](../../src/Bruinet.Bdd/BddCommandCatalog.cs)); phrases are
   declared by attributes (AD-011) or as callable phrases (AD-017).
3. **The lexicon**: the page, control, app and user names that fill the placeholders. Owned by
   name resolution (`[UatName]`, `UatNameInference`, the `Apps` table).

How to write scenarios is in the [UAT Template Guide](../guides/uat-template-guide.md); what the
phrases are and how they bind is in [UAT Phrases And Flows](../guides/uat-step-definitions.md).

## 1. Scenario files (`*.feature.md`)

A feature file is a Markdown document (AD-023). The engine reads the parts below from the top
level of the document, as Markdig splits it into blocks; everything else is `prose`, and is left
out. `NEWLINE` and `WS` are lexical; `text` is the rest of the line, trimmed.

```ebnf
document         = { prose } , feature-heading ,
                   { metadata-section | background-section | data-section
                   | phrase-section | scenario | prose-section | prose } ;
                                                            (* at least one scenario *)

feature-heading  = "# Feature:" , text , NEWLINE ;             (* the only level-1 heading *)

metadata-section = "## Metadata" , NEWLINE , { prose } , kv-table ;
kv-table         = table-with-columns("Field", "Value") ;

background-section = "## Background" , NEWLINE , steps ;

data-section     = "## Data:" , name , NEWLINE , { prose } , table ;

phrase-section   = ( "## Step:" | "## Step for each row:" ) , phrase , NEWLINE ,
                   { prose } , [ parameters-table ] , phrase-steps ;
parameters-table = table-with-columns("Parameter", "Type" [, "Example"]) ;

scenario         = [ tag-line ] ,
                   ( "## Scenario:" , name , NEWLINE , steps
                   | "## Scenario Outline:" , name , NEWLINE , steps ,
                     examples , { examples } ) ;

examples         = [ tag-line ] , "### Examples" , [ ":" , name ] , NEWLINE , { prose } , table ;

tag-line         = "@" , tag , { WS , "@" , tag } , NEWLINE ;   (* directly above the heading *)

steps            = { prose } , gherkin-block , { gherkin-block | prose } ;
phrase-steps     = { prose } , phrase-block , { phrase-block | prose } ;
gherkin-block    = fence , "gherkin" , NEWLINE , { step | comment | NEWLINE } , fence ;
phrase-block     = fence , "gherkin" , NEWLINE , { phrase-step | comment | NEWLINE } , fence ;
fence            = "```" | "~~~" ;                (* or longer, as CommonMark; "gherkin" in any case *)
comment          = "#" , text , NEWLINE ;

prose-section    = "## " , text , NEWLINE , { prose } ;  (* a level-2 heading not named above *)
prose            = (* any other Markdown block: a paragraph, list, quote, image, link, table,
                      HTML block, rule, code block of another language, or a heading of level 3
                      or lower but an outline's "### Examples" or "### Examples: name" *) ;

step             = keyword , WS , step-text , NEWLINE , [ table ] , [ doc-string ] ;
phrase-step      = step | keyword , WS , step-text , NEWLINE , passed-block ;
passed-block     = "<table>" | "<doc string>" ;
keyword          = "Given" | "When" | "Then" | "And" | "But" ;

doc-string       = '"""' , NEWLINE , { line } , '"""' , NEWLINE ;

table            = header-row , separator-row , data-row+ ;
separator-row    = "|" , ( "---" , { "-" | ":" } , "|" )+ ;
```

Rules the EBNF does not show:

- **Keywords.** `And` and `But` take the effective keyword of the step before them, so a step
  binds as `Given`, `When` or `Then` only. An `And` with nothing before it is `UAT016`.
- **Tags** must be on the line directly above a `## Scenario:` or `## Scenario Outline:` heading,
  or an outline's `### Examples` heading.
- **Outlines.** Each `<column>` in an outline step is replaced from one `### Examples` row, and
  every placeholder needs a column in every Examples section. An outline has one or more Examples
  sections; `### Examples: <name>` names one, and two names in one outline must differ. A
  section's tags join the outline's for its rows only. Rows are numbered across the sections:
  row *n* runs as `<name> [n]`, so a row's name does not depend on its section
  ([AD-036](../../.docs/decisions/ad-036-an-outline-has-named-examples-sections.md)).
- **Tables.** Every row has as many cells as the header, and a table has at least one data row.
  Cells are trimmed. In a gherkin block, a table under a step is the step's. Outside the blocks,
  a section's table (Metadata, Data, Examples, Parameters) is the first table of the section;
  another table is prose.
- **Doc strings.** A `"""` block under a step (after its table, if it has one) is passed to the
  step as text. Lines lose the indentation of the opening `"""`.
- **Gherkin blocks.** A step is read only inside a fenced block whose info string is `gherkin`,
  at the top level of the document; a block in a list or a quote is prose, and so is a block of
  another language. A section may have several blocks, read in order as one list, so `And`
  continues across them. In a block, a `#` line is a comment and every other line must be a step,
  its table or its doc string (`UAT011`). Line numbers are the file's.
- **Prose** is everything else, in any Markdown form, before, between and after the parts
  above. A level-2 heading the engine does not know opens a prose section, up to the next
  level-2 heading. Text that reads like a step outside a block is prose; when its section then
  has no steps, the "no steps" error says to put them in a gherkin block, and
  `bruinet data validate --fix` does.
- **Headings** that look like a section but are not one (`## scenario:`, `### Scenario:`,
  `## Scenario x`, an underlined heading) are a warning, `UAT031`, and read as prose.

## 2. Phrase files (`*.steps.md`)

```ebnf
phrases-document = "# Steps:" , text , NEWLINE , phrase-section* ;
```

A phrase file holds only phrase sections. A phrase in a scenario file is seen by that file; a
phrase in a `*.steps.md` file in `Features/` or a folder under it is seen by the scenarios in
that folder and below (AD-017, [Where Phrases Live](../guides/uat-step-definitions.md#where-phrases-live)).

In a phrase section:

- The heading's `{x}` placeholders are the call's arguments. The body uses them as `<x>`. In a
  `## Step for each row:` section, `<Column>` reads a column of the current row of the call's
  table.
- `<table>` or `<doc string>` alone on the line under a body step passes the call's table or doc
  string to that step. It is only allowed in a phrase section.
- The optional `Parameters` table declares a type that the body's use does not show (AD-017).

## 3. The phrase language

A phrase is the template a step line matches:

```ebnf
phrase      = segment , { WS , segment } ;
segment     = literal | placeholder ;
placeholder = "{" , letter , { letter | digit | "_" | "-" } , "}" ;
```

- A phrase matches the **whole** step text, case-sensitively. Whitespace in the phrase matches
  one or more whitespace characters.
- A placeholder's value is trimmed, and surrounding double quotes are removed: `"Alice"` is
  `Alice`. The value is then read as the bound parameter's type (AD-016).
- `{page}` and `{control}` name a page and a control of the current page. `{user}` and `{app}`
  in first place name the step's subject (AD-015). Every other placeholder is an argument.
- Binding keeps the phrases with the step's effective keyword that match, prefers phrases with
  no placeholder, then the phrase with the most literal characters. Two left is ambiguous
  (`UATB002`); none is unknown (`UATB001`). Callable and C# phrases compete under the same rule.

No optional words, alternatives, regexes or types in phrase text (AD-016).

## 4. The lexicon

A page or control name is its `[UatName("...")]`, or, without one, its member name with the
type suffix removed (`Page`, `Button`, `Entry`, ...) and split into words: `TodoListPage` is
`Todo List`, `AddButton` is `Add`, `TitleError` is `Title Error`. Users and apps come from the
`Apps` table. A scenario never names an AutomationId.

## 5. Parser diagnostics

| Code | Meaning |
| --- | --- |
| `UAT000`, `UAT001` | no `# Feature:` heading, or more than one |
| `UAT002`, `UAT003` | the heading has no title, or is not first |
| `UAT004` | `## Background` has no steps; when it holds step-like prose, the message says to put it in a gherkin block |
| `UAT005` | `## Data:` has no name |
| `UAT006`, `UAT007` | a scenario has no name, or no steps (with the same hint as `UAT004`) |
| `UAT008` | the file has no scenario |
| `UAT009`, `UAT010` | the Metadata table's columns are not `Field`/`Value`, or a field is empty |
| `UAT011` | a line in a gherkin block is not a step, a comment, a table or a doc string |
| `UAT012` - `UAT015` | an outline has no `### Examples`, no name, no steps (with the hint), or a placeholder with no column (naming the Examples section when it has a name) |
| `UAT016` | `And`/`But` without a step before it |
| `UAT017` - `UAT019` | a section's table is missing, a row has the wrong width, or a table has no data rows |
| `UAT020` | a tag line is not directly above a scenario |
| `UAT021` | a gherkin block outside a Background, Scenario or Step section |
| `UAT022` | a second level-1 heading |
| `UAT023` | a doc string is not closed |
| `UAT024` | `<table>`/`<doc string>` outside a phrase section, or not under a step |
| `UAT025`, `UAT026` | a phrase heading has no phrase, or the phrase has no steps (with the hint) |
| `UAT027` | a malformed `Parameters` table |
| `UAT028` | a steps file with content other than one `# Steps:` heading and step sections |
| `UAT029` | a heading or file name in the form before feature files (`# UAT:`, `## Phrase:`, `*.uat.md`, `*.phrases.md`); `bruinet data validate --fix` rewrites it |
| `UAT031` | warning: a heading that looks like a section but is not one, read as prose |
| `UAT032` | two Examples sections of one outline have the same name |

Binder (`UATB0xx`) and argument (`UATA0xx`) diagnostics are raised in the binder and the value
reader (`BddBinder`, `BddValues`, `BddPhraseDeclarations`).
