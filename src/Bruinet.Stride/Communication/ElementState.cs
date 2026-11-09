using System.Text.Json.Serialization;

namespace Bruinet.Stride.Communication;

/// <summary>
/// Serializable state information for a UI element.
/// </summary>
public class ElementState
{
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isHitTestVisible")]
    public bool IsHitTestVisible { get; set; }

    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; set; }

    [JsonPropertyName("controlType")]
    public string? ControlType { get; set; }

    /// <summary>
    /// The element's identity: its Stride <c>UIElement.Id</c>, the same for the same element and new
    /// for a replaced one. A command addresses an element by it as the target <c>#key</c>.
    /// </summary>
    [JsonPropertyName("instanceKey")]
    public string? InstanceKey { get; set; }

    [JsonPropertyName("bounds")]
    public ElementBounds Bounds { get; set; } = new();

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1f;

    [JsonPropertyName("isChecked")]
    public bool? IsChecked { get; set; }

    [JsonPropertyName("selectedIndex")]
    public int SelectedIndex { get; set; } = -1;

    [JsonPropertyName("selectedText")]
    public string? SelectedText { get; set; }

    [JsonPropertyName("items")]
    public List<string>? Items { get; set; }

    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("minimum")]
    public double? Minimum { get; set; }

    [JsonPropertyName("maximum")]
    public double? Maximum { get; set; }

    [JsonPropertyName("isOpen")]
    public bool? IsOpen { get; set; }
}

/// <summary>
/// Screen rectangle for UI element bounds.
/// </summary>
public class ElementBounds
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Width == 0 || Height == 0;

    [JsonIgnore]
    public int CenterX => X + Width / 2;

    [JsonIgnore]
    public int CenterY => Y + Height / 2;

    public bool Contains(int x, int y)
        => x >= X && x < X + Width && y >= Y && y < Y + Height;
}
