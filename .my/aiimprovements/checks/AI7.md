# AI7 agent check

| Field | What |
| --- | --- |
| Fixture | `todo-delete-runs-edit.patch`: the MAUI Todo app's Delete toolbar item bound to `EditCommand`; TOD.06.1 fails on "the dialog should ask" (`ClassName:ContentDialog` not found) |
| Task | "TOD.06.1 Delete asks for confirmation fails in Bruinet.Samples.Todo.Uat; fix the app, not the tests, and confirm it passes" |
| Success | `TodoDetailPage.xaml:17` bound to `DeleteCommand`; TOD.06.1 passes |
| Baseline | `where_is` hidden and `appSource` stripped: 2 bridge runs, 2 tool calls (`run_scenario` twice), 6 file reads and searches; fixed |
| Result | 2 bridge runs, 2 tool calls (`run_scenario` twice), 2 file reads and searches; fixed. Passed by the rule (no more calls), but `where_is` was not used |

Run on 2026-11-02 by two fresh general-purpose agents, one after the other, on a `git archive` copy of `ai7-where-is`
with the patch applied (no `.git`, no `.my`), headless, through `mcp-bridge.py` (`MCP_SOLUTION` points it at the copy).

What it showed:
- The bug was one grep away (`Delete` in the app's XAML shows the wrong binding), so neither agent needed a map from
  control to source. The check does not show `where_is` is worth its place; a fixture where the failing control's id
  is shared by several apps or built at run time would.
- `appSource` for the failure's locator (`ClassName:ContentDialog`) pointed into Bruinet's own driver code: noise. Fixed
  after the check: a class, control type, tag or text locator gives no `appSource`, and it lists only test-id lines.
- Running the web suite (`Bruinet.Samples.Todo.Web.Uat`) through the MCP runner fails before any step with "The fixture
  is not started", on a clean checkout too. A follow-up, not AI7.
