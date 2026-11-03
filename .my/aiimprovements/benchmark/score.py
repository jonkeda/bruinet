"""
Scores one benchmark run (benchmark/plan.md, step 4).

  python score.py <task folder> <copy folder> <call log> --setup <all|before|...> --run <label>
                  [--tokens N] [--tool-uses N] [--seconds N] [--answer <file>] [--results <file>]

Solved when the expected scenarios pass on a fresh build of the copy, a file of changeAnyOf changed since the seed, and
nothing under mustNotChange did (expect.json); each part only when expect.json has it. A question task (answer) is
solved when every expected text is in the agent's answer (--answer, its final message), case aside. The changes are the copy's working tree against refs/bench/seeded (the
seeded tree make_copy.py kept), new files included. The tool calls come from the bridge's log (MCP_LOG), not from the
agent; tokens, the agent's own tool uses and seconds come from the run's notification, passed in.

Prints the result and appends it as one JSON line to the results file (benchmark/results.jsonl by default).
"""
import argparse
import collections
import datetime
import fnmatch
import json
import os
import subprocess

import make_copy

HERE = os.path.dirname(os.path.abspath(__file__))


def changed_since_seed(repo):
    """The files the agent changed: against the seeded tree, and new ones git does not ignore."""
    diff = make_copy.run(make_copy.GIT + ["diff", "--name-only", "refs/bench/seeded"], repo).stdout
    new = make_copy.run(make_copy.GIT + ["ls-files", "--others", "--exclude-standard"], repo).stdout
    return sorted({line.strip() for line in (diff + new).splitlines() if line.strip()})


def calls_of(log):
    """The run's tool calls from the bridge's log: how many, which, the errors and the milliseconds; the listing apart."""
    lines = []
    if os.path.exists(log):
        with open(log, encoding="utf-8") as file:
            lines = [json.loads(line) for line in file if line.strip()]
    calls = [line for line in lines if line["tool"] != "(tools)"]
    return {
        "listings": len(lines) - len(calls),
        "calls": len(calls),
        "byTool": dict(collections.Counter(line["tool"] for line in calls).most_common()),
        "errors": sum(1 for line in calls if line["error"]),
        "hiddenAsked": sum(1 for line in calls if line["hidden"]),
        "ms": sum(line["ms"] for line in calls),
        "answerBytes": sum(line["answerBytes"] for line in calls),
        "bridges": len({line["bridge"] for line in lines}),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("task")
    parser.add_argument("copy")
    parser.add_argument("log")
    parser.add_argument("--setup", required=True)
    parser.add_argument("--run", required=True)
    parser.add_argument("--tokens", type=int)
    parser.add_argument("--tool-uses", type=int)
    parser.add_argument("--seconds", type=int)
    parser.add_argument("--answer")
    parser.add_argument("--results", default=os.path.join(HERE, "results.jsonl"))
    args = parser.parse_args()

    task_folder, out = os.path.abspath(args.task), os.path.abspath(args.copy)
    repo = os.path.join(out, "repo")
    with open(os.path.join(task_folder, "task.json"), encoding="utf-8") as file:
        task = json.load(file)
    with open(os.path.join(task_folder, "expect.json"), encoding="utf-8") as file:
        expect = json.load(file)

    changed = changed_since_seed(repo)
    forbidden = [path for path in changed if any(fnmatch.fnmatch(path, pattern) for pattern in expect.get("mustNotChange", []))]
    fixed = [path for path in changed if path in expect.get("changeAnyOf", [])]
    fixed_ok = bool(fixed) or "changeAnyOf" not in expect

    # The scenarios on a fresh build, through the bridge, as the agent's own runs went.
    failing = []
    if "pass" in expect:
        ran = make_copy.bridge_run(out, {"feature": expect["pass"]["feature"], "suite": expect["pass"].get("suite"), "env": task.get("env", {})}, build=True)
        failing = [name for name in expect["pass"]["scenarios"] if (ran.get(name) or ("missing",))[0] != "passed"]

    # A question's answer: every expected text in the agent's final message.
    missing = []
    if "answer" in expect:
        answer = open(args.answer, encoding="utf-8").read().lower() if args.answer and os.path.exists(args.answer) else ""
        missing = [text for text in expect["answer"] if text.lower() not in answer]

    result = {
        "at": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
        "task": task["id"],
        "heldOut": task.get("heldOut", False),
        "setup": args.setup,
        "run": args.run,
        "solved": not failing and fixed_ok and not forbidden and not missing,
        "scenariosFailing": failing,
        "fixedFile": fixed_ok,
        "answerMissing": missing,
        "forbiddenChanged": forbidden,
        "changed": changed,
        **calls_of(args.log),
        "tokens": args.tokens,
        "agentToolUses": args.tool_uses,
        "seconds": args.seconds,
    }
    with open(args.results, "a", encoding="utf-8") as file:
        file.write(json.dumps(result) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
