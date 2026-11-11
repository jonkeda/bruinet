using System.Globalization;

namespace Bruinet.Samples.Maui.App.Converters;

/// <summary>
/// Converts strings to bool - returns true if string is not null or empty.
/// Also supports ConverterParameter comparison.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter != null && value is string strValue)
        {
            return strValue == parameter.ToString();
        }
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return parameter.ToString();
        }
        return string.Empty;
    }
}

/// <summary>
/// Converts int to bool - returns true if value is greater than 0.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("A one-way converter.");
    }
}

/// <summary>
/// Converts bool IsPlaying to Play/Pause text.
/// </summary>
public class PlayPauseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
        {
            return isPlaying ? "⏸ Pause" : "▶ Play";
        }
        return "▶ Play";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("A one-way converter.");
    }
}

/// <summary>
/// Inverts a bool value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Converts bool (HasChildren) to folder/file icon.
/// </summary>
public class BoolToFolderIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasChildren)
        {
            return hasChildren ? "📁" : "📄";
        }
        return "📄";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("A one-way converter.");
    }
}

/// <summary>
/// Converts double (0-100) to progress value (0.0-1.0) for ProgressBar.
/// </summary>
public class DoubleToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue / 100.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double progressValue)
        {
            return progressValue * 100.0;
        }
        return 0.0;
    }
}
