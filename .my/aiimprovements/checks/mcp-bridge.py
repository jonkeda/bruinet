"""
A minimal MCP client for the bruinet server, over stdio: one server for the whole script, so a Step-through lives
across calls.

  python mcp.py tools                      -> the server's instructions and its tools (name, description, arguments)
  python mcp.py calls <file.json>          -> runs [{"tool": "...", "arguments": {...}}, ...] in order on one server,
                                              printing each answer
"""
import json
import os
import subprocess
import sys

SOLUTION = os.environ.get("MCP_SOLUTION", r"E:\repos\Private\tomorrow")
SERVER = os.path.join(r"E:\repos\Private\tomorrow", ".bruinet", "mcp", "bruinet.dll")
HIDDEN = {name for name in os.environ.get("MCP_HIDE", "").split(",") if name}


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


def without_app_source(text):
    """The answer as a server without AI7 gives it: no appSource."""
    try:
        node = json.loads(text)
    except ValueError:
        return text

    def strip(value):
        if isinstance(value, dict):
            return {key: strip(item) for key, item in value.items() if key != "appSource"}
        if isinstance(value, list):
            return [strip(item) for item in value]
        return value

    return json.dumps(strip(node), indent=2)


def main():
    process = start()
    try:
        init = request(process, 1, "initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "check", "version": "1"}})
        if sys.argv[1] == "tools":
            instructions = init["result"].get("instructions", "")
            if HIDDEN:
                instructions = instructions.replace(" to look at a screen without clicking to it, set_state puts the app in a declared state (list_states);", "")
                instructions = instructions.replace(" where_is gives the page object line of a control and the app line (razor, XAML) that makes it;", "")
                instructions = instructions.replace(" get_trace gives what a failed run did step by step (each step's page, control calls and waits, app events, the page after the failure);", "")
            print("INSTRUCTIONS:", instructions)
            tools = request(process, 2, "tools/list", {})["result"]["tools"]
            for tool in [tool for tool in tools if tool["name"] not in HIDDEN]:
                arguments = ", ".join(tool.get("inputSchema", {}).get("properties", {}).keys())
                print(f"\n- {tool['name']}({arguments}): {tool['description']}")
            return

        calls = json.load(open(sys.argv[2], encoding="utf-8"))
        for index, call in enumerate(calls, start=3):
            if call["tool"] in HIDDEN:
                print(f"== {call['tool']} (error)\nUnknown tool.\n")
                continue
            answer = request(process, index, "tools/call", {"name": call["tool"], "arguments": call.get("arguments", {})})
            result = answer.get("result", {})
            text = "".join(part.get("text", "") for part in result.get("content", []))
            if "where_is" in HIDDEN:
                text = without_app_source(text)
            flag = " (error)" if result.get("isError") else ""
            print(f"== {call['tool']}{flag}\n{text}\n")
    finally:
        process.stdin.close()
        process.terminate()


if __name__ == "__main__":
    main()
