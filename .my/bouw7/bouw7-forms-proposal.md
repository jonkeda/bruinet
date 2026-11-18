# Proposal: pages a person can read: named, typed fields, sections, rows, and a review that asks for them

Status: **accepted in part and built for Contacts** (2026-11-17). The owner said yes to Q5, Q6 and Q7 and asked for
Contacts to be built; Q1 to Q4 were taken as proposed. Built: A1, A2 and A3 for the contact form; B1 to B3 and B5;
C1 for the contact's page; E1, E2, E3 and E5 for the Contacts list (E4's plain message is not); F1 to F3; G in the
skill and in `600-Improvements.md` (review 6). Not built: the draft tool and the drift check (A4, A5, C2, E6), the
page of the forms (D1), the counts by a command (F4, F5, G6), B4, and every form, list and page but Contacts'.
What it gave is measured in review 6. **Later the same day the rest of the suite followed, by hand and by script, without the draft
tool:** the seven other forms, every page's buttons, headings and tabs, and a typed row for 32 lists (the task list:
"Every page and every spec on the new models"). Still not built: the draft tool, the drift check, the page of the
forms, the counts by a command. Date of the proposal: 2026-11-17.

| | |
| --- | --- |
| What | How the Bouw7 suite models a form today, why that is hard on whoever writes the next test, and what to change |
| Asked by | The owner, after reading the page objects: a generic `FormRow` works but is not easy, because a person must still find out what the fields are called and what kind each is; and the contact form has sections of its own (Financial, Payment term, Surcharges, Price per hour type, Attributes) |
| The short version | A form becomes a class with **one member per field, named after its label and typed by its kind**, and **one member per section** that holds that section's fields. A list gets **a row with a member per column** and its own actions. The members are drafted from the running app by a tool, not typed by hand, and a check says when the app and the class have drifted apart. The words in the specs stay as they are. So that this is not missed again: **the skills get a rule** against members that stand for fixed structure by a text, and **the improvement review stops asking only "what is faster"**: it also asks how easy the pages and controls are to use, and whether the tests can be trusted |
| The numbers | Counted in the suite and read from acc with `scrape look` on 2026-11-17. The costs are estimates |

## 1. How a form is modelled today

Seven forms have a page object: contact, project, hour entry, department, category, day off, absence, quotation.
All but one look like this:

```csharp
[BddControl("the field {label}")]
public FormRow<ContactCreatePage> Field(string label) => new(this, label);
```

and a test or a step then says which control the row is:

```csharp
form.Field("Name").Text.SetText("…");          // a text field
form.Field("Contact Type").Select.SelectByText("Client");   // a select
form.Field("Start date").Date.PickDate(…);      // a calendar
```

`FormRow` itself offers `Text`, `Select`, `Checkbox`, `Date` and `Input` on every row, whatever the row is.

