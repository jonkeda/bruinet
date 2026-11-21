---
name: api-object
description: Write or extend an API object of a suite - the suite's own words for one subject of the app's back end (Contacts, Projects), over Bruinet's API session - for test data made and removed beside the screen, a saved form checked against what was stored, or a scenario on the api head. Use when a test needs a record made, found or deleted through the API, when a suite still has its own HttpClient for set-up or clean-up, when a form's save should be checked against the stored record, or when a scenario's Then is about stored data.
---

# An API object

An API object is to a subject of the back end what a page object is to a screen. The guide is
[API testing](../../../docs/guides/api-testing.md); the rule is
[AD-046](../../../.docs/decisions/ad-046-a-test-reaches-a-service-through-api-objects-on-a-session.md). Bouw7's
`Bouw7.Web.UITests/Support/Api` is the worked example.

## Never

- A test, a step definition or a page object that names `ApiSession`, `HttpClient` or `HttpRequestMessage`: use or
  add a member of an API object.
- A host check, a token fetch, a `Writes` check or a delete-by-marker written in the suite: the session has them.
- A delete of a record that does not go through `DeleteMarked` or `Sweep`.
- A generic HTTP step (`When I POST /contact`).
- A sleep after a write: wait for the record with `Settle.Read` on the API object.

## Steps

1. **Find what is there.** The suite's `Support/Api` folder: the class that makes the session (`<App>Api.As(account)`),
   its API objects and its `MarkedResource` declarations. Add to these before adding a class.
2. **Learn the call.** In this order: the suite's description (`api.json`, or the observed `api.observed.json`); an
   exchange of the page's own call (`BRUINET_API_EXCHANGES`, or `context.Calls.WriteOf(...)` around the page
   object's save); only then a call tried on the environment, as an account that may write, with the run's marker in
   every name.
3. **Write the member in the suite's words.** `Contacts.Create(name, type)`, `Contacts.Called(name)`,
   `Contacts.Delete(id)`: what a test wants, not the address it goes to. What is the app's own (the body of a create,
   a list's query) stays in the API object; nothing else does.
4. **Declare what it makes.** A kind of record a test makes gets a `MarkedResource`: its list, how one is read, how
   one is deleted, and `Keep` when the service refuses a delete it still needs. A record with a fixed name is a
   `SeedRecord`.
5. **Test it on the environment**, as the suite's data tests do: made, found, deleted; a record without the marker
   refused; an account whose `Writes` is `no` refused. Run only those tests.
6. **A scenario** says it as a phrase of the suite (`Given a contact "{run} Arend" exists`), bound to the API object.
   A scenario whose Then is about stored data is tagged `@api` and runs on the suite's `api` head.

## A form checked against what was stored

`context.Calls.WriteOf(save)` gives the call the save made; `FormCheck.Map` finds each typed value in its body;
`FormCheck.Compare` holds typed, sent and stored side by side. A field with no place in the body, and a stored value
that differs, are findings to report, not to hide by changing what the test types.

## When the suite has a description

Generate the client from it at build time (Kiota) and build it on `session.Client`, so the hosts, the sign-in, the
rule on writes and the exchanges hold for it. The API object becomes a thin layer over the generated client; its
members and the tests that call them do not change.
