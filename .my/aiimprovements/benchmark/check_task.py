"""
Checks a benchmark task (benchmark/plan.md, step 5): its seeded copy fails (the failures are written into task.json), it
scores unsolved as it is, and solved with its known fix (fix.patch, or answer.txt for a question).

  python check_task.py <task folder> <scratch folder>
"""
import json
import os
import subprocess
import sys

import make_copy

HERE = os.path.dirname(os.path.abspath(__file__))


def score(task, out, label, answer=None):
    results = os.path.join(out, "check-results.jsonl")
    command = [sys.executable, os.path.join(HERE, "score.py"), task, out, os.path.join(out, "no-log.jsonl"),
               "--setup", "check", "--run", label, "--results", results]
    if answer:
        command += ["--answer", answer]
    subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace", check=True)
    with open(results, encoding="utf-8") as file:
        return json.loads(file.readlines()[-1])


def main():
    task, scratch = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    name = os.path.basename(task)
    out = os.path.join(scratch, f"check-{name}")
    has_seed = os.path.exists(os.path.join(task, "seed.patch"))
    done = subprocess.run([sys.executable, os.path.join(HERE, "make_copy.py"), task, out] + (["--record"] if has_seed else []),
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(done.stdout.rstrip())
    if done.returncode != 0:
        raise SystemExit(f"{name}: make_copy failed\n{done.stderr[-1500:]}")

    question = os.path.join(task, "answer.txt")
    unfixed = score(task, out, "unfixed", answer=os.path.join(out, "empty-answer.txt"))
    fix = os.path.join(task, "fix.patch")
    if os.path.exists(fix):
        make_copy.run(make_copy.GIT + ["apply", "--whitespace=nowarn", fix], os.path.join(out, "repo"))
    fixed = score(task, out, "fixed", answer=question if os.path.exists(question) else None)
    # A seeded task must fail somewhere: one that only scores unsolved for want of a change would test nothing.
    with open(os.path.join(task, "task.json"), encoding="utf-8") as file:
        recorded = json.load(file)
    good = not unfixed["solved"] and fixed["solved"] and (not has_seed or bool(recorded.get("fails")))
    print(f"{name}: unfixed solved={unfixed['solved']} (failing {unfixed['scenariosFailing']}, missing {unfixed['answerMissing']}); "
          f"fixed solved={fixed['solved']} (failing {fixed['scenariosFailing']}, changed {fixed['changed']}, forbidden {fixed['forbiddenChanged']}, missing {fixed['answerMissing']})")
    print(f"{name}: {'OK' if good else 'BROKEN'}")
    if not good:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
