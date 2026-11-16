# Contacts journeys: the worked example

Status: proposed, 2026-11-16, revised for a few journeys per module. Part of
[the plan](000-Plan.md); read its model first. This shows the Contacts journeys and what each
head needs to run them against Acceptance, without a mock.

## What changes for Contacts

The 16 MAUI Contacts scenarios stay as they are: mock backend, MAUI only, headless on every PR.
Contacts gets three journeys that cover its main flows on all three apps, plus one small
scenario for a deliberate difference. They are chosen from the
[Contacts parity matrix](https://github.com/exact-mobile-apps/construction/blob/main/.migration/modules/contacts/310-ParityMatrix.md): the list,
search and filter, the detail, the form, and the contact picker that Projects uses.

## The journeys

A first draft. The wording is the contract: every head binds exactly these phrases. `{run}` is
the run's unique prefix, for example `UIT-0416-ios`, so parallel heads and earlier runs never
collide. Cleanup is not in the scenarios: a shared after-scenario hook deletes this run's
records through the API, and the nightly sweep removes leftovers.

```gherkin
@journey @contacts
Feature: Contacts parity

  Scenario: Create, change and find a client
    Given I am logged in to Bouw7
    When I create a Client contact named "{run} Client" with the comment "Called on Monday"
    Then the contact "{run} Client" shows type Client and the comment "Called on Monday"
    When I change the comment of "{run} Client" to "Quote requested"
    And I search the contact list for "{run} Client"
    Then the contact list shows only "{run} Client"
    When I open "{run} Client" from the contact list
    Then the contact shows the comment "Quote requested"

  Scenario: Filter the contact list by type and keep a favourite
    Given I am logged in to Bouw7
    And a Supplier contact named "{run} Supplier" exists
    And a Client contact named "{run} Client" exists
    When I show only Supplier contacts
    And I search the contact list for "{run}"
    Then the contact list shows only "{run} Supplier"
    When I mark "{run} Supplier" as a favourite
    And I reopen the contact list
    Then the contact list still shows only Supplier contacts
    And "{run} Supplier" is a favourite

  @projects
  Scenario: A new client gets a project
    Given I am logged in to Bouw7
    And a Client contact named "{run} Client" exists
    When I create a project named "{run} Kitchen" for the client "{run} Client"
    Then the project "{run} Kitchen" shows the client "{run} Client"
    When I open the client from the project "{run} Kitchen"
    Then the contact "{run} Client" is shown

  @maui @native-ios
  Scenario: Share a contact as a card
    # Deliberate difference: Android has no share action (Contacts parity matrix, iOS rows).
    Given I am logged in to Bouw7
    And a Client contact named "{run} Client" exists
    When I share the contact "{run} Client"
    Then the share sheet offers a contact card
```

The `Given ... exists` steps arrange data through the API, so each journey checks only the
screens it is about. The second journey proves that the type filter is kept, which the parity
matrix lists as persisted on every app. The last scenario has head tags, so the native Android
head skips it, and the report shows the difference instead of hiding it.

## Screens and page objects per head

The MAUI head reuses its existing page objects. The native heads write new ones with the same
class names, drafted by the Scraper from captures, so a reader finds the same page in each head.

| Page object | Native Android | Native iOS |
|---|---|---|
| `ContactListPage` (exists in MAUI) | `ContactsFragment`, `contacts_frag.xml` | `ContactListVC`, `ContactList.storyboard` |
| `ContactFilterPage` (exists) | The type filter in `ContactsFragment` | The filter in `ContactListVC` |
| `ContactCreateEditPage` (exists) | `ContactCreationWindow`, `creation_contact.xml` | `ContactCreationVC`, `ContactCreation.storyboard` |
| `ContactDetailPage` (exists) | `ContactDetailActivity` | `ContactDetailVC`, `ContactDetail.storyboard` |
| `ContactSelectorPage` (exists) | The selector in `ProjectCreationActivity` | `ContactSelectionVC` |
| `ProjectCreatePage` (exists) | `ProjectCreationActivity` | `ProjectCreationVC`, `ProjectCreation.storyboard` |
| `ProjectDetailPage` (exists) | `ProjectDetailActivity` | `ProjectDetailVC`, `ProjectDetail.storyboard` |

Each native head also needs a main-screen page that opens Contacts and Projects. Where these
sit in the native navigation (a tab, the drawer or the main menu) is still to be checked. The
two project pages are shared with the Projects journeys later.

## Control objects per head

MAUI has all of these. Native Android has most in `Bruinet.NativeAndroid`; native iOS gets
them in the new `Bruinet.NativeIos`.

- A list with rows found by their text, for the contact list and the picker.
- A search field.
- A text field and a multi-line comment field. Android edits the comment in an HTML editor
  screen, so its comment control opens that screen and returns.
- A type picker: an Android `Spinner`, an iOS selection screen.
- A multi-select type filter and a favourite toggle.
- A toolbar action (new, save, share) and a back action.
- An alert and, for iOS and MAUI, the system share sheet.

## Fixture per head

| Head | Ready means |
|---|---|
| MAUI | Logged in on Acceptance (Windows: the WebView2 helper), the Projects overview loaded. |
| Native Android | Debug APK installed, environment picked, deep-link login done, synchronization finished, main screen shown. |
| Native iOS | App installed on the simulator, environment unlocked and picked, deep-link login done, main screen shown. |

`Given I am logged in to Bouw7` checks that the fixture is ready, and logs in again only if
the session was lost. Because the filter journey checks a kept setting, each head also resets
the contact filter before it, through the app's settings or a fresh install.

## Steps

- **Per head:** every screen `When` and `Then` above, using that head's page objects.
- **Shared:** the run prefix, the `Given ... exists` steps, the cleanup hook, and the account
  selection behind `Given I am logged in to Bouw7`.

A `Then` checks only what the user sees. There are no request checks, because there is no
mock to ask; the contract tests keep proving the wire format.

## Work in the native apps

- **Android:** no change is expected. The form and detail have ids (`input_name`, `spinner`,
  `input_comments`, `contact_name`, `contact_type_name`, `remarks`), and the list has
  `recycler_view` and `action_new`. To check: ids on a list row, the filter and the picker.
- **iOS:** the app has no identifiers. Start with text and type locators. Add an
  `accessibilityIdentifier` only where a locator is ambiguous, such as two text fields
  without labels on the contact form, and only after the native team agrees.

## Done when

- The three journeys pass on MAUI (Windows), native Android and native iOS on the same night,
  and the share scenario passes on MAUI and native iOS.
- The Contacts parity table shows every journey against every head, and each red cell is
  triaged into the parity matrix or fixed.
- The head check reports no missing phrase on any head.
- A second run on the same night passes too, which proves the cleanup works.

## Open questions

- Can a contact and a project be deleted through the API, or only archived? Cleanup depends on it.
- Where do Contacts and project creation sit in each native app's navigation?
- Does the iOS picker search the server only from three characters? The search text must be
  long enough on every head.
- Do the apps show the type in the same words (Client, Klant)? The journeys must run with one
  language on every device.
- Does native Android keep the type filter between visits as the parity matrix says, and on
  iOS too? If not, that `Then` is the first parity difference the suite finds.
