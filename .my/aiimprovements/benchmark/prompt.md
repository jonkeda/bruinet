You are working on an app. Its code is in {repo} (a .NET solution with a Todo sample under samples/Todo). Work only in
that folder and in {client}.

Task: {task}

You have a test tool server (Bruinet MCP). Reach it through a small client from a shell (Bash), from the folder {client}:
- list its instructions and tools: `{vars} python mcp.py tools`
- call tools: write a JSON file `[{{"tool": "<name>", "arguments": {{...}}}}, ...]` in that folder and run
  `{vars} python mcp.py calls <file>.json`. Each such run starts a fresh server; calls in one file share one server. Use
  absolute Windows paths with forward slashes in arguments.
Always pass these variables exactly as shown. Read the server's instructions first and follow them.

You may read, search and edit files, and run dotnet (build, test){env_note}. Do not run git in any form. Do not read
anything outside the two folders above. Keep to about 40 tool calls in all.

At the end, report what was wrong and what you changed (file and line), and whether the scenarios now pass.