| What a person must know, and where it is today | |
| --- | --- |
| What the fields of a form are called | Nowhere in the code. On the screen, or in a `scrape look` |
| What kind each field is | Nowhere. Found by trying: "Start date is read-only: it takes no text" cost a failed run and 15 minutes (friction #102) |
| Which fields are required | In a spec, for the few a rule names |
| Which section a field is in | Nowhere. The contact form's sections are not modelled at all |
| That a name is misspelt | Only on acc: "the field Nmae" binds, opens a browser, and fails after 15 seconds of looking |

The specs use 50 different "the field …" phrases; none of them is checked against a form before a browser opens.

The hour entry dialog is half-way: it has a property per field (`AmountOfHours`, `TypeOfHour`), but each is still
an untyped `FormRow`, so the caller still picks `.Text` or `.Select`.

## 2. What the contact form really holds

Read from acc (`/contact#/create`, each section opened in turn):

| Part | Fields | Kinds |
| --- | --- | --- |
| Contact information (always open) | 16: Contact Type, Name, Street, House number, Postcode, City, Phone, Mobile number, E-mail address, CoC no., VAT number, OIN, IBAN, Debtor number, NI No., GLN | 1 select, 15 text |
| Financial | 12: Blocked account description, G- account percentage, Labour cost share percentage, Second blocked account description, Hourly rate, Sales price, Agreed hourly rate, Reverse charge VAT, Invoice mail, Default contact person, Addition to subject invoice e-mail, Reminder mail | text, number, 1 checkbox |
| Payment term (in days after invoice date) | 3: Expiration time, 1st reminder, 2nd reminder | number |
| Surcharges | 7: delivery ticket, work outsourced, Purchase Order, Materials, Equipment, Subcontracting, Other costs | number |
| Price per hour type | Cost price and Sales price, per type of hour | number |
| Attributes | The organization's own fields | whatever the organization made |

Two things follow from this table:

- **A label is not unique on the form.** "Sales price" is in Financial and in Price per hour type. `Field("Sales price")`
  finds the first one. A field must be asked for within its section, so sections are needed, not only nice.
- **A closed section's fields are not on the page.** Today a test would have to know that, and open the section
  first. Nothing in the page object says so.

The contact's own page (B7.CON.03) has the same shape one level up: tabs General, Financial, Sale, Files and Prices,
reached today through `Tab(name)`, `Part(title)` and `Detail(label)`, all by text.

## 3. Proposal

### A. A member per field, typed by its kind

| # | Change | Why | Size | Where |
| --- | --- | --- | --- | --- |
| A1 | **Typed rows**: `TextRow`, `NumberRow`, `SelectRow`, `CheckRow`, `DateRow`, each a row of the form that offers only what its kind can do (`SetText`; `Pick` and `Choices`; `Check`; `PickDate`), and all of them `Label`, `IsRequired`, `Message` | The kind is said once, where the form is described, and the editor offers only what works. "I fill in" on a date does not compile | S | The suite (`Components/Forms`) |
| A2 | **A form is a class with a member per field**, named after the label: `form.Name`, `form.ContactType`, `form.StartDate`. Each carries its phrase: `[BddControl("the field Name")]` | A person reads the form's fields in the class, and the editor completes them. The spec's words do not change | M for the seven forms, when drafted by A4 | The suite |
| A3 | **A field a spec names must exist**: with a member per field, "the field Nmae" fails the bind check (3 seconds, no browser) with the fields the form does have | Today it fails on acc after 15 seconds, once per run | Comes with A2 | The suite; nothing to build |
| A4 | **The members are drafted, not typed**: `bruinet scrape draft-form --url /contact#/create` writes the class from the running app: a member per row, its kind from what the row holds (a select, a checkbox, a number input, a read-only input with a calendar), its section from where it sits | 40 fields on the contact form alone; typed by hand they are 40 chances for a wrong name or kind. It is also how a form gets its class in minutes | M | Bruinet (Scraper). The row's layout (label left, field right) is the suite's, given as a pattern in `scraper.config.md` |
| A5 | **A drift check**: `bruinet scrape verify` (it exists for pages) compares a form's class with the form on acc: a field the app has and the class lacks, one the class has and the app lacks, a kind that changed | The app's form will change. Today nobody hears of a new required field until a create fails | S, on top of A4 | Bruinet |

What a form looks like after A1, A2 and A4:

```csharp
// ContactForm.scraped.cs: drafted from /contact#/create. Draft again to update.
public sealed partial class ContactForm
{
    [BddControl("the field Contact Type")] public SelectRow<ContactForm> ContactType => Select("Contact Type");   // required
    [BddControl("the field Name")]         public TextRow<ContactForm>   Name        => Text("Name");            // required
    [BddControl("the field Street")]       public TextRow<ContactForm>   Street      => Text("Street");
    …
    [BddControl("Financial")]              public FinancialSection Financial => new(this);
    [BddControl("Surcharges")]             public SurchargesSection Surcharges => new(this);
}
```

and a test:

```csharp
form.ContactType.Pick("Client");
form.Name.SetText($"{RunMarker.Current} Test contact");
form.Financial.HourlyRate.SetText("45");      // opens Financial first, see B2
```

### B. A section is a part of the form

| # | Change | Why | Size | Where |
| --- | --- | --- | --- | --- |
| B1 | **`FormSection`**: a part found by its title, with its own fields as members, `IsOpen`, `Open`, `Close` | "Sales price" in Financial and in Price per hour type become two members that cannot be mixed up | S | The suite; the open-and-close of a Vuetify expansion panel in `Bruinet.Web.Vuetify2` (`VExpansionPanel`, unit-tested) |
| B2 | **A field of a closed section opens its section first** | A test says what it means (`form.Financial.HourlyRate`), not how the page folds | S | The suite |
| B3 | **In a spec a field is named with its section**: "the field Hourly rate under Financial"; a field of the always-open part stays "the field Name" | The spec says where the person finds the field, and the phrase is unique | S | The suite; one line in the `spec-from-app` skill |
| B4 | **Rows that repeat** (Price per hour type: a cost price and a sales price per type of hour) are a list of rows by their type of hour: `form.PricePerHourType.For("Gewerkte uren").SalesPrice` | The fields are not fixed; the types of hour are the organization's | S | The suite |
| B5 | **Attributes stays by label**: `form.Attributes.Field("…")` | The fields are the organization's own and differ per organization: there is nothing to name ahead | None | The suite |

### C. The same for a detail page's tabs

| # | Change | Why | Size | Where |
| --- | --- | --- | --- | --- |
| C1 | **A tab is a part with its details as members**: `contact.Financial.Iban`, in place of `Tab("Financial")` and `Detail("IBAN")` by text | The same two problems as a form: the names are nowhere, and a label may be on two tabs | M for the contact's five tabs | The suite. Bruinet's `TabPageBase` (AD-039) is the base; this was left over as C3 of the speed proposal |
| C2 | **Drafted by the same tool** as A4, one tab at a time | 5 tabs on a contact, 3 on an employee, more on a project | Comes with A4 | Bruinet |

### D. For whoever writes a spec and reads no code

| # | Change | Why | Size | Where |
| --- | --- | --- | --- | --- |
| D1 | **A page of the forms**, written from the classes: per form its sections, fields, kinds, and which are required (`.testing/180-Forms.md`) | Whoever writes "the field …" in a spec looks the name up, in place of opening acc | S | Bruinet (a `bruinet` command over the suite's `BddControl` members), run in the site loop |

### E. A list's row is a part too

Bruinet has the base for it and the suite does not use it: `CollectionObjectBase<TParent, TSelf, TItem>` with
`ItemObjectBase<TSelf>` (the `html-control` skill: "a collection with rows"). `HeimdallTable` is a plain table that
reads a cell by its header as text: 16 such reads in the suite and the filters beside them ("Name" alone 8 times).

| # | Change | Why | Size | Where |
| --- | --- | --- | --- | --- |
| E1 | **`HeimdallTable<TScope, TRow>`**: a collection whose row is an item container; a row type per list with **a member per column** (`row.Name`, `row.ContactType`) | The columns of a list are in the class, the editor completes them, and a column a spec names is checked before a browser opens | M | The suite (`Components/Collection`); it builds on Bruinet's collection base |
| E2 | **A row's actions are members**: `row.Open()`, `row.Pen`, `row.Copy` | The absence journey spent four failed runs finding and pressing a row's pen (friction #109); the contact, project and employee lists each have a hand-written "open the … named" step | S per list | The suite |
| E3 | **The filters are members**: `list.Filters.Name`, `list.Filters.ContactType`, typed like the form's rows | The same two problems as a form: the name is a text, and the kind (a text or a select) is the caller's guess | S | The suite |
| E4 | **A cell is found by its header, never by its place**, and a column that is not shown says so: "the column Description is not shown (the list shows: …)" | These lists have a column picker; Day off per employee shows no column at all in this organization. "Not found" after 20 seconds tells nobody that | S | The suite |
| E5 | **Finding a row is one thing, written once**: `list.Row(r => r.Name == name)`, waiting for the list to have loaded and stopped changing | Today each kind of record has its own steps in the fixture ("the list shows the contact named …", "… the department named …", "… the category named … with the code …"): 31 steps there, most of them this | S | The suite |
| E6 | **Drafted by the same tool** as the forms (A4): a row type from a list's columns, which a look already reads | 29 pages are one list each | Comes with A4 | Bruinet |

After E1 to E5:

```csharp
var row = contacts.List.Row(r => r.Name == name);     // waits for the list; says what it showed when there is none
Assert.Equal("Supplier", row.ContactType.GetText());
row.Open();
```

In a spec the words stay: "the list names the columns …", "the list shows the contact named … as …". What changes
is that one step serves every list.

### F. The skills get the rule that was missing

Why this was not caught: the shortcut was mine, and nothing said no.

| What happened | What it shows |
| --- | --- |
| One member with a `{label}` bound every field of a form in a minute, and five improvement reviews in a row counted that as a gain | The reviews measured minutes and failed runs only (section G) |
| The Scraper's draft of the contact form names its inputs `Input584`, `Input586` and leaves them to the library | The tool gave the hand-written part nothing to build on (A4) |
| `html-ui-test` says a page object owns the page's structure, and says nothing of a member that takes that structure as a text | The rule is missing |
| "Controls accepted" by a person is still unticked for Hours, Projects and Contacts in the task list | The checkpoint that would have caught it was skipped, and nothing said it was overdue |

| # | Change | Size | Where |
| --- | --- | --- | --- |
| F1 | **`html-ui-test`, and the same in the other `*-ui-test` skills**: a member that takes a text (`{label}`, `{name}`, `{title}`) is for **data** only: a row by its name, an organization's own attribute. The app's fixed fields, sections, tabs, columns and buttons are members with names. With the reason and one example of each | S | Bruinet (skills) |
| F2 | **`html-control`**: a form's row and a list's row are named as cases of "a component with named parts" and "a collection with rows", pointing at the bases; a control that leaves its kind to the caller (`.Text` or `.Select` on every row) is named as what not to make | S | Bruinet (skills) |
| F3 | **`spec-from-app` and `scenario-from-spec`**: a form or a list a spec touches gets its drafted class first; a spec's "the field …" is looked up in it, not on acc | S | Bruinet (skills) |
| F4 | **The bind check counts them**: `Every_step_binds` (or a `bruinet` command beside it) prints how many members stand for fixed structure by a text, and where | S | Bruinet |
| F5 | **A person's checkpoint that is skipped is said**: the site loop's report lists the task list's person checkpoints that are open for longer than one review | S | Bruinet (`site-loop.ps1`) |

### G. The improvement review asks more than "what is faster"

The review as it is (`improvement-review` skill) has one question: what would make the next journeys **cheaper**.
Its measures are minutes from nothing to green, failed runs per journey, and the suite's time; its proposals are
"ranked by time saved per journey against the cost to build". Every review since the first followed that, and did
what it was asked: the work got about ten times faster, and the page objects got harder to use, because a generic
member is always the fastest thing to write. A loop that measures one thing improves that one thing at the cost
of what it does not measure.

**Proposed: four questions, each with its own measures.** A review reports all four, every time.

| Question | For whom | Measures, counted the same way each review | Today (2026-11-17) |
| --- | --- | --- | --- |
| **Speed**: what does a journey cost? | Whoever waits for it | Minutes from nothing to green; failed runs per journey; the whole suite's time | About 4 min a journey that reads, 25 to 50 one that writes; 0.4; 134 examples in 12 m 18 s |
| **Ease of use**: can the next person write a test from the pages and controls alone? | Whoever writes the next test or spec | Members that stand for fixed structure by a text, of all members; forms and lists without named members; controls that leave their kind to the caller; steps written in the fixture for one kind of record; a **cold write** (below) | 16 of 91 members; 7 of 7 forms, 29 of 29 lists; 1 control (`FormRow`); 31 steps; never tried |
| **Trust**: does a green run mean what it says, and does a run leave acc as it found it? | Whoever reads the report | Examples that failed once and passed on a rerun; records a run left behind and for how long; clean-ups that failed; rules whose example checks less than the rule says | 2 unsteady this session (#94, #111); 3 leftovers, up to 10 min (#106, #108); 1 hidden clean-up failure (#108); not counted |
| **Reach**: how much of the app do the tests say something about? | The owner | Routes at each depth (opens, list, detail, form, make) of the map's 76; journeys of the plan done; pages that cannot be read | 76 open; 30 specs; 7 pages unread (#100) |

**The cold write** is the one new measurement. Once per review a fresh agent gets a small task ("write a test that
makes a contact with an hourly rate of 45"), the suite's page objects and controls, and **no access to acc and no
look**. Counted: did its test bind at once, did it pass on the first run, and what did it have to guess. Bruinet's
`ai-item` skill already checks its own tools this way. It costs about ten minutes, and it is the nearest thing to
"is this easy for the next person" that can be counted.

**What else changes in the review:**

| # | Change | Why |
| --- | --- | --- |
| G1 | **A proposal names which question it serves, and what it costs the other three.** "Saves 3 min a journey; adds 8 members by text" is a proposal the owner can weigh. One that hides its price is not one | The generic form row would have had its price on the page in review 2 |
| G2 | **Ranking is not by time alone.** The five proposals are ranked by what they give against their cost, across the four questions; when a measure got worse since the last review, a proposal for it is on the list, and first | Otherwise speed always wins: it is the easiest to count |
| G3 | **A measure that got worse is said at the top of the review**, in a line, before any proposal | So a slide is seen in the review after it starts, not five reviews later |
| G4 | **The Reviews table gets a column per question**, not three columns of speed | One row shows whether the work got better or only faster |
| G5 | **The review reads the page objects**, not only the run reports: the newest pages and controls are in its "Look" table, with F4's count | The reports show what ran, not what it is like to write the next one |
| G6 | **The counts are made by a command**, not by hand: `bruinet suite facts` (members by text, forms and lists without members, fixture steps) beside what the loop already counts, printed in the site loop's report | A measure that takes a person ten minutes is skipped; and the same count each time |
| G7 | **The name**: "improvement review" stays; "what would make the next ones cheaper" in the skill becomes "what would make the next ones cheaper, easier, more to be trusted, or reach further" | The skill's first sentence is what an agent optimises for |

What does not change: at most five proposals, each with a measure; the owner picks; what is built is measured
the same way before and after; a proposal turned down stays in the file with why.

## 4. What stays, and what goes

- **The specs' words stay.** "I fill in the field Name with …" and the 50 phrases in use keep working: the phrase
  moves from one member with a `{label}` to a member per field.
- **`FormRow` stays** as the base of the typed rows, and as the way to a field nobody named (Attributes).
- **`Field(string label)` goes** from each form once its fields are members, so there is one way, and A3 holds.
  Until a form is done it keeps its `Field`.
- **`GetColumnText(row, "header")` goes** from tests and steps once a list has its row; `HeimdallTable` keeps
  reading by header inside, which is how a row's member finds its cell (E4).
- **The steps get stricter.** "I fill in {control} with" takes a text or number row; "I pick … in {control}" a select
  row; "I pick the date … in {control}" a date row. A spec that fills in a select stops at the bind check.

## 5. Order and cost

| Step | What | Cost | Done when |
| --- | --- | --- | --- |
| 1 | A1 and B1 by hand on one form, the contact form, with its five sections; B2 to B5 | Half a day | B7.CON.02 green 3 runs with members only; a new example that fills in a field of Financial |
| 2 | A4: the draft tool, tried by drafting the contact form again and comparing it with step 1's | 1 day | The drafted class equals the hand-written one, or the differences are the tool's to fix |
| 3 | A2 for the other six forms, drafted; `Field(label)` removed from each | Half a day | Every spec green; the bind check red on a planted wrong field name |
| 4 | A5 and D1 | Half a day | A planted extra field in a class is reported; the forms page is in the morning report |
| 5 | C1 and C2: the contact's tabs, then the employee's and the project's | 1 day | B7.CON.03 green 3 runs with members only |
| 6 | F1 to F3 and G: the rule in the skills, and the review's four questions (the skill, the `600-Improvements.md` table, today's numbers as review 6's baseline) | Half a day | A review written the new way; the cold write tried once on the contact form as it is today, so step 1 has a before |
| 7 | E1 to E5 by hand on the Contacts list, then E6 and the other lists drafted | 1 day and a half | B7.CON.01 green 3 runs with a typed row; the per-record list steps of the fixture replaced by one |
| 8 | F4, F5 and G6: the counts as a command, in the loop's report | Half a day | The report of a run shows the four questions' counts |

About six days. Step 6 is first in time if the owner agrees: it costs half a day, changes no test, and makes every
later step measurable. Step 1 alone then answers both of the owner's first points for the form that prompted them.

## 6. Risks

| Risk | What limits it |
| --- | --- |
| The draft tool reads a kind wrong (a select reads as a text field to `scrape look` today: Contact Type shows as "Select [text]") | A4 reads the row's own markup (`v-select`, `v-input--checkbox`, `type=number`, read-only with a calendar), not the look's summary; step 2 compares it with a hand-written form before it is trusted |
| A label changes in the app and a member's name with it | A5 says so; the member is drafted again; the spec's phrase changes in one place and the bind check lists every spec that used the old one |
| Labels depend on the language of the account | They do today as well. The accounts are English; a member's name is the English label |
| Many small classes | They are drafted and marked as such (`.scraped.cs`), as the page drafts already are; a person writes only what the tool cannot know |
| The stricter steps stop a spec that worked | That is the point of A3; step 3 finds each one before it is merged |
| Typed rows for 29 lists is a lot of code for lists nobody writes a journey for | They are drafted; and a list only gets its row when a spec reads more of it than its columns' names |
| Four questions make the review longer and nobody reads it | Still five proposals at most; the four questions are one table of numbers, made by a command (G6) |
| The cold write measures the agent, not the pages | The same task and the same kind of agent each review; what is compared is one review with the next, not a score |
| Ease of use is bought with speed: a member per field is slower to write than one by label | Not when drafted (A4). And that trade is now on the page (G1) for the owner to make, not made silently |

## 7. Questions for the owner

Answered 2026-11-17: **Q5 yes, Q6 yes, Q7 yes**. Q1 to Q4 were not answered and are built as proposed: "under" for a
field in a section, no `Field(label)` on the contact form, the form by hand first, the tabs included.

Added by the owner while it was built: the text editor (Comments) is a component of its own. It is one now: a
`RichTextRow` on the form, with a `RichTextEditor` (its text and a member per toolbar button).

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | Is "the field Hourly rate under Financial" the wording for a field in a section, or should the section be opened in a step of its own ("When I open the section Financial")? | Under: the spec says where the field is, and the folding is the page's business |
| Q2 | Should `Field(label)` go once a form has its members (one way, checked early), or stay beside them? | Go, except for Attributes |
| Q3 | Start with step 1 on the contact form by hand, or build the draft tool first? | Step 1 first: it shows what the tool must produce, in half a day |
| Q4 | Are the tabs of the detail pages (C) part of this, or a later piece? | Part of the proposal, after the forms |
| Q5 | Are speed, ease of use, trust and reach the right four questions for the review, and is one missing (cost of upkeep when the app changes, say)? | These four; upkeep shows up under ease of use (the drift check, A5) until it needs a line of its own |
| Q6 | May the review spend ten minutes and one fresh agent on a cold write each time? | Yes: it is the only measure of ease that is not a count of code |
| Q7 | Should the skills and the review change first (step 6), before any form is rebuilt? | Yes: half a day, no test changes, and the rebuild then has a before and an after |
