# Design: how a step names a control

Status: **accepted and built for Contacts** (2026-11-17). The owner said "do all 4" to the questions of section 8:
both "in" and "under"; existing specs change as they are touched, B7.CON.02 first; the table step is wanted; built
on Bruinet's `move` branch.

**Names since this was written** (2026-11-18): Bruinet is one product in four modules. Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet **Migrate**; `lirenet check`, `lirenet trace`, `frainet check` and `frainet trace` are `bruinet spec check` and `bruinet spec trace`. The decision is AD-044 (`.docs/decisions/ad-044-one-product-four-modules.md`). The text below keeps the names it was written with.

| Part | Built | Not built, or different from the design |
| --- | --- | --- |
| A. The label once | Members of a kind are named after their member; a row takes its label from its member; labels are matched in any case | 20 `[BddControl]` lines are left on the contact's form and page, not about 10: the five tabs, three buttons and a menu keep theirs, and 11 labels need one |
| B. The kind is optional | `[BddKind]` in Bruinet; the kind's word before or after a name, or left out; the kind tells two controls of one name apart | Kinds are on the suite's types only (field, section, part, detail). Bruinet's own controls (a button, a list) have none yet. A quoted name (B4) is not built |
| C. A place only when needed | Parts are looked in four levels deep; "in" and "under"; the refusal says how; declared words are tried first, so nothing that bound before changed | The refusal does not offer a near miss ("did you mean"), and its list of names is not grouped by part |
| D. A form filled in with a table | "I fill in the form with:" and "the form shows:", as steps of the suite's fixture; Lirenet's format check accepts a step's table | "I fill in Financial with:" (a part, D2) is not built: a step on a control takes no table in Bruinet today |

**Then, on the owner's word ("rewrite all the specs and all the pages to the new models"), for the whole suite:** every
page has named members and all 30 specs use the short wording; the task list has what was done and what is left
("Every page and every spec on the new models").

Measured: Bruinet's binder tests 347 pass (21 new), Lirenet's 40 (1 new); B7.CON.01, .02 and .03 pass with the names
from the members, and B7.CON.02 in the short wording. Date of the design: 2026-11-17.

| | |
| --- | --- |
| What | How a step of a spec or scenario says which control it means, after the contact form got a member per field |
| Asked by | The owner, on reading `the field Expiration time under Payment term`: there must be a better way for "under Payment term", and do we really want "the field" each time |
| Builds on | `bouw7-forms-proposal.md` (members with names), and how Bruinet resolves a control today (`Bruinet.Uat`: `BddControlNames`, `UatPageScope`, `StepNames`) |
| The short version | **A control is named by what the page calls it, and nothing else.** "the field" becomes a word a writer may add, not one they must; the page object says it once per kind of control, not once per field. "under Payment term" leaves the control's name: a step says where a field is only when two controls share a name, in words the binder makes from the part's own name. And a form is filled in with one step and a table. The 87 `[BddControl]` lines of the contact's form and page become about ten |

## 1. What is wrong today

The contact form as it was built this week:

```csharp
[BddControl("the field Hourly rate under Financial")]
public NumberRow<ContactFinancialSection> HourlyRate => Number("Hourly rate");

[BddControl("the field Expiration time under Payment term")]
public NumberRow<ContactPaymentTermSection> ExpirationTime => Number("Expiration time");
```

```gherkin
When I pick "Client" in the field Contact Type
And I fill in the field Name with "{run} Rated contact"
And I fill in the field Hourly rate under Financial with "45"
```

| What | Counted | Why it is wrong |
| --- | --- | --- |
| The label is typed three times per field: in the member's name, in the attribute, in the locator | 46 attributes on the contact form, 41 on the contact's page | Three places to keep the same; the attribute is the one a person reads least and a step depends on most |
| "the field" is in every attribute and every step | 86 of the specs' 698 step lines | It says the kind of thing, which the reader sees and the page object knows from the member's type. It makes every step longer and no step clearer |
| "under Payment term" is part of the control's name | 22 attributes | Where a field sits is the form's structure, which the page object already has (`form.PaymentTerm.ExpirationTime`). Written into a text, it is said twice, and it must be said in every step even when no other field is called that |
| It was needed for no field | 0 of the 22 section fields share a name with another field of the form today | I added it for all of them because one could collide ("Sales price" is also in Price per hour type, which has no members yet) |

