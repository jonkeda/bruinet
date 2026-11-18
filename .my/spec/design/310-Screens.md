# Screens: ASCII, Wireframe, HTML, or something else?

Status: **discussion**, for a decision. Date: 2026-11-18. Follows [300-Pictures.md](300-Pictures.md),
which proposed a screen card, a Mermaid sketch and an annotated screenshot for Q1 ("what is on this
screen?") and Q14 ("does it look right?"). This document asks which **source format** a screen is
written in. Nothing is designed yet; each option shows the same screen so they can be compared.

## The screen used in every option

Bouw7's department form (B7.MST.02): a Back button and the title "Create department", one required
field Name, and the buttons Cancel and Create.

## What a screen format must do

| # | Need | Why |
| --- | --- | --- |
| N1 | **Say what each element is**: a field, a button, a label, a list | So Lirenet can check it and Bruinet can test it. A picture of a box is not a field |
| N2 | **Carry the spec's facts**: required, kind, choices, limits, where a button leads | So the screen and the field table are not two sources that drift |
| N3 | **Read as a screen** for the owner, at a glance | The reason to have a picture at all |
| N4 | **Plain text in git**: diffs in a pull request, merges, reviewable | Everything else in a spec is |
| N5 | **Renders where people look**: GitHub, VS Code, the Presenter | A picture nobody sees is not read |
| N6 | **Written by agents and drafted from the app** | The Scraper knows the controls; agents will write most screens |
| N7 | **Read by Lirenet without a model**, in .NET | The checks must stay exact |
| N8 | **Small to learn, small to keep** | One author; the format must not become a product of its own |

## Option 1: ASCII

```text
+---------------------------------------------+
| < Back        Create department             |
|---------------------------------------------|
| Name *                                      |
| [_______________________________]           |
|                                             |
|                       [ Cancel ]  [Create]  |
+---------------------------------------------+
```

**For**
- Renders everywhere as a code block, with nothing installed. GitHub, VS Code, a terminal, a chat.
- Anyone can write it; agents produce it readily.
- No dependency at all.

**Against**
- **No meaning.** `[____]` is a field and `[Create]` a button only by convention; `*` means
  required only if we say so. Lirenet would need its own small grammar of brackets to read it, and
  that grammar is the hard part, not the drawing.
- **Fragile to edit.** One longer label breaks every right edge below it. Diffs show every
  realigned line, not the one real change. Agents often misalign columns.
- Cannot hold limits, choices, messages or where a button leads without notes beside it.
- Does not scale: a form of 40 fields in sections (the contact form) is a page of boxes.

**Fits:** a quick sketch in a discussion or a chat. Not the source of a spec.

## Option 2: Wireframe (github.com/jonkeda/Wireframes)

Your own tool: a keyword-based text language for UI wireframes, rendered to SVG. A VS Code
extension with live preview, IntelliSense and diagnostics; npm packages (`wireframe-core`, a
Mermaid plugin, a CLI with `render` and `validate`); four themes; 50+ controls; layouts. Version
0.0.7 on npm, last real changes December 2025.

```wireframe
wireframe sketch
    %title: Department form
    Vertical gap=16
        Horizontal
            Button "Back" @Departments
            Label "**Create department**"
        /Horizontal
        Label "Name"
        TextInput "Name" :Name required
        Horizontal justify=end
            Button "Cancel" @Departments
            Button "Create" :Create primary
        /Horizontal
    /Vertical
/wireframe
```

**For**
- **It has meaning** (N1): `TextInput`, `Dropdown`, `Checkbox`, `Button`, `Table`, `DataGrid`
  with typed columns, `Tabs`, `Modal`. That is close to the field kinds of a field table, and to
  Bruinet's ControlObjects.
- **It already carries spec facts** (N2): `required`, `min=8`, `pattern=email`, `Option` lists in
  a `Dropdown`, ids (`:Name`), and links to another screen (`@Departments`). The links are the
  flow's arrows, written where the button is.
- **Text in git** (N4), with a VS Code preview (N3) and a validator that already exists.
- **It is yours**, so it can grow what specs need: a message per field, a "spec" profile, a
  Bouw7 or Vuetify control kind. No waiting for someone else's roadmap.
- **One source for three styles**: the screen card, the field table's kind and required columns,
  and the flow's arrows could all be read from it, instead of written three times.
- **Drafted from the app** (N6): the Scraper knows each control's kind, label, required mark and
  choices; writing them as Wireframe is a projection, not a guess. The ids could be the page
  object's member names, so screen and page object meet by name.

**Against**
- **Does not render on GitHub** (N5). GitHub's Mermaid has no plug-ins, so a `wireframe` block
  shows as code there. Fix: a rendered SVG committed beside each screen (`wire render`), refreshed
  by a check, and shown as an image in the Markdown.
- **Lirenet cannot read it yet** (N7). The parser is TypeScript; Lirenet is .NET. Either a small
  C# reader for a subset (the syntax is keywords, quotes and `/End` lines, so this is modest), or
  the CLI asked for a JSON tree, which makes Node a dependency of Lirenet.
- **Layout is noise in a spec.** `gap=16`, `justify=end`, `w=300` are design, not behaviour. A
  spec profile would make them optional and never checked.
- **Label and field are separate elements.** `Label "Name"` above `TextInput "Name"` does not say
  which label belongs to which field; the quoted text of `TextInput` reads as a placeholder. For a
  spec the field's name must be its label. Needs a convention or an attribute (`label=`).
- **One author, early version** (N8): the same risk the reviewers named for Bruinet, now for two
  tools. It has been quiet for most of 2026.
- **Another language to learn**, though a small one, with IntelliSense to help.

**Fits:** the source of a screen, if it is extended with a spec profile, a C# reader and committed
SVGs.

## Option 3: HTML

```html
<main data-screen="Department form">
  <header>
    <a href="departments.html">Back</a>
    <h1>Create department</h1>
  </header>
  <label>Name <input name="Name" required data-message="Name is required"></label>
  <footer>
    <a href="departments.html">Cancel</a>
    <button class="primary">Create</button>
  </footer>
</main>
```

**For**
- **Meaning is built in** (N1, N2): `input`, `select`, `button`, `required`, `maxlength`,
  `pattern`, `<option>`, `<label>` that *does* own its field, links that are the flow.
- **Looks like the real thing** (N3), in any browser, with a stylesheet.
- **Clickable**: the screens linked together are a prototype a person can walk through.
- **Bruinet can test it.** A prototype in HTML is a web app: the same page objects and scenarios
  can run against it *before the real app exists*, on the web head. For a new system, that is the
  spec proving its own tests.
- Agents write HTML very well. Every tool reads it; Lirenet can parse it with an HTML library.

**Against**
- **Not readable as text** (N3, N4): the owner cannot judge it from a pull request; every change is
  tags and attributes. It must be opened in a browser to be read.
- **Not rendered in Markdown on GitHub**: GitHub strips forms and inputs from Markdown. It needs
  separate `.html` files and a preview step (a link to GitHub Pages, or a screenshot made by
  Bruinet).
- **Drifts into design.** CSS invites colours, spacing and fonts; soon it is a mock-up to keep up
  to date, not a spec.
- **Verbose**: about three times the lines of the Wireframe version for the same screen.

**Fits:** a clickable prototype of a **new** system, where testing the prototype is worth the
weight. Less so for describing an existing screen.

## Option 4: something else

| Option | What | For | Against |
| --- | --- | --- | --- |
| **4a Screen card and field table only** | No picture; the tables of [200-Tables.md](200-Tables.md) | Simplest; exact; renders everywhere; already proposed | No picture; layout and grouping are lost |
| **4b Mermaid `block-beta`** | Boxes in a grid, inside standard Mermaid | Renders on GitHub and VS Code natively | Boxes only, no field or button meaning; marked beta; poor for large forms |
| **4c PlantUML Salt** | An established text wireframe language (`@startsalt`) | Mature, documented, known to some teams | Needs a PlantUML server or Java to render; not on GitHub; less meaning than Wireframe; not ours to extend |
| **4d Annotated screenshot** | Bruinet's own screenshot with numbered marks and a table | Shows the real screen; drafted with no effort | Only when the app exists; no meaning in the image; goes stale |
| **4e draw.io / Excalidraw (`.drawio.svg`)** | Drawn by hand, saved as an SVG that renders on GitHub and edits in VS Code | Pretty, free-form | Hand-drawn, so not drafted and not checked; diffs unreadable |
| **4f A screen model in YAML or JSON, rendered** | The facts as data; a picture made from them | Exact, easy for Lirenet and agents | Two artefacts; the data is not pleasant to read; the picture needs a renderer anyway |
| **4g Figma or another design tool** | The designer's file, linked from the spec | Real design | Not text, not in git, not a spec; a link only |

## Side by side

● good · ◐ partly · ○ poor

| Need | ASCII | Wireframe | HTML | Card + table (4a) | Mermaid block (4b) | Screenshot (4d) |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| N1 Says what each element is | ○ | ● | ● | ● | ○ | ○ |
| N2 Carries the spec's facts | ○ | ◐ (messages missing) | ● | ● | ○ | ○ |
| N3 Reads as a screen | ◐ | ● (in preview) | ● (in a browser) | ○ | ◐ | ● |
| N4 Text in git, good diffs | ◐ | ● | ◐ | ● | ● | ○ |
| N5 Renders on GitHub | ● | ○ (needs a committed SVG) | ○ (needs a screenshot) | ● | ● | ● |
| N6 Agents and drafted from the app | ◐ | ● | ● | ● | ◐ | ● |
| N7 Lirenet reads it, in .NET | ○ | ◐ (needs a C# reader) | ● | ● | ◐ | ○ |
| N8 Small to learn and keep | ● | ◐ | ◐ | ● | ● | ● |
| Can be tested against before the app exists | ○ | ○ | ● | ○ | ○ | ○ |

## What I think

There are really two things here that should not be mixed:

1. **The screen's facts**: what is on it, what kind, required, choices, where it leads. These must
   be exact and readable by Lirenet. They are the spec.
2. **The screen's picture**: so a person sees it. This helps reading but proves nothing.

ASCII gives only the second, and badly. Mermaid blocks, Salt, draw.io and screenshots give only the
second. The card and table give only the first. **Wireframe and HTML give both from one source**,
and of the two, Wireframe reads as text and HTML does not.

So my recommendation:

- **Wireframe as the source of a screen**, with a **spec profile**: the controls and their facts
  matter, layout attributes are allowed but never checked, a field's label is its name, and a
  `message=` per field. Lirenet gets a small C# reader for that profile. Every screen gets a
  committed SVG beside it, refreshed by a check, so GitHub shows the picture.
- **The field table stays, but is generated** from the Wireframe as a read-only view, for those
  who want columns (limits, messages, defaults). Written in one place, shown in two.
- **The annotated screenshot beside it** once the app exists, for "does it look right", drafted by
  Bruinet and never the judge.
- **HTML only for a clickable prototype of a new system**, and then generated from the Wireframe
  if possible, not written by hand. Worth trying once on Frainet's first new journey.
- **ASCII for chats and quick sketches**, never as a spec.

If extending Wireframe is more than you want to take on now, the fallback is **4a**: the screen card
and field table only, with the screenshot beside it. It is the least to build and the most exact,
and nothing is lost that cannot be added later.

## A small trial

1. Write the department form and the contact form (40 fields, six sections) in Wireframe, in the
   spec profile.
2. Write the same two as card and table (4a), and as HTML.
3. Count lines; time the owner answering "is Sales price in Financial required?" from each.
4. Let an agent draft both screens from the Scraper's recording in each format; count the
   corrections a person had to make.
5. Write the C# reader for the profile on the department form only; see how big it gets.

## Questions

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Are you willing to extend Wireframe for specs (a profile, `message=`, label as name), or should it stay a general wireframe tool? | Extend it, behind a profile, so the general tool is unchanged |
| Q2 | Does Lirenet read Wireframe with its own C# reader, or by calling the `wire` CLI for a JSON tree? | A C# reader for the profile only: Lirenet stays free of Node, and the profile is small |
| Q3 | Committed SVGs: rendered by a check in CI, or by the author? | By a check that fails when an SVG is out of date, as `PublicAPI` files are kept |
| Q4 | Are the ids (`:Name`) the page object's member names? | Yes: then a screen and its page object can be compared by name, and drift found |
| Q5 | Does the Wireframe replace the field table, or does the table stay as the written source? | The Wireframe is the source; the table is generated from it |
| Q6 | Wireframe's controls are generic (`TextInput`, `Dropdown`). Do we need Bouw7's own (a Vuetify select, a `b7-data-table`)? | No: a spec says what a person sees, so generic kinds are right; the page object knows the real control |
| Q7 | What about very large forms (the contact form's 40 fields)? | Sections as `Panel` or `Tabs`; the picture shows them closed, the generated table lists every field |
| Q8 | Do Mermaid diagrams elsewhere (flow, states) move to Wireframe too? | No: Mermaid stays for diagrams; Wireframe is only for screens. The `@` links let Lirenet check the flow against the screens |
| Q9 | Is the Mermaid plugin for Wireframe worth using where a Mermaid renderer can load plug-ins (the Presenter, docs sites)? | Yes there; on GitHub the committed SVG does it |
