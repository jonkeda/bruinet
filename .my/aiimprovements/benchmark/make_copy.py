"""
Makes a benchmark task's copy (benchmark/plan.md, step 3).

  python make_copy.py <task folder> <out folder> [--check]

<out>/repo is a git archive of the task's base commit without .my, .claude or the skills, committed once on its own (the
scorer and `why` diff against it; the agent may not use git). The suite's project is built; when the task asks for a
green run, the feature runs green on the copy first, through the bridge, so its traces are the last green run's. Then
the seed patch is applied, uncommitted, and the bridge is copied to <out>/mcp.py.

--check runs the feature once more on the seeded copy and says whether it fails as task.json says. Use it on a copy made
for checking only: its failing run would be the agent's first.
"""
import io
import json
import os
import shutil
import subprocess
import sys
import tarfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
BRIDGE = os.path.join(HERE, "..", "checks", "mcp-bridge.py")
LEFT_OUT = [".my", ".claude", os.path.join(".github", "skills")]
GIT = ["git", "-c", "core.longpaths=true", "-c", "user.name=Owner", "-c", "user.email=owner@example.test"]


def long(path):
    """A Windows path that may pass 260 characters."""
    full = os.path.abspath(path)
    return "\\\\?\\" + full if os.name == "nt" and not full.startswith("\\\\?\\") else full


def run(command, cwd, check=True):
    done = subprocess.run(command, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and done.returncode != 0:
        raise SystemExit(f"{' '.join(command)} failed in {cwd}:\n{done.stdout[-2000:]}\n{done.stderr[-2000:]}")
    return done


def bridge_run(out, task, build):
    """The feature run once through the bridge (all tools); each scenario's name and status, and its failed step's line."""
    calls = os.path.join(out, "make-copy-run.json")
    feature = os.path.join(out, "repo", task["feature"]).replace("\\", "/")
    with open(calls, "w", encoding="utf-8") as file:
        json.dump([{"tool": "run_scenario", "arguments": {"path": feature, "build": build}}], file)
    env = dict(os.environ, MCP_SOLUTION=os.path.join(out, "repo"), MCP_PROFILE="all")
    env.pop("MCP_LOG", None)
    done = subprocess.run([sys.executable, os.path.join(out, "mcp.py"), "calls", calls], capture_output=True, text=True,
                          encoding="utf-8", errors="replace", env=env)
    os.remove(calls)
    text = done.stdout.split("\n", 1)[1] if done.stdout.startswith("==") else done.stdout
    try:
        answer = json.loads(text)
    except ValueError:
        raise SystemExit(f"The run gave no answer:\n{done.stdout[-2000:]}\n{done.stderr[-2000:]}")
    return {item["scenario"]: (item["status"], (item.get("failedStep") or {}).get("line")) for item in answer.get("scenarios", [])}


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    task_folder, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    check = "--check" in sys.argv[3:]
    with open(os.path.join(task_folder, "task.json"), encoding="utf-8") as file:
        task = json.load(file)

    if os.path.exists(out):
        shutil.rmtree(long(out))
    repo = os.path.join(out, "repo")
    os.makedirs(repo)
    archive = subprocess.run(["git", "archive", "--format=tar", task["base"]], cwd=REPO, capture_output=True, check=True).stdout
    # Member by member: the long-path form takes back slashes only, which extractall does not give.
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        for member in tar:
            target = long(os.path.normpath(os.path.join(repo, member.name)))
            if member.isdir():
                os.makedirs(target, exist_ok=True)
            elif member.isfile():
                os.makedirs(os.path.dirname(target), exist_ok=True)
                with tar.extractfile(member) as source, open(target, "wb") as copy:
                    shutil.copyfileobj(source, copy)
    for gone in LEFT_OUT:
        if os.path.exists(os.path.join(repo, gone)):
            shutil.rmtree(long(os.path.join(repo, gone)))
    run(GIT + ["init", "-q"], repo)
    run(GIT + ["config", "core.longpaths", "true"], repo)
    run(GIT + ["add", "-A"], repo)
    run(GIT + ["commit", "-q", "-m", "The app"], repo)
    shutil.copy(BRIDGE, os.path.join(out, "mcp.py"))
    print(f"copy of {task['base']} committed in {repo}")

    run(["dotnet", "build", task["project"], "-v:quiet", "-nologo"], repo)
    print(f"built {task['project']}")

    if task.get("green"):
        green = bridge_run(out, task, build=False)
        if not green or any(status != "passed" for status, _ in green.values()):
            raise SystemExit(f"The green run did not pass: {green}")
        print(f"green run: {len(green)} scenarios passed")

    run(GIT + ["apply", "--whitespace=nowarn", os.path.join(task_folder, "seed.patch")], repo)
    print(f"seeded: {run(GIT + ['status', '--short'], repo).stdout.strip().count(chr(10)) + 1} files changed")

    if check:
        seeded = bridge_run(out, task, build=True)
        wanted = {item["scenario"]: item["line"] for item in task["fails"]}
        problems = [f"{name}: {seeded.get(name)}, wanted failed at line {line}" for name, line in wanted.items() if seeded.get(name) != ("failed", line)]
        problems += [f"{name}: {seeded.get(name)}, wanted passed" for name in task.get("passes", []) if (seeded.get(name) or ("",))[0] != "passed"]
        print("check: fails as task.json says" if not problems else "check FAILED:\n  " + "\n  ".join(problems))
        if problems:
            raise SystemExit(1)


if __name__ == "__main__":
    main()
