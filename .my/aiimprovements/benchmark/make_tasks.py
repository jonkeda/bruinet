"""
Writes the benchmark's seeded tasks T2-T8 (benchmark/plan.md, step 5): each task's edits are made in this checkout,
seed.patch is the diff of all of them, fix.patch the reverse of the bug's own edits (the known fix, for checking), and
the checkout is restored. task.json, task.md and expect.json are written beside them. T1 was made by hand (step 3).

  python make_tasks.py [T2 T5 ...]   (the ones named, all when none; a rewritten task.json loses its recorded failures)
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
BASE = "a5203d47"
TODO = "samples/Todo"
# The head goes to the agent's own dotnet test too, not only to the tool server's runs (the pilot ran the Windows app).
MAUI = {"project": f"{TODO}/tests/Bruinet.Samples.Todo.Uat", "suite": "Bruinet.Samples.Todo.Uat", "env": {"BRUINET_HEAD": "headless"}}
WEB = {"project": f"{TODO}/tests/Bruinet.Samples.Todo.Web.Uat", "suite": "Bruinet.Samples.Todo.Web.Uat", "env": {"BRUINET_HEAD": "bunit"}}
FEATURES = f"{TODO}/tests/Bruinet.Samples.Todo.Uat/Features"
APP_ONLY = ["samples/Todo/tests/**", "src/**", "tools/**", "tests/**"]

TASKS = [
    {
        "id": "T2", "kind": "locator after a UI change", "heldOut": False, **WEB, "green": False,
        "feature": f"{FEATURES}/TOD.03-read-a-todo.feature.md",
        "prompt": "In the Todo web app's acceptance suite (suite Bruinet.Samples.Todo.Web.Uat, run with BRUINET_HEAD=bunit), the "
                  "scenario \"TOD.03.1 Tapping a row opens its detail with all its fields\" in "
                  f"{FEATURES}/TOD.03-read-a-todo.feature.md fails since the detail page's markup was tidied. Fix the app (not "
                  "the tests, feature files or page objects) so it passes, and confirm all TOD.03 scenarios pass in that suite.",
        "bug": [(f"{TODO}/src/Bruinet.Samples.Todo.Web/Components/Pages/TodoDetailPage.razor",
                 '<p data-testid="TodoDetail_Title" class="detail-title">', '<p data-testid="TodoDetail_Heading" class="detail-title">')],
        "noise": [],
        "pass": ["TOD.03.1 Tapping a row opens its detail with all its fields"],
        "changeAnyOf": [f"{TODO}/src/Bruinet.Samples.Todo.Web/Components/Pages/TodoDetailPage.razor"],
        "mustNotChange": APP_ONLY,
    },
    {
        "id": "T3", "kind": "backend answer", "heldOut": False, **MAUI, "green": False,
        "feature": f"{FEATURES}/TOD.03-read-a-todo.feature.md",
        "prompt": "In the Todo MAUI app's acceptance suite (suite Bruinet.Samples.Todo.Uat), the scenario \"TOD.03.1 Tapping a "
                  f"row opens its detail with all its fields\" in {FEATURES}/TOD.03-read-a-todo.feature.md fails. Fix the app "
                  "(not the tests, feature files or page objects) so it passes, and confirm all TOD.03 scenarios pass.",
        "bug": [(f"{TODO}/src/Bruinet.Samples.Todo.Infrastructure/Api/TodoApiClient.cs",
                 "        Title = dto.Title,\n        Notes = dto.Notes,\n        DueDate = dto.DueDate,",
                 "        Title = dto.Title,\n        DueDate = dto.DueDate,")],
        "noise": [],
        "pass": ["TOD.03.1 Tapping a row opens its detail with all its fields"],
        "changeAnyOf": [f"{TODO}/src/Bruinet.Samples.Todo.Infrastructure/Api/TodoApiClient.cs"],
        "mustNotChange": APP_ONLY,
    },
    {
        "id": "T4", "kind": "look at a screen", "heldOut": False, **MAUI, "green": False,
        "feature": f"{FEATURES}/TOD.01-see-my-todos.feature.md",
        "prompt": "Find out, from the running Todo MAUI app (suite Bruinet.Samples.Todo.Uat), what its Todo list screen shows "
                  "when the app holds the three-todos test data: the titles of the todos, the Last Sync label, and the filter. "
                  "Change no files. End your answer with a line \"ANSWER:\" followed by the titles, the label and the filter.",
        "bug": [], "noise": [],
        "answer": ["Buy milk", "Book dentist", "File taxes", "Synced 10:00", "All"],
        "mustNotChange": ["**"],
    },
    {
        "id": "T5", "kind": "timing", "heldOut": True, **WEB, "green": False,
        "feature": f"{FEATURES}/TOD.08-things-go-wrong.feature.md",
        "prompt": "In the Todo web app's acceptance suite (suite Bruinet.Samples.Todo.Web.Uat, run with BRUINET_HEAD=bunit), the "
                  f"scenario \"TOD.08.3 A slow server shows Loading until the answer arrives\" in {FEATURES}/TOD.08-things-go-wrong.feature.md "
                  "fails. Fix the app (not the tests, feature files or page objects) so it passes, and confirm all TOD.08 scenarios "
                  "pass in that suite.",
        # The stub's delay does not run on the real clock, so an HTTP timeout never fires: the list shows Loading late instead.
        "bug": [(f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoListViewModel.cs",
                 "        _isLoading = true;\n        UpdateState();\n\n        try\n        {\n            var result = await _sync.SyncAsync();",
                 "        _isLoading = true;\n\n        try\n        {\n            // Loading only for a sync that takes long: a quick one would flicker.\n"
                 "            var slow = Task.Delay(TimeSpan.FromSeconds(3));\n            var syncing = _sync.SyncAsync();\n"
                 "            if (await Task.WhenAny(syncing, slow) == slow)\n            {\n                UpdateState();\n            }\n\n"
                 "            var result = await syncing;")],
        "noise": [],
        "pass": ["TOD.08.3 A slow server shows Loading until the answer arrives"],
        "changeAnyOf": [f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoListViewModel.cs"],
        "mustNotChange": APP_ONLY,
    },
    {
        "id": "T6", "kind": "exception", "heldOut": True, **MAUI, "green": False,
        "feature": f"{FEATURES}/TOD.02-create-a-todo.feature.md",
        "prompt": "In the Todo MAUI app's acceptance suite (suite Bruinet.Samples.Todo.Uat), the scenario \"TOD.02.2 Saving a "
                  f"valid title returns to the list, which shows the new todo\" in {FEATURES}/TOD.02-create-a-todo.feature.md "
                  "fails. Fix the app (not the tests, feature files or page objects) so it passes, and confirm all TOD.02 "
                  "scenarios pass.",
        "bug": [(f"{TODO}/src/Bruinet.Samples.Todo.Core/Services/TodoService.cs",
                 "        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();",
                 "        => notes is null ? null : char.ToUpperInvariant(notes.Trim()[0]) + notes.Trim()[1..];")],
        "noise": [],
        "pass": ["TOD.02.2 Saving a valid title returns to the list, which shows the new todo"],
        "changeAnyOf": [f"{TODO}/src/Bruinet.Samples.Todo.Core/Services/TodoService.cs"],
        "mustNotChange": APP_ONLY,
    },
    {
        "id": "T7", "kind": "regression in a bigger change", "heldOut": True, **MAUI, "green": True,
        "feature": f"{FEATURES}/TOD.01-see-my-todos.feature.md",
        "prompt": "In the Todo MAUI app's acceptance suite (suite Bruinet.Samples.Todo.Uat), the scenario \"TOD.01.4 Filtering "
                  f"by status shows only the matching rows\" in {FEATURES}/TOD.01-see-my-todos.feature.md passed earlier today and "
                  "fails now, after a tidy-up of the app's code. Fix the app (not the tests, feature files or page objects) so it "
                  "passes, and confirm all TOD.01 scenarios pass.",
        "bug": [(f"{TODO}/src/Bruinet.Samples.Todo.Core/Rules/TodoListRules.cs",
                 "        TodoFilter.Done => item.Status == TodoStatus.Done,",
                 "        TodoFilter.Done => item.Status >= TodoStatus.InProgress,")],
        # Renames of locals, every use: real code changes that change nothing.
        "noise": [
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoDetailViewModel.cs", "var confirmed = ", "var sure = "),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoDetailViewModel.cs", "(!confirmed)", "(!sure)"),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/Services/TodoService.cs", "var now = time.GetUtcNow();", "var stamp = time.GetUtcNow();"),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/Services/TodoService.cs", "At = now,", "At = stamp,"),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoListViewModel.cs", "var visible = ", "var shown = "),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoListViewModel.cs", "in visible)", "in shown)"),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoEditViewModel.cs", "DateOnly? due = ", "DateOnly? dueOn = "),
            (f"{TODO}/src/Bruinet.Samples.Todo.Core/ViewModels/TodoEditViewModel.cs", "Notes, due, Status", "Notes, dueOn, Status"),
        ],
        "pass": ["TOD.01.4 Filtering by status shows only the matching rows"],
        "changeAnyOf": [f"{TODO}/src/Bruinet.Samples.Todo.Core/Rules/TodoListRules.cs"],
        "mustNotChange": APP_ONLY,
    },
    {
        "id": "T8", "kind": "write a scenario", "heldOut": True, **WEB, "green": False,
        "feature": f"{FEATURES}/TOD.01-see-my-todos.feature.md",
        "prompt": f"Add a scenario to {FEATURES}/TOD.01-see-my-todos.feature.md, named exactly \"TOD.01.7 Filtering by In "
                  "progress shows only the rows in progress\", tagged @maui @web @list: with three todos on the server (one Open, "
                  "one InProgress, one Done), after a sync and choosing \"In progress\" in the filter, the list shows only the one "
                  "in progress. Use only phrases the suite already has. It must pass in the Todo web suite (suite "
                  "Bruinet.Samples.Todo.Web.Uat, run with BRUINET_HEAD=bunit). Change no C# or app files.",
        "bug": [], "noise": [],
        "pass": ["TOD.01.7 Filtering by In progress shows only the rows in progress"],
        "changeAnyOf": [f"{FEATURES}/TOD.01-see-my-todos.feature.md"],
        "mustNotChange": ["samples/Todo/src/**", "src/**", "tools/**", "**/*.cs"],
        "fixText": """
