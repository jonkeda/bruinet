
namespace Bruinet.Uat;

public static class UatControlAssertions
{
    public static void AssertVisible(object control, string name, int timeoutMs)
    {
        if (!WaitVisible(control, expected: true, timeoutMs))
            throw new InvalidOperationException($"Expected '{name}' to be visible.");
    }

    public static void AssertAbsentOrHidden(object control, string name, int timeoutMs)
    {
        if (!WaitUntil(() => IsAbsentOrHidden(control), timeoutMs))
            throw new InvalidOperationException($"Expected '{name}' to be hidden or absent.");
    }

    public static bool WaitVisible(object control, bool expected, int timeoutMs)
    {
        if (!expected)
            return WaitUntil(() => IsAbsentOrHidden(control), timeoutMs);

        try
        {
            return ((dynamic)control).WaitVisible(true, timeoutMs);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsAbsentOrHidden(object control)
    {
        return !IsExists(control) || IsVisible(control) != true;
    }

    public static bool IsExists(object control)
    {
        try
        {
            return ((dynamic)control).IsExists();
        }
        catch
        {
            return false;
        }
    }

    public static bool? IsVisible(object control)
    {
        try
        {
            return ((dynamic)control).IsVisible();
        }
        catch
        {
            return null;
        }
    }

    public static string? GetText(object control, int timeoutMs)
    {
        try
        {
            return ((dynamic)control).GetText(timeoutMs);
        }
        catch
        {
            return null;
        }
    }

    public static string? GetTextIfPresent(object control, int timeoutMs)
    {
        return IsExists(control) ? GetText(control, timeoutMs) : null;
    }

    public static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(condition);

        // Inside a running call the caller's budget bounds it (R2); Settle applies it.
        return Bruinet.Core.Calls.Settle.Until(condition, timeoutMs, pollingIntervalMs: 100);
    }
}
