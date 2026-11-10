namespace Bruinet.Stride.Automation;

/// <summary>
/// A handle on one UI element of the game, by its identity. Each read asks the game for the
/// element's state now (one command, addressed as <c>#key</c>); an element the game no longer has
/// is stale (R5), never another element with the same name.
/// </summary>
public sealed class StrideElement : IStrideElement
{
    /// <summary>The prefix of a target that names an element by its identity.</summary>
    public const string KeyPrefix = "#";

    private readonly IStrideDriver _driver;

    /// <summary>The element whose state <paramref name="found"/> is, as the lookup that found it read it.</summary>
    /// <exception cref="ArgumentException">The state is of an element that does not exist, or has no identity.</exception>
    public StrideElement(IStrideDriver driver, ElementState found)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        ArgumentNullException.ThrowIfNull(found);
        if (!found.Exists || string.IsNullOrEmpty(found.InstanceKey))
            throw new ArgumentException("An element handle needs an element that exists and has an identity.", nameof(found));

        InstanceKey = found.InstanceKey;
        AutomationId = found.Name;
        ControlType = found.ControlType;
    }

    /// <summary>The element for <paramref name="state"/>, or null when it does not exist.</summary>
    public static StrideElement? From(IStrideDriver driver, ElementState state)
        => state.Exists && !string.IsNullOrEmpty(state.InstanceKey) ? new StrideElement(driver, state) : null;

    /// <inheritdoc />
    public string InstanceKey { get; }

    /// <inheritdoc />
    public string? AutomationId { get; }

    /// <inheritdoc />
    public string? ControlType { get; }

    private string Target => KeyPrefix + InstanceKey;

    // The state now; an element that is gone is stale.
    private ElementState Now()
    {
        var state = _driver.GetState(Target);
        return state.Exists ? state : throw new StaleElementException();
    }

    /// <inheritdoc />
    public bool Visible => Now().IsVisible;

    /// <inheritdoc />
    public bool Enabled => Now().IsEnabled;

    /// <inheritdoc />
    public bool? Checked => Now().IsChecked;

    /// <inheritdoc />
    public string? Text => Now().Text;

    /// <inheritdoc />
    public double? RangeValue => Now().Value;

    /// <inheritdoc />
    public double? RangeMinimum => Now().Minimum;

    /// <inheritdoc />
    public double? RangeMaximum => Now().Maximum;

    /// <inheritdoc />
    public Rectangle Rect
    {
        get
        {
            var bounds = Now().Bounds;
            return new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    /// <inheritdoc />
    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var state = Now();
        return name.ToLowerInvariant() switch
        {
            "automationid" or "name" => state.Name,
            "controltype" => state.ControlType,
            "text" => state.Text,
            "checked" => state.IsChecked?.ToString(),
            "value" => state.Value?.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <inheritdoc />
    public void Click() => Act("Click");

    /// <inheritdoc />
    public void Toggle() => Act("Toggle");

    /// <inheritdoc />
    public void SetText(string text) => Act("SetText", text);

    /// <inheritdoc />
    public void SetRangeValue(double value) => Act("SetSliderValue", value);

    /// <inheritdoc />
    public void ScrollIntoView(int timeoutMs)
    {
    }

    /// <inheritdoc />
    public IStrideElement? TryFindElement(Locator locator)
        => From(_driver, _driver.Find(Target, StrideLocators.TargetOf(locator)));

    /// <inheritdoc />
    public IReadOnlyList<IStrideElement> FindElements(Locator locator)
        => [.. _driver.FindAll(Target, StrideLocators.TargetOf(locator)).Select(state => From(_driver, state)).OfType<IStrideElement>()];

    /// <inheritdoc />
    public IReadOnlyList<IStrideElement> Children()
        => [.. _driver.GetChildren(Target).Select(state => From(_driver, state)).OfType<IStrideElement>()];

    /// <summary>"Button 'Save'", for messages.</summary>
    public override string ToString() => $"{ControlType} '{AutomationId ?? Target}'";

    // An action on an element that is gone is stale, not "refused".
    private void Act(string method, params object[] args)
    {
        Now();
        _driver.Act(method, Target, args);
    }
}

/// <summary>What a locator names in the Stride game: an element's <c>Name</c>.</summary>
public static class StrideLocators
{
    /// <summary>
    /// The pipe target for <paramref name="locator"/>: the name for an AutomationId, Id, Name or
    /// AccessibilityId locator. Stride's UI has nothing else to find an element by.
    /// </summary>
    /// <exception cref="LocatorNotSupportedException">Any other strategy.</exception>
    public static string TargetOf(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return locator.Strategy switch
        {
            LocatorStrategy.AutomationId or LocatorStrategy.Id or LocatorStrategy.Name or LocatorStrategy.AccessibilityId => locator.Value,
            _ => throw new LocatorNotSupportedException(locator.Strategy, "Stride"),
        };
    }
}