Why it came out this way: three things about how Bruinet resolves `{control}` today.

1. **One level deep.** A step finds a page's own controls, and the named members of the parts the page holds. Not a part's part.
2. **A name is the whole phrase.** `[BddControl("the field Name")]` matches "the field Name" and "field Name" (a leading "the" may be left out), nothing else. The kind word is part of the name because there is nowhere else to put it.
3. **Two controls with the same words are an error**, and the only way to tell them apart is to give them different words by hand.

## 2. The design

### A. A control's name is its label, said once

The label is given once, where the member is declared, and is both what the control is found by and what a step
calls it:

```csharp
public NumberRow<ContactFinancialSection> HourlyRate => Number("Hourly rate");
```

No attribute. The binder reads the name from the member: `HourlyRate` is "Hourly rate". Where the label is not what
the member's name gives (punctuation, a digit first, an abbreviation), the label is said in the one attribute that
then also feeds the locator:

```csharp
[BddControl("CoC no.")]      public TextRow<ContactCreatePage> CocNumber     => Text();
[BddControl("1st reminder")] public NumberRow<ContactPaymentTermSection> FirstReminder => Number();
```

| # | Change | Where |
| --- | --- | --- |
| A1 | A member of a named kind (B1) is a control with the name its member gives: `HourlyRate` is "Hourly rate". Today only members whose name or type ends in a known word (`Button`, `Field`, `Control`) are | `Bruinet.Uat` (discovery) |
| A2 | A row made without a label (`Text()`, `Number()`) takes it from its member's `[BddControl]`, else from its member's name: one place for the label | The suite (`FormRowBase`, `FormSection`); the caller's member name is known to the compiler |
| A3 | The draft tool writes the member's name from the label, and the attribute only when the two differ | Bruinet (the forms proposal's A4) |

Of the contact form's 39 field members, 6 need the attribute (CoC no., E-mail address, NI No., G- account
percentage, 1st reminder, 2nd reminder); 33 need nothing.

### B. The kind is a word a writer may use, declared once per kind

A kind of control says its word once, on its type:

```csharp
[BddKind("field")]   public abstract class FormRowBase<TParent, TSelf> …
[BddKind("section")] public abstract class FormSection<TParent, TSelf> …
[BddKind("tab")]     …        [BddKind("button")] …      [BddKind("list")] …
```

A step may then name a control in any of these ways, and they all mean the same control:

| The step says | Reads as |
| --- | --- |
| `Name` | the control called Name |
| `the Name field`, `the field Name` | the control called Name, which is a field |
| `the Create button`, `the button Create` | the control called Create, which is a button |

The rule for a writer: **say the name; add the kind when the sentence needs it.** "When I fill in Name with …" needs
nothing. "Then the Create button is shown" reads better with its kind. Nobody has to write "the field" 86 times,
and nobody is stopped from writing it once.

| # | Change | Where |
| --- | --- | --- |
| B1 | `[BddKind("…")]` on a control's type (inherited): the kind's word | `Bruinet.Bdd.Abstractions`; the suite's and the libraries' controls |
| B2 | Resolving `{control}`: an optional "the", then the name with the kind's word before or after it, or without it | `Bruinet.Uat` (`BddControlNames`) |
| B3 | **The kind is also how two controls of one name are told apart**, with no hand-made names: the contact's page has a tab, a part and (on the form) a section all called Financial. "the tab Financial" and "the part Financial" are different controls because their kinds differ; plain "Financial" is refused, and the message offers both | `Bruinet.Uat` |
| B4 | A name that holds a word a step's own phrase uses ("with", "in") may be quoted: `I fill in "Paid with card" with "yes"` | `Bruinet.Uat`; it is how every other argument is written already |

### C. Where a control sits is said only when it must be, and never typed into a name

The page object has the structure: `form.PaymentTerm.ExpirationTime`. The binder uses it.

