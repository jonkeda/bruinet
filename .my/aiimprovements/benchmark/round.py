"""
The main round's bookkeeping (benchmark/plan.md, step 7). Runs are named M<repeat>-<task>-<setup> (M2-T5-all); their
copies are in TestResults/bench/<run>, made a batch ahead and removed once scored (a copy is up to 0.7 GB).

  python round.py order                         the 48 runs in order, four per batch
  python round.py prepare <run> ...             the copies and their prompt.txt
  python round.py score <run> <tokens> <tool uses> <ms>
                                                scored into results.jsonl (report.txt is a question's answer); the log
                                                and the report kept in TestResults/bench/kept/<run>; the copy removed
"""
import json
import os
import shutil
import subprocess
import sys

import make_copy

HERE = os.path.dirname(os.path.abspath(__file__))
BENCH = os.path.abspath(os.path.join(HERE, "..", "..", "..", "TestResults", "bench"))
TASKS = [f"T{number}" for number in range(1, 9)]


def order():
    """Three repeats; per repeat two tasks a batch, each with both setups; which setup goes first alternates."""
    runs = []
    for repeat in (1, 2, 3):
        for index, task in enumerate(TASKS):
            first, second = ("before", "all") if (index + repeat) % 2 == 0 else ("all", "before")
            runs += [f"M{repeat}-{task}-{first}", f"M{repeat}-{task}-{second}"]
    return runs


def parts(run):
    _, task, setup = run.split("-", 2)
    return task, setup


def prepare(run):
    task, setup = parts(run)
    out = os.path.join(BENCH, run)
    done = subprocess.run([sys.executable, os.path.join(HERE, "make_copy.py"), os.path.join(HERE, "tasks", task), out],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    if done.returncode != 0:
        raise SystemExit(f"{run}: {done.stdout[-800:]}\n{done.stderr[-800:]}")
    prompt = subprocess.run([sys.executable, os.path.join(HERE, "prompt_for.py"), os.path.join(HERE, "tasks", task), out, setup, run],
                            capture_output=True, text=True, encoding="utf-8", check=True).stdout
    with open(os.path.join(out, "prompt.txt"), "w", encoding="utf-8") as file:
        file.write(prompt)
    print(f"{run}: ready")


def score(run, tokens, tool_uses, ms):
    task, setup = parts(run)
    out = os.path.join(BENCH, run)
    report = os.path.join(out, "report.txt")
    command = [sys.executable, os.path.join(HERE, "score.py"), os.path.join(HERE, "tasks", task), out, os.path.join(out, "calls.jsonl"),
               "--setup", setup, "--run", run, "--tokens", tokens, "--tool-uses", tool_uses, "--seconds", str(round(int(ms) / 1000)),
               "--answer", report, "--results", os.path.join(HERE, "rerun-results.jsonl" if run.startswith("R") else "results.jsonl")]
    done = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if done.returncode != 0:
        raise SystemExit(f"{run}: scoring failed\n{done.stdout[-800:]}\n{done.stderr[-800:]}")
    result = json.loads(done.stdout)
    kept = os.path.join(BENCH, "kept", run)
    os.makedirs(kept, exist_ok=True)
    for name in ("calls.jsonl", "report.txt", "prompt.txt"):
        if os.path.exists(os.path.join(out, name)):
            shutil.copy(os.path.join(out, name), kept)
    make_copy.remove(out)
    print(f"{run}: solved={result['solved']} calls={result['calls']} {result['byTool']} tokens={result['tokens']} s={result['seconds']}"
          + ("" if result["solved"] else f" failing={result['scenariosFailing']} missing={result['answerMissing']} forbidden={result['forbiddenChanged']} fixed={result['fixedFile']}"))


def main():
    command, rest = sys.argv[1], sys.argv[2:]
    if command == "order":
        runs = order()
        for start in range(0, len(runs), 4):
            print(" ".join(runs[start:start + 4]))
    elif command == "prepare":
        for run in rest:
            prepare(run)
    elif command == "score":
        score(*rest)
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main()
