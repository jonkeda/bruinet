# Feature: Presenter Edits The Workspace Config

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Presenter |
| Area | Config |
| Target | MAUI |
| Tags | smoke, presenter, config |

@smoke @presenter @config
## Scenario: Config tab shows what the config derives to

```gherkin
Given I am on the Presenter page
When I tap Workspace Mode
And I tap Config
Then Config State should contain "bdd.config.md"
And Config Derived Target should contain "MAUI"
And Config Derived Pages should contain "Bruinet.Maui.UITests.dll"
And Config Derived App should contain "configured override"
When I tap Tree
And I tap Present Mode
```

@smoke @presenter @config
## Scenario: Editing the fixture marks the config unsaved and revert takes it back

```gherkin
Given I am on the Presenter page
When I tap Workspace Mode
And I tap Config
And I clear Config Fixture
And I enter "ShellFixture" into Config Fixture
Then Config State should contain "unsaved"
When I tap Config Revert
Then Config Fixture should contain "MauiFixture"
When I tap Tree
And I tap Present Mode
```
