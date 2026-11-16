# Feature: Native controls

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.NativeAndroid.App |
| Area | Controls |
| Target | NATIVEANDROID, NATIVEIOS |
| Tags | native, controls |

The same control verbs as on MAUI and Html, on Android's own widgets: a check box, a radio group
and a spinner whose option is below its popup's fold.

@native @controls
## Scenario: Controls take what the user chooses

```gherkin
Given I am on the Controls page
When I check Agree
And I select Large from Size
And I select Navy from Color
Then Agree should be checked
And Size should have selected "Large"
And Color should have selected "Navy"
And Status should equal "Color Navy"
```
