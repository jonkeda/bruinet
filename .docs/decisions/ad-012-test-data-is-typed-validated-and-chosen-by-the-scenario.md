# AD-012: Test Data Is Typed, Validated, And Chosen By The Scenario File

A test's starting data is a named **test set**: a JSON file in the app's `TestData/` folder,
read strictly into the app's real types (the API's DTOs for the server half, the app's own model
for the device half). An app describes its test sets once, in a `TestDataDefinition<TSet>`: the
folder, the root type and the rules. One engine, `Bruinet.TestData`, checks them - shape (unknown
field, missing field, wrong value), the app's own rules and consistency - and reports
`file:line: CODE path: message`. The xUnit gate and `bruinet data`
(`tools/Bruinet.Cli`, which loads the app's assembly at run time) both run that
engine, so they cannot disagree.

A UAT scenario file names its test set in its `TestData` metadata; `DefaultTestData` in
`uat.config.md` covers files that do not. The fixture loads what the file names - it does not
hard-code it. Scenarios **add** to a test set; they do not change or remove its records, because
the device's database is not reset between scenarios. Additions and checks are step tables bound
to the same real types, compared on the listed columns only.

A mock response body is a contract type or a recorded sample, never an anonymous object.
`BRUINET001` (shipped in `Bruinet.Api.Mocking`) reports one.

**Ruled out:** anonymous-object stub bodies (they compile whatever their fields are called, and a
test passes against JSON the app would never be sent); a private copy of a model for seeding (it
does not break when the model changes); lenient loading (a typo becomes a `null`, and a UI test
fails far from the cause); a test set name in fixture code (the scenario file is where a reader
looks for its starting state); "world" as the term (it meant three things - it is "test set").

**Broken when:** a test-set file is read with options that allow unknown members; a stub body is
`new { ... }`; seeding goes through a type the app does not use; the gate and the tool validate
with different code; a UAT fixture hard-codes the test set its scenarios run on; or a scenario
changes a default record that later scenarios rely on without a per-scenario reset.
