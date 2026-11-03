"""
A minimal MCP client for the bruinet server, over stdio: one server for the whole script, so a Step-through lives
across calls.

  python mcp.py tools                      -> the server's instructions and its tools (name, description, arguments)
  python mcp.py calls <file.json>          -> runs [{"tool": "...", "arguments": {...}}, ...] in order on one server,
                                              printing each answer

MCP_LOG=<file> appends one JSON line per call (and one per tools listing), for the benchmark (benchmark/plan.md, step 1):
  {"at", "bridge", "label", "tool", "argumentBytes", "ms", "error", "hidden", "answerBytes"}
"bridge" is one id per invocation of this script; "label" is MCP_LABEL (a run's name), when set.

MCP_PROFILE picks a setup (benchmark/plan.md, step 2): "all" (the default) changes nothing; "before" is the server as
before AI1: every AI item's tools hidden, its fields stripped from answers, its arguments dropped, and the instructions'
sentences about them cut; "without-<item>" (without-AI9) is "all" but that one item. MCP_HIDE hides more tools; an
item one of whose tools it hides goes whole.
"""
import datetime
import json
import os
import re
import subprocess
import sys
import time
import uuid

SOLUTION = os.environ.get("MCP_SOLUTION", r"E:\repos\Private\tomorrow")
SERVER = os.path.join(r"E:\repos\Private\tomorrow", ".bruinet", "mcp", "bruinet.dll")
# What each item added: its tools, the fields it put in other tools' answers, the arguments it added to them, and the
# phrases of the instructions about those fields (a clause naming a hidden tool goes whole).
ITEMS = {
    "AI1": {"tools": ["run_scenario", "last_runs"]},
    "AI2": {"tools": ["get_failure", "failing"], "fields": ["failure"],
            "phrases": [": a failed one comes with its failure (kind, expected and actual, locator, binding, the code frames to look in)"]},
    "AI2b": {"tools": ["coverage"]},
    "AI3": {"tools": ["tests_affected"]},
    "AI4": {"tools": ["step_start", "step_move", "step_screenshot", "step_state"]},
    "AI5": {"tools": ["rename_step", "rename_page_object"]},
    "AI6": {"tools": ["step_log"], "fields": ["appEvents"]},
    "AI7": {"tools": ["where_is"], "fields": ["appSource"]},
    "AI8": {"tools": ["get_trace"], "arguments": ["trace"]},
    "AI9": {"tools": ["why"], "fields": ["sinceGreen"],
            "phrases": [" and, for a scenario that passed before, sinceGreen: where it parted from that run and the code changed since"]},
    "AI10": {"tools": ["list_states", "set_state"]},
    "AI11": {"tools": ["http_calls"], "arguments": ["replay"]},
}


def hidden_items():
    profile = os.environ.get("MCP_PROFILE", "all")
    if profile == "all":
        items = set()
    elif profile == "before":
        items = set(ITEMS)
    elif profile.startswith("without-") and profile[len("without-"):] in ITEMS:
        items = {profile[len("without-"):]}
    else:
        raise SystemExit(f"MCP_PROFILE '{profile}': all, before, or without-<item> ({', '.join(ITEMS)}).")
    asked = {name for name in os.environ.get("MCP_HIDE", "").split(",") if name}
    return items | {item for item, added in ITEMS.items() if asked & set(added["tools"])}, asked


GONE, ASKED = hidden_items()
HIDDEN = ASKED | {tool for item in GONE for tool in ITEMS[item]["tools"]}
STRIPPED = {field for item in GONE for field in ITEMS[item].get("fields", [])}
DROPPED = {argument for item in GONE for argument in ITEMS[item].get("arguments", [])}
PHRASES = [phrase for item in GONE for phrase in ITEMS[item].get("phrases", [])]
LOG = os.environ.get("MCP_LOG")
LABEL = os.environ.get("MCP_LABEL")
BRIDGE = uuid.uuid4().hex[:12]


