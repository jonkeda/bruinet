---
mode: agent
description: Make Bruinet page objects and controls from an app's recorded pages, controls first, until the project builds.
tools: ['bruinet']
---

Follow the `scrape-pages` skill (`.github/skills/scrape-pages/SKILL.md`), in agent mode with the
`bruinet` MCP server:

1. `scrape_list_projects`; pick the project the user means.
2. When pages are to be recorded: `scrape_start`, let the user browse, `scrape_stop`.
3. `scrape_list_page_objects`, `scrape_list_pages`, `scrape_detect_patterns`; propose the controls to make and
   the pages to cover, and wait for the user's choice.
4. Controls first, then pages: `scrape_get_page` for the elements, `scrape_get_header` for each
   file's first line, show the code, then `scrape_write_file`.
5. `scrape_build` until it passes. Report what was made, and any locator that had to fall back to
   position.