| # | Change | What a step says | Where |
| --- | --- | --- | --- |
| C1 | **Every part is looked in, to any depth.** A name that is on the page once needs no place | `I fill in Expiration time with "30"` | `Bruinet.Uat` (today: one level) |
| C2 | **A name that is on the page twice is said with its part**, in words the binder makes from the part's name: `<name> in <part>`, or `<name> under <part>` | `I fill in Sales price in Financial with "60"` | `Bruinet.Uat` |
| C3 | **The refusal says how.** "Sales price is on the Contact Create page twice: say Sales price in Financial, or Sales price in Price per hour type" | (the bind check, before a browser) | `Bruinet.Uat` |
| C4 | **A field that becomes ambiguous later breaks nothing silently**: the steps that say it bare stop at the bind check with C3's message. Nothing has to be put in a name today for a collision that may come | | Follows from C2 |

"in" and "under" both work, because both read well somewhere: a field is *under* a section's heading, a part is
*in* a tab. The binder does not care which is written.

### D. A form is filled in with one step and a table

Most of a write journey's steps are one "I fill in" or "I pick" after another. The form's page object knows each
field's kind, so the spec does not have to:

```gherkin
When I fill in the form with:
  | Contact Type | Client              |
  | Name         | {run} Rated contact |
  | Hourly rate  | 45                  |
And I create the contact
Then the Contact View page is open
And Hourly rate says "€45.00"
```

| # | Change | Where |
| --- | --- | --- |
| D1 | "I fill in the form with:" and a table of name and value. Each row goes to its field by name (A, C) and is typed, picked, ticked or chosen in a calendar by what the field's row is | The suite (`Bouw7Verbs`): a row type already says how it takes a value; step tables exist in Bruinet |
| D2 | The same for a part: "I fill in Financial with:" for fields that are only unique within their part | The suite |
| D3 | "the form shows:" and a table, to read a form back the same way | The suite |

A single "I fill in Name with …" stays for an example that is about one field.

## 3. Before and after

The page object, for one section:

```csharp
// Before: 3 fields, 3 attributes, each label three times.
[BddControl("the field Expiration time under Payment term")]
public NumberRow<ContactPaymentTermSection> ExpirationTime => Number("Expiration time");

[BddControl("the field 1st reminder under Payment term")]
public NumberRow<ContactPaymentTermSection> FirstReminder => Number("1st reminder");

// After: the label once.
public NumberRow<ContactPaymentTermSection> ExpirationTime => Number();

[BddControl("1st reminder")]
public NumberRow<ContactPaymentTermSection> FirstReminder => Number();
```

The spec, for the example that makes a contact with a rate:

```gherkin
# Before
When I pick "Client" in the field Contact Type
And I fill in the field Name with "{run} Rated contact"
And I fill in the field Hourly rate under Financial with "45"
And I create the contact
Then the Contact View page is open
And the title says "{run} Rated contact"
And the detail Hourly rate says "€45.00"

# After, step by step
When I pick "Client" in Contact Type
And I fill in Name with "{run} Rated contact"
And I fill in Hourly rate with "45"
…
And Hourly rate says "€45.00"

# After, with the table (D)
When I fill in the form with:
  | Contact Type | Client              |
  | Name         | {run} Rated contact |
  | Hourly rate  | 45                  |
```

| Counted | Today | After |
| --- | --- | --- |
| `[BddControl]` lines on the contact's form and page | 87 | about 10: the labels a member's name cannot give |
| Places a field's label is typed | 3 | 1 |
| Step lines that must say "the field" | 86 | 0 |
| Fields that must say their section | 22 | 0 today; one when Price per hour type gets its members (Sales price) |
| Old steps that stop binding | | none: "the field Name" is the name with its kind, and "under Financial" is a place (B2, C2) |

## 4. What stays the same

- **A member that takes a text is for data only** (the rule in the skills). `Row("Arend de Krom")` in a list stays a
  phrase with a placeholder; this design is about the names of fixed things.
