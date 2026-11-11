using System.Reflection;
using Bruinet.Core.Locators;

namespace Bruinet.Core.Calls;

/// <summary>
/// A control's name on its page object, found by its locator when a message needs it (review 01, finding 1): the page's
/// properties are read only then, so a passing call pays nothing and no control's constructor has to be told its name.
/// </summary>
public static class ControlMembers
{
    /// <summary>The page object's property that gives a control with <paramref name="locator"/>; null when none does.</summary>
    /// <param name="page">The page object the control is on; null when unknown.</param>
    /// <param name="locator">The control's locator.</param>
    public static string? NameOf(object? page, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (page is null)
        {
            return null;
        }

        foreach (var property in page.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.PropertyType.IsValueType || property.PropertyType == typeof(string)
                || property.PropertyType.GetProperty("Locator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not { } locatorProperty
                || locatorProperty.PropertyType != typeof(Locator))
            {
                continue;
            }

            try
            {
                if (property.GetValue(page) is { } control && Equals(locatorProperty.GetValue(control), locator))
                {
                    return property.Name;
                }
            }
            catch (TargetInvocationException)
            {
                // A property that cannot be read now (it needs a row, an index) names no control here.
            }
        }

        return null;
    }

    /// <summary>
    /// The page's controls that are shown now, by their property names: at most <paramref name="most"/>, asked once each
    /// within <paramref name="budget"/>. For a "not found" to say what the page does show (review 01, proposal 6).
    /// </summary>
    /// <param name="page">The page object; null when unknown.</param>
    /// <param name="most">The most names given.</param>
    /// <param name="budget">How long the reads may take in all.</param>
    public static IReadOnlyList<string> ShownOf(object? page, int most = 12, TimeSpan? budget = null)
    {
        List<string> shown = [];
        if (page is null)
        {
            return shown;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var limit = budget ?? TimeSpan.FromSeconds(1);
        foreach (var property in page.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (shown.Count >= most || clock.Elapsed > limit)
            {
                break;
            }

            if (property.GetIndexParameters().Length > 0 || property.PropertyType.IsValueType || property.PropertyType == typeof(string)
                || property.PropertyType.GetMethod("IsVisible", Type.EmptyTypes) is not { } isVisible)
            {
                continue;
            }

            try
            {
                if (property.GetValue(page) is { } control && isVisible.Invoke(control, null) is true)
                {
                    shown.Add(property.Name);
                }
            }
            catch (TargetInvocationException)
            {
                // A control that cannot be asked now is left out.
            }
        }

        return shown;
    }
}