@TOD.01.7 @maui @web @list
## Scenario: TOD.01.7 Filtering by In progress shows only the rows in progress

```gherkin
Given I am on the Todo List page
And the server also has todos
  | Title         | Status     |
  | ------------- | ---------- |
  | Mow the lawn  | Open       |
  | Sand the door | InProgress |
  | Return books  | Done       |
When I tap Sync
Then Todos should have todo "Sand the door"
When I select "In progress" from Filter
Then Todos should have todo "Sand the door"
And Todos should not have todo "Mow the lawn"
And Todos should not have todo "Return books"
```
""",
    },
]


def git(*arguments):
    return subprocess.run(["git", *arguments], cwd=REPO, capture_output=True, text=True, encoding="utf-8", check=True).stdout


def edit(path, old, new, count=1):
    full = os.path.join(REPO, path)
    with open(full, encoding="utf-8", newline="") as file:
        text = file.read()
    old_n, new_n = old.replace("\n", "\r\n") if "\r\n" in text else old, new.replace("\n", "\r\n") if "\r\n" in text else new
    if old_n not in text:
        raise SystemExit(f"{path}: not found: {old[:60]}")
    with open(full, "w", encoding="utf-8", newline="") as file:
        file.write(text.replace(old_n, new_n) if count == 0 else text.replace(old_n, new_n, count))


def main():
    if git("status", "--porcelain", "--", "samples/").strip():
        raise SystemExit("samples/ has changes: commit or stash them first.")
    only = set(sys.argv[1:])
    for task in [task for task in TASKS if not only or task["id"] in only]:
        folder = os.path.join(HERE, "tasks", task["id"])
        os.makedirs(folder, exist_ok=True)
        try:
            for path, old, new in task["bug"]:
                edit(path, old, new)
            bug_files = sorted({path for path, _, _ in task["bug"]})
            fix = git("diff", "-R", "--", *bug_files) if bug_files else ""
            for path, old, new in task["noise"]:
                edit(path, old, new, count=0)
            seed = git("diff", "--", "samples/")
        finally:
            git("checkout", "--", "samples/")
        if task.get("fixText"):
            # The known fix of a task that writes something: the scenario appended to the feature.
            feature = os.path.join(REPO, task["feature"])
            with open(feature, "a", encoding="utf-8", newline="") as file:
                file.write(task["fixText"].replace("\n", "\r\n") if "\r\n" in open(feature, encoding="utf-8", newline="").read() else task["fixText"])
            fix = git("diff", "--", task["feature"])
            git("checkout", "--", task["feature"])
        for name, text in (("seed.patch", seed), ("fix.patch", fix)):
            path = os.path.join(folder, name)
            if text:
                with open(path, "w", encoding="utf-8", newline="") as file:
                    file.write(text)
            elif os.path.exists(path):
                os.remove(path)
        meta = {key: task[key] for key in ("id", "kind", "heldOut", "project", "suite", "env", "feature", "green")}
        meta["base"] = BASE
        meta["fails"], meta["passes"] = task.get("fails", []), task.get("passes", [])
        with open(os.path.join(folder, "task.json"), "w", encoding="utf-8") as file:
            json.dump(meta, file, indent=2)
            file.write("\n")
        with open(os.path.join(folder, "task.md"), "w", encoding="utf-8") as file:
            file.write(task["prompt"] + "\n")
        expect = {"mustNotChange": task["mustNotChange"]}
        if "pass" in task:
            expect["pass"] = {"feature": task["feature"], "suite": task["suite"], "scenarios": task["pass"]}
        if "changeAnyOf" in task:
            expect["changeAnyOf"] = task["changeAnyOf"]
        if "answer" in task:
            expect["answer"] = task["answer"]
        with open(os.path.join(folder, "expect.json"), "w", encoding="utf-8") as file:
            json.dump(expect, file, indent=2)
            file.write("\n")
        print(task["id"], "seed" if seed else "no seed", "fix" if fix else "no fix")


if __name__ == "__main__":
    main()