- **The bind check is where a wrong name fails**, with the page's controls listed. With C1 the list is longer, so it
  is grouped by part, and a near miss is offered first ("no control 'Hourly rat': did you mean Hourly rate, in
  Financial?").
- **Every existing step keeps binding.** Old and new wording can stand side by side in one spec; a spec is changed
  when someone touches it, not all at once.

## 5. Risks

| Risk | What limits it |
| --- | --- |
| Looser matching finds the wrong control: with every part searched, a name that was unique on the page is now also a column, a filter or a detail | A name found twice is refused, never guessed (C3). The kind tells most apart (B3): "the filter Name", "the column Name", "the field Name" |
| A step's own words swallow part of a name: `I pick {choice} in {control}` and "Sales price in Financial" | The binder already reads a step's fixed words first and its arguments after; a name that still clashes is quoted (B4). To be proven in Bruinet's own binder tests before any suite uses it |
| The name from a member differs from the label on the screen by a letter (`EmailAddress` is "Email address", the screen says "E-mail address") | The label is what finds the row, so a wrong name fails on its first run; the drift check (the forms proposal's A5) compares names with the app's labels; the draft tool writes the attribute where they differ |
| The table step hides which control a value went to | The step's failure names the row, the field and its kind; the run's step log lists each field as it is filled |
| Two ways to say everything ("Name", "the field Name") make specs uneven | The `spec-from-app` skill says which to use: the bare name, the kind only where the sentence needs it. `lirenet check` can count "the field" as a style finding later |
| It is a change to Bruinet's binder, which every suite uses | Each part is additive: nothing that binds today stops binding. Bruinet's `Bruinet.Uat.Tests` and the Todo sample's scenarios are the gate |

## 6. Order and cost

| Step | What | Where | Cost | Done when |
| --- | --- | --- | --- | --- |
| 1 | B1 and B2: `[BddKind]`, and the kind's word optional before or after a name | Bruinet | Half a day | Bruinet's binder tests; the Bouw7 bind check passes unchanged, and "Name" alone binds |
| 2 | C1 to C3: every part searched, a place only when a name is twice, the refusal that says how | Bruinet | 1 day | A test with "Sales price" in two parts: bare is refused with both offers, each "in" binds |
| 3 | A1 and A2: names from members; the contact form and page lose their attributes | Bruinet, the suite | Half a day | 87 attributes become about 10; B7.CON.01 to .03 green 3 runs with their words unchanged |
| 4 | D1 to D3: the table steps; B7.CON.02 rewritten with them as the example | The suite | Half a day | The spec reads as in section 3; green 3 runs |
| 5 | The skills: `spec-from-app` and `scenario-from-spec` say how a control is named | Bruinet | 1 h | A cold write of a spec for the contact form uses no "the field" and no "under" |

About three days. Steps 1 and 2 are the ones that answer the two questions; 3 to 5 make the page objects and the
specs shorter.

## 7. Considered and not chosen

| Idea | Why not |
| --- | --- |
| Keep the attributes and only shorten them: "Hourly rate (Financial)" | Still typed by hand, still said in every step, still two controls told apart by a made-up name |
| A step that sets the place: "When I am in Financial", and the next steps mean that section | A step would mean something else depending on a line above it; a section already opens itself when a field is asked for, so the step would do nothing a reader can see. D2's table does the same job in one visible step |
| Drop the kind word altogether | "the Create button", "the tab Financial" read better with it, and it is what tells a tab from a part of the same name (B3) |
| A page object per section | A section is not a page: it has no address, and the form is saved as one |
| Name every control in a table beside the page object, not on its members | A second place to keep; the member is where a person looks |

## 8. Questions for the owner

| # | Question | Proposal |
| --- | --- | --- |
| Q1 | For a name that is on the page twice: "Sales price in Financial", "Sales price under Financial", or both? | Both: each reads well somewhere, and it costs nothing |
| Q2 | Should the existing specs be rewritten to the short wording at once, or as they are touched? | As they are touched, except B7.CON.02, which becomes the example |
| Q3 | Is the table step (D) wanted, or should a spec stay one field per line? | Wanted for journeys that write; one line per field stays for an example about one field |
| Q4 | This changes Bruinet's binder for every suite. Build it on the `move` branch as the rest, or as a proposal to Bruinet's own plan first? | On `move`, step 1 and 2 first, behind Bruinet's own binder tests |
