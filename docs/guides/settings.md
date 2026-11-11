# Test Settings

Bruinet test settings live in `src/Bruinet.Core/Settings`.

## Purpose

Settings provide a shared way to load environment-like test configuration from
JSON files without hardcoding values in test code or UAT scenarios.

## Files

Default request:

```text
TestSettings/
  testsettings.json
  testsettings.local.json
  scenarios/<ScenarioId>.json
```

`testsettings.json` is the default file. `testsettings.local.json` is optional
and should be used for local overrides. Scenario files are optional and use the
configured `ScenarioConvention`.

## JSON Shape

```json
{
  "include": ["shared.json"],
  "settings": {
    "capabilities": {
      "hardware": false,
      "liveApi": false
    },
    "uat": {
      "startupMode": "deterministic"
    }
  }
}
```

Rules:

- Files must be JSON objects.
- Only JSON is supported in this implementation slice.
- Includes are required files and are loaded before later overrides.
- Values are merged case-insensitively.
- Later files override earlier files.

## Access

Use `TestSettings` for dynamic access:

```csharp
var settings = context.GetSettings();
var useLiveApi = settings.GetRequired<bool>("capabilities.liveApi");
```

Use `[TestSettingsSection]` for typed binding:

```csharp
[TestSettingsSection("capabilities")]
public sealed class CapabilitySettings
{
    public bool Hardware { get; set; }
    public bool LiveApi { get; set; }
}
```

UAT attaches resolved settings to `BddExecutionContext`; commands can retrieve
`TestSettings` or typed settings through the context settings extensions.

## Source Files

- `src/Bruinet.Core/Settings/TestSettings.cs`
- `src/Bruinet.Core/Settings/JsonTestSettingsProvider.cs`
- `src/Bruinet.Core/Settings/TestSettingsRequest.cs`
- `src/Bruinet.Core/Settings/TestSettingsSectionAttribute.cs`
- `src/Bruinet.Bdd/BddExecutionContextSettingsExtensions.cs`
