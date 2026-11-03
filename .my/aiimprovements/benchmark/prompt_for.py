"""
The prompt one benchmark run's agent gets (benchmark/plan.md, "Runs"): prompt.md filled with the task, the copy's folders,
and the bridge's variables for the setup. The same words for every setup; only the variables differ.

  python prompt_for.py <task folder> <copy folder> <setup> <run label>
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def win(path):
    return os.path.abspath(path).replace("\\", "/")


def main():
    task_folder, out, setup, label = sys.argv[1:5]
    with open(os.path.join(task_folder, "task.json"), encoding="utf-8") as file:
        task = json.load(file)
    with open(os.path.join(task_folder, "task.md"), encoding="utf-8") as file:
        text = file.read().strip()
    variables = {"MCP_SOLUTION": win(os.path.join(out, "repo")), "MCP_PROFILE": setup,
                 "MCP_LOG": win(os.path.join(out, "calls.jsonl")), "MCP_LABEL": label, **task.get("env", {})}
    env = task.get("env", {})
    with open(os.path.join(HERE, "prompt.md"), encoding="utf-8") as file:
        prompt = file.read()
    print(prompt.format(
        repo=win(os.path.join(out, "repo")),
        client=win(out),
        task=text,
        vars=" ".join(f'{name}="{value}"' for name, value in variables.items()),
        env_note=f"; set {', '.join(f'{name}={value}' for name, value in env.items())} for dotnet too" if env else "",
    ))


if __name__ == "__main__":
    main()
