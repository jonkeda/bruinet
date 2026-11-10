# Bruinet WinForms Sample Application

> **Looking for a complete example?** [Todo](Todo/README.md) is a MAUI app tested at every tier:
> unit, integration against WireMock and SQLite, hermetic and live UI tests with Bruinet on
> Windows and Android, and manual charters, all traced to user journeys.

This directory contains a sample WinForms application demonstrating the Bruinet UI testing framework for Windows Forms.

## Projects

### Bruinet.Samples.WinForms.App
A simple Windows Forms application showcasing various UI controls:

**Features:**
- TextBox controls for username and password entry
- ComboBox for role selection (Admin, User, Guest)
- CheckBox for "Remember me" option
- Button controls for login and clear actions
- Label for status messages
- ListBox with sample items

**Controls and Automation IDs:**
- `txtUsername` - Username input field
- `txtPassword` - Password input field
- `chkRemember` - Remember me checkbox
- `cmbRole` - Role selection dropdown
- `btnLogin` - Login button
- `btnClear` - Clear form button
- `lblStatus` - Status message label
- `lstItems` - Sample items listbox

### Bruinet.Samples.WinForms.UITests
Comprehensive UI test suite demonstrating the Bruinet framework capabilities.

**Test Classes:**

1. **LoginPageTests** - Basic login page functionality
   - Form display verification
   - Text input (username/password)
   - Checkbox interactions
   - ComboBox selections
   - Button clicks
   - Status label verification

2. **AdvancedLoginTests** - Advanced patterns and workflows
   - Wait/Check/Assert pattern demonstrations
   - Complete user workflow testing
   - Form reset verification
   - Multiple login attempts
   - Control visibility testing

## Getting Started

### Running the Sample App

```bash
dotnet run --project Bruinet.Samples.WinForms.App/Bruinet.Samples.WinForms.App.csproj
```

The application will launch a simple login form for manual testing or as a target for automated tests.

### Running the Tests

To run the tests against a running instance of the sample application:

1. **First**, launch the sample application:
   ```bash
   dotnet run --project Bruinet.Samples.WinForms.App/Bruinet.Samples.WinForms.App.csproj
   ```

2. **Then**, in another terminal, run the tests:
   ```bash
   dotnet test Bruinet.Samples.WinForms.UITests/Bruinet.Samples.WinForms.UITests.csproj
   ```

**Note:** Tests are currently marked with `[Skip]` because they require a running application instance. To use these tests:

1. Uncomment the application launch code in `InitializeAsync()`
2. Update the path to the compiled sample application executable
3. Remove the `Skip` attribute from test methods

## Page Object Pattern

The tests demonstrate the **Page Object Model (POM)** pattern with the `LoginPage` class:

```csharp
public class LoginPage : PageBase
{
    // Control wrappers
    private readonly TextBoxControl _usernameField;
    private readonly ButtonControl _loginButton;
    // ... other controls
    
    // High-level actions
    public void EnterUsername(string username) { }
    public void ClickLogin() { }
    
    // State assertions
    public string GetStatusMessage() { }
    public bool IsRememberMeChecked() { }
}
```

This pattern:
- **Encapsulates** UI elements and their interactions
- **Centralizes** control locators for easy maintenance
- **Provides** clear, business-readable test methods
- **Isolates** tests from UI implementation changes

## Is/Wait/Check/Assert Pattern

The framework implements the **Is/Wait/Check/Assert** pattern for robust control interaction:

```csharp
// Is - Check current state (returns bool, doesn't wait)
bool exists = control.Exists();

// Wait - Poll for condition with timeout (returns bool)
bool found = control.WaitExists(expected: true, timeoutMs: 5000);

// Check - Wait and throw if condition fails (for assertions mid-test)
control.CheckExists(expected: true, timeoutMs: 5000);

// Assert - Explicit test assertion (typically last step)
control.AssertExists("expected control to exist");
```

## Control Interactions

### TextBox
```csharp
control.Enter("text");           // Set text
control.Append("more");          // Append to existing text
control.Clear();                 // Clear the field
var text = control.GetText();    // Get current text
```

### CheckBox
```csharp
control.Check();                 // Check the box
control.Uncheck();               // Uncheck the box
control.SetChecked(true);        // Set to specific state
bool checked = control.IsChecked(); // Get current state
```

### ComboBox
```csharp
control.SelectByText("Admin");       // Select by text
control.SelectByIndex(0);            // Select by index
var text = control.GetSelectedItem(); // Get selected item
var items = control.GetItems();       // Get all items
```

### Button
```csharp
control.Click();                 // Click the button
control.DoubleClick();           // Double-click
control.RightClick();            // Right-click (context menu)
```

### Label
```csharp
var text = control.GetText();    // Get label text
control.AssertTextEquals("expected");
control.AssertTextContains("partial");
```

## Test Data

The sample application uses hardcoded data:

**Roles:**
- Admin
- User
- Guest

**Sample Items:**
- Item 1
- Item 2
- Item 3
- Item 4
- Item 5

## Extension Points

To extend these tests:

1. **Add More Controls** - Create new page objects for additional forms
2. **Create Test Fixtures** - Implement setup/teardown for data preparation
3. **Add Data-Driven Tests** - Use xUnit's `[MemberData]` or `[InlineData]` for multiple test cases
4. **Implement Custom Assertions** - Add domain-specific assertions to page objects
5. **Add Screenshots on Failure** - Implement failure handlers to capture screenshots

Example of extending the page object:

```csharp
public class LoginPage : PageBase
{
    // New control
    private readonly ListBoxControl _itemsList;
    
    // New action
    public void SelectItem(string itemText)
    {
        _itemsList.SelectByText(itemText);
    }
    
    // New assertion
    public void AssertItemExists(string itemText)
    {
        var items = _itemsList.GetItems();
        items.Should().Contain(itemText);
    }
}
```

## Troubleshooting

### Application Doesn't Launch
- Ensure the sample app executable is built
- Check the path in `FlaUIDriverAdapter.Launch()`
- Verify AutomationId values match in the form and tests

### Tests Timeout
- Application might be slow to respond
- Increase timeoutMs in Wait/Check methods
- Verify all controls exist and are named correctly

### Element Not Found
- Check AutomationId in MainForm.cs matches test code
- Ensure control is visible/not hidden
- Verify control isn't in a nested container that's hidden

## Best Practices

1. **Use Page Objects** - Always wrap UI elements in page objects
2. **Meaningful Names** - Use descriptive AutomationId values
3. **Single Responsibility** - Each test should verify one behavior
4. **Explicit Waits** - Use WaitExists/WaitVisible instead of Thread.Sleep
5. **Readable Assertions** - Use FluentAssertions for clear error messages
6. **Arrange-Act-Assert** - Structure tests clearly (AAA pattern)
7. **Test Data** - Keep test data close to tests or use fixtures
8. **Error Handling** - Capture screenshots on failure for debugging

## Related Documentation

- [Bruinet Framework Guide](../../docs/)
- [WinForms Testing Architecture](../../Architecture/20_WorldToolsArchitecture.md)
- [FlaUI Documentation](https://github.com/Roemer/FlaUI)

## Contributing

To add new test examples:

1. Create new page objects in `Pages/` directory
2. Create corresponding test classes in `Tests/` directory
3. Follow the existing naming and pattern conventions
4. Include XML documentation for public members
5. Skip tests if they require a running application instance

## License

This sample is part of the Bruinet project.
