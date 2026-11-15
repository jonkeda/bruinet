# Feature: Presenter Loads A UAT Folder

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Presenter |
| Area | Workspace |
| Target | MAUI |
| Tags | smoke, presenter, load |

@smoke @presenter @load
## Scenario: Presenter shows the sample MAUI UAT workspace

```gherkin
Given I am on the Presenter page
When I tap Reload
Then Status Summary should contain "Ready"
Then Workspace Tree should contain "bdd.config.md"
And Workspace Tree should contain "Features"
And All Workspace Tree should contain "main-page-greeting.feature.md"
And All Workspace Tree should contain "user-form-basic-input.feature.md"
And Workspace Summary should contain "App ok"
```
