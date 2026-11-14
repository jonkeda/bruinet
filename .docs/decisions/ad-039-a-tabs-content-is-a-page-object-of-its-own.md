# AD-039: A Tab's Content Is A Page Object Of Its Own

On a web page with tabs, each tab's content is a page object of its own: a `TabPageBase<TParent, TSelf>`
whose container is the tab's panel and whose `Tab` is found in the parent. `Open()` selects the tab when it is not
selected, then returns only once the tab says it is selected (`aria-selected="true"` by default) and its panel is
shown. A UI kit's tab page (Vuetify's `v-tabs`, …) derives from it with that kit's tab and panel locators, outside
`Bruinet.Html`.

**Why:** a tab's content is as large as a page, and an app with many tabs (Bouw7) needs it found, scraped and
reused like one. Switching tabs changes no URL, so a page object keyed by the page alone acted on whichever panel
was showing: the tab that was open before, while the new one was still loading.

**Ruled out:**
- a tab switch that returns before the tab is selected and its panel shown;
- members of a tab's content declared on the host page;
- a UI kit's tab locators in `Bruinet.Html`.

**Broken when:** a test clicks a tab and then acts on its content without `Open()`; a page object has a tab's
controls as its own members; `Bruinet.Html` names a UI kit's class (`v-tab`, `mat-tab`).
