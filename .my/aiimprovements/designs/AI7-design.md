# AI7 design: from a UI element to its source line

Status: **built**, 2026-11-02, on branch `ai7-where-is` (stacked on `ai10-states`). The spike settled the plan's open
question: a static search for the locator's value replaces the plan's build step and its `data-bruinet-src` attribute.

## Outcome

An agent with a failing or unknown control learns in one call where the page object declares it and which app line
makes the element: `TodoDetailPage.cs:55` and `TodoDetailPage.razor:19`. It edits the right file instead of grepping the
solution.

## Tools

| Tool | Arguments | Result |
| --- | --- | --- |
| `where_is` | `page`, `control` (as `list_pages` names them), `suite?` | `{page, control, pageObject, locator, app[{file, text, testId}], note}` |
| `get_failure` (and `run_scenario`'s failures) | unchanged | adds `appSource`: the top 3 app lines for the failure's locator, `"file:line: text"` |

Example, `where_is {suite: "Bruinet.Samples.Todo.Web.Uat", page: "Todo Detail", control: "Delete"}`:

```json
{
  "page": "Todo Detail", "control": "Delete",
  "pageObject": "samples\\Todo\\tests\\Bruinet.Samples.Todo.Web.UITests\\Pages\\TodoDetailPage.cs:55",
  "locator": "TodoDetail_Delete",
  "app": [{ "file": "samples\\Todo\\src\\Bruinet.Samples.Todo.Web\\Components\\Pages\\TodoDetailPage.razor:19",
            "text": "<CommandButton Command=\"Model.DeleteCommand\" AutomationId=\"TodoDetail_Delete\">Delete</CommandButton>",
            "testId": true }]
}
```

`locator` ends in `…` for a value built at run time (`$"Row_{id}"`). `note` says why `app` is empty: no string literal
on the member (a constant, `nameof`), or no app source has the value (a stale page object).

## Code

| Project | Class | What |
| --- | --- | --- |
| `Bruinet.Bdd.Workspace` | `BddAppSources` (new) | `ValueOf(locator)`, `ValueAt(file, line)`, `Find(value, max, hint)`; skips `bin`, `obj`, dot-folders, other checkouts (own `.git`), links, and test projects (by name or a test framework reference) |
| `Bruinet.Bdd.Workspace` | `BddLocatorValue`, `BddAppSourceHit`, `BddAppSourceHint` (new records) | the value or its prefix; a hit; what ranks one app over another |
| `Bruinet.Uat` | `UatControlModel.MemberName` (new init property) | the page object member (`DeleteButton` for "Delete"), filled by `UatReflectionRuntime.PageModels()` |
| `Bruinet.Cli` | `BddMcpServer.WhereIs`, `AppSource` | the tool row, the switch entry, `appSource` in `get_failure` |

Search: a regex for the value quoted (or, for a prefix, followed by `{`, `@` or `" +`), in `.razor`, `.cshtml`, `.html`,
`.htm`, `.xaml`, `.axaml`, `.cs`. A CSS selector gives its attribute value (`[data-testid='x']`) or id (`#x`); a tag
name (`h1`) gives nothing. Rank: test-id attribute (`data-testid`, `AutomationId`, `x:Name`, `id`) first, then the
control's family (a `Bruinet.Maui*` control prefers `.xaml`, an Html or Blazor one web markup), then file name words
shared with the page object's type name, then project name words shared with its test project, then markup before code.

## Spike: where_is over every control of four suites

| Suite | Controls | Test id found | No literal | Not found |
| --- | --- | --- | --- | --- |
| Todo Web (`Bruinet.Samples.Todo.Web.Uat`) | 32 | 27, all in the web app | 5 (dialogs, lists) | 0 |
| Todo MAUI (`Bruinet.Samples.Todo.Uat`) | 60 | 53, 23 of 24 shared ids in the MAUI app | 5 | 0 (2 as plain strings) |
| `Bruinet.Maui.UITests` | 162 | 158 | 4 | 0 |
| `Bruinet.Blazor.UITests` | 32 | 29 | 0 | 3 (`home-title`, `counter-link`, `login-link`: stale page object) |

Before the member name, selectors and ranking: 22, 42, 69 and 0 found. A build step would add only the "no literal"
rows, which are containers and collections whose locator is a constant.

## Tests

| Test | Project | Proves |
| --- | --- | --- |
| `BddAppSourcesTests.ValueOf_*`, `ValueAt_*` | `Bruinet.Bdd.Workspace.Tests` | locator text and page object source give the test id |
| `BddAppSourcesTests.Find_*` | same | test ids first, tests and `obj` left out, prefixes, the page object's own app first |
| `McpWhereIsTests.WhereIs_AWebControl_*` | `Bruinet.Cli.Tests` | Done when: the web suite's Delete gives `TodoDetailPage.cs` and `TodoDetailPage.razor` |
| `McpWhereIsTests.WhereIs_TheSameControlInTheMauiSuite_*` | same | the MAUI suite's gives `TodoDetailPage.xaml` |
| `McpWhereIsTests.WhereIs_AnUnknownControl_*` | same | the error names `list_pages` |

## Open questions

- WinForms' designer file: left out, as the plan's line on other stacks says; `x:Name` and `AutomationId` cover XAML.
  Proposal: add when a WinForms suite exists.