def log(tool, arguments, started, error=False, hidden=False, answer=""):
    """One line of the call log, when MCP_LOG names a file; never fails the call."""
    if not LOG:
        return
    line = {
        "at": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="milliseconds"),
        "bridge": BRIDGE,
        "label": LABEL,
        "tool": tool,
        "argumentBytes": len(json.dumps(arguments, ensure_ascii=False).encode("utf-8")),
        "ms": round((time.perf_counter() - started) * 1000),
        "error": error,
        "hidden": hidden,
        "answerBytes": len(answer.encode("utf-8")),
    }
    try:
        with open(LOG, "a", encoding="utf-8") as file:
            file.write(json.dumps(line) + "\n")
    except OSError:
        pass


def start():
    env = dict(os.environ)
    env.setdefault("BRUINET_HEAD", "headless")
    process = subprocess.Popen(
        ["dotnet", "exec", SERVER, "mcp", "--solution", SOLUTION],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
        text=True, encoding="utf-8", env=env, bufsize=1)
    return process


def request(process, ident, method, params):
    process.stdin.write(json.dumps({"jsonrpc": "2.0", "id": ident, "method": method, "params": params}) + "\n")
    process.stdin.flush()
    while True:
        line = process.stdout.readline()
        if not line:
            raise RuntimeError("the server closed")
        message = json.loads(line)
        if message.get("id") == ident:
            return message


def without_keys(text, hidden):
    """The answer without some keys, wherever they are: as a server without the items that added them."""
    if not hidden:
        return text
    try:
        node = json.loads(text)
    except ValueError:
        return text

    def strip(value):
        if isinstance(value, dict):
            return {key: strip(item) for key, item in value.items() if key not in hidden}
        if isinstance(value, list):
            return [strip(item) for item in value]
        return value

    return json.dumps(strip(node), indent=2)


def instructions_without(text):
    """The instructions without the hidden fields' phrases and the clauses that name a hidden tool."""
    for phrase in PHRASES:
        text = text.replace(phrase, "")
    if not HIDDEN:
        return text
    kept = []
    for sentence in text.split(". "):
        clauses = [clause for clause in sentence.split("; ") if not any(re.search(rf"\b{re.escape(tool)}\b", clause) for tool in HIDDEN)]
        if clauses:
            kept.append("; ".join(clauses))
    return ". ".join(kept)


def main():
    # Answers carry any text (a screen's element tree has symbols): the Windows console's code page cannot print them all.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    process = start()
    try:
        init = request(process, 1, "initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "check", "version": "1"}})
        if sys.argv[1] == "tools":
            started = time.perf_counter()
            instructions = init["result"].get("instructions", "")
            instructions = instructions_without(instructions)
            print("INSTRUCTIONS:", instructions)
            tools = request(process, 2, "tools/list", {})["result"]["tools"]
            listing = [f"INSTRUCTIONS: {instructions}"]
            for tool in [tool for tool in tools if tool["name"] not in HIDDEN]:
                arguments = ", ".join(name for name in tool.get("inputSchema", {}).get("properties", {}).keys() if name not in DROPPED)
                listing.append(f"\n- {tool['name']}({arguments}): {tool['description']}")
            print("\n".join(listing[1:]))
            log("(tools)", {}, started, answer="\n".join(listing))
            return

        calls = json.load(open(sys.argv[2], encoding="utf-8"))
        for index, call in enumerate(calls, start=3):
            started = time.perf_counter()
            if call["tool"] in HIDDEN:
                print(f"== {call['tool']} (error)\nUnknown tool.\n")
                log(call["tool"], call.get("arguments", {}), started, error=True, hidden=True, answer="Unknown tool.")
                continue
            arguments = {name: value for name, value in call.get("arguments", {}).items() if name not in DROPPED}
            answer = request(process, index, "tools/call", {"name": call["tool"], "arguments": arguments})
            result = answer.get("result", {})
            text = "".join(part.get("text", "") for part in result.get("content", []))
            text = without_keys(text, STRIPPED)
            flag = " (error)" if result.get("isError") else ""
            print(f"== {call['tool']}{flag}\n{text}\n")
            log(call["tool"], call.get("arguments", {}), started, error=bool(result.get("isError")), answer=text)
    finally:
        process.stdin.close()
        process.terminate()


if __name__ == "__main__":
    main()
