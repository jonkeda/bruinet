namespace Bruinet.Core;

/// <summary>
/// Specifies how text should be entered into an element.
/// </summary>
public enum TextInputMethod
{
    /// <summary>
    /// Types each character as keyboard events (slower but realistic, fires input events).
    /// </summary>
    Keys,

    /// <summary>
    /// Pastes text from clipboard (faster, bypasses keyboard but still fires events).
    /// </summary>
    Paste,

    /// <summary>
    /// Directly sets the element's value property (fastest, no keyboard events fired).
    /// </summary>
    SetValue
}
