# Prompt for the Mac: J0 on native iOS

Paste the text below into the Mac agent. It is self-contained; the links point into this
folder.

---

You are on the Mac. Your job is the iOS part of a small proof of concept: one journey, J0,
must pass from the same feature file on MAUI Windows, native Android and native iOS. Windows
does MAUI and Android; you do native iOS (`exact-bouw-ios`) through Bruinet's
`Bruinet.NativeIos` platform, which you built in phases I0 to I5.

**Read first:** `Brinell/.my/native/030-Contacts-Pilot.md` (the section "First: a small proof
of concept" and the step 0 and step 1 results), `Brinell/.my/native/ios/100-Handoff.md`, and
`Brinell/AGENTS.md` with its required reading.

**The journey.** J0, written in the pilot plan:

```gherkin
Given I am logged in to Bouw7
When I create a Client contact named "{run} Client"
And I search the contact list for "{run} Client"
Then the contact list shows only "{run} Client"
When I open "{run} Client" from the contact list
Then the contact shows the name "{run} Client" and type Client
```

If Windows has not pushed J0 into
`Journeys/Exact.Bouw7.Journeys/Features/Contacts/CTS.J-contacts-parity.feature.md` yet, add it
there yourself as `@CTS.J0 @journey @contacts`, with exactly these phrases, and say so on the
board. Phrase changes are agreed on the board, never made on one machine only.

**What to build, in order:**

1. **Start from I4 and I5,** which are done. Only the contact type picker and the search bar
   in `exact-bouw-ios` may still need proving live; do that inside J0, not in the gallery.
2. **The fixture** in `Journeys/Exact.Bouw7.NativeIos.UITests` (Construction repository), on
   `NativeIosTestFixtureBase`. Login: pick Acceptance in the environment picker on the login
   screen (visible in debug builds), get a portal token headless through the Acceptance API
   client in `Exact.Bouw7.Journeys`, and open `exactbouw://login.do?token=…`. Wait for
   synchronization and the main screen.
3. **Page objects** for the contact list, the create form and the detail, with the same class
   and control names as the MAUI head (`Journeys/Exact.Bouw7.Maui.UITests`). `exact-bouw-ios`
   has no accessibility identifiers, so use text locators; add an identifier in the iOS app
   only where text is ambiguous.
4. **Steps** only for the J0 phrases the shared and generic steps do not bind. The head check
   (`head-check.md` in the output folder) lists them.
5. **Run J0 twice in a row** on the simulator. Record the run times on the board.

**Rules:**

- **Half a day per blocker.** When something takes longer, write it on
  `Brinell/.my/native/050-Remaining-Work.md`, work around it or skip it, and go on.
- **No framework work beyond what J0 needs.** No other journeys, no Scraper, no generic list
  and dialog steps.
- **Accounts and secrets.** Credentials come only from `construction/.env.local` on the Mac;
  never print, log or commit them. The refresh token rotates, so the Mac and Windows must not
  use the same account at the same time. Use a different account from Windows, or agree a time
  slot on the board.
- **Records.** Every record name starts with `{run}`; the shared cleanup hook deletes them after
  each scenario.
- **Git.** Brinell is a submodule on branch `move`; Construction is on branch `Dade_100_Remaining`.
  Pull and rebase before every push, because both machines push. Bruinet fixes go straight on
  `move`. Run `dotnet format whitespace` on what you change.
- **Board.** Keep your rows on `ios/100-Handoff.md` current, and add one Events line per work
  session: date, Mac, what happened, hours.

**Done when** J0 passes twice in a row on the iOS simulator and the board says so. Then stop
and wait for the owner; do not start J1 to J4.
