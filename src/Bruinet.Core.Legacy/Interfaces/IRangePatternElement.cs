namespace Bruinet.Core.Interfaces;

/// <summary>
/// Interface for elements supporting RangeValue pattern (Windows UI Automation).
/// Used for Slider and Stepper controls on Windows.
/// Implemented by platform-specific drivers (e.g., FlaUIMauiElement).
/// </summary>
public interface IRangePatternElement
{
    /// <summary>
    /// Gets whether the element supports the RangeValue UI Automation pattern.
    /// </summary>
    bool SupportsRangeValue { get; }

    /// <summary>
    /// Sets the value using RangeValue.SetValue pattern.
    /// Value is clamped to min/max bounds before setting.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>True if successful, false if pattern not supported or operation failed.</returns>
    bool SetRangeValue(double value);

    /// <summary>
    /// Gets the current value from RangeValue.Value pattern.
    /// </summary>
    /// <returns>The current value, or null if pattern not supported.</returns>
    double? GetRangeValue();

    /// <summary>
    /// Gets the minimum value from RangeValue.Minimum pattern.
    /// </summary>
    /// <returns>The minimum value, or null if pattern not supported.</returns>
    double? GetRangeMinimum();

    /// <summary>
    /// Gets the maximum value from RangeValue.Maximum pattern.
    /// </summary>
    /// <returns>The maximum value, or null if pattern not supported.</returns>
    double? GetRangeMaximum();

    /// <summary>
    /// Gets the small change (step) value from RangeValue.SmallChange pattern.
    /// </summary>
    /// <returns>The step value, or null if pattern not supported.</returns>
    double? GetRangeSmallChange();
}
