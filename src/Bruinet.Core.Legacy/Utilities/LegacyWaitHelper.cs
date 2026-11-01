using System.Diagnostics;

namespace Bruinet.Core.Utilities;

/// <summary>
/// The first model's waits: each polls with a budget of its own and swallows what the condition
/// throws. For the WPF, WinForms and Stride stacks; a call-model stack waits in its call's poll, or
/// below a call with <c>Bruinet.Core.Calls.Settle</c> (AD-029).
/// </summary>
public static class LegacyWaitHelper
{

    /// <summary>
    /// Wait for a condition to become true.
    /// </summary>
    /// <param name="condition">Condition to check.</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
    /// <param name="pollingIntervalMs">Interval between checks in milliseconds.</param>
    /// <returns>True if condition became true, false if timeout.</returns>
    public static bool WaitFor(Func<bool> condition, int timeoutMs, int pollingIntervalMs = 100)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (condition())
                    return true;
            }
            catch (Exception)
            {
                // Swallow exceptions and continue polling
            }

            WaitHelper.Pause(pollingIntervalMs);
        }

        try
        {
            return condition();
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Wait for a value getter to return a value that matches the predicate.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="getValue">Function to get the current value.</param>
    /// <param name="predicate">Predicate to check the value.</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
    /// <param name="pollingIntervalMs">Interval between checks in milliseconds.</param>
    /// <returns>True if predicate became true, false if timeout.</returns>
    public static bool WaitFor<T>(Func<T?> getValue, Func<T?, bool> predicate, int timeoutMs, int pollingIntervalMs = 100)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var value = getValue();
                if (predicate(value))
                    return true;
            }
            catch (Exception)
            {
                // Swallow exceptions and continue polling
            }

            WaitHelper.Pause(pollingIntervalMs);
        }

        try
        {
            var value = getValue();
            return predicate(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Wait for a value getter to return a value that matches the predicate, and report why the
    /// last attempt failed when it never does.
    /// </summary>
    /// <remarks>
    /// Unlike the overload without <paramref name="lastError"/>, a failing read is not lost: when
    /// the wait times out, <paramref name="lastError"/> holds the exception the final attempt
    /// threw, so the caller's message can say "not found" rather than only "did not happen".
    /// It is null when the final attempt read a value that did not match.
    /// </remarks>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="getValue">Function to get the current value.</param>
    /// <param name="predicate">Predicate to check the value.</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
    /// <param name="pollingIntervalMs">Interval between checks in milliseconds.</param>
    /// <param name="lastError">The final attempt's exception, or null.</param>
    /// <returns>True if predicate became true, false if timeout.</returns>
    public static bool WaitFor<T>(Func<T?> getValue, Func<T?, bool> predicate, int timeoutMs,
        int pollingIntervalMs, out Exception? lastError)
    {
        var sw = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                lastError = null;
                if (predicate(getValue()))
                    return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (sw.ElapsedMilliseconds >= timeoutMs)
                return false;

            WaitHelper.Pause(pollingIntervalMs);
        }
    }

    /// <summary>
    /// Wait for a value getter to return a non-null value.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="getValue">Function to get the value.</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
    /// <param name="pollingIntervalMs">Interval between checks in milliseconds.</param>
    /// <returns>The value if found, or default if timeout.</returns>
    public static T? WaitForValue<T>(Func<T?> getValue, int timeoutMs, int pollingIntervalMs = 100) where T : class
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var value = getValue();
                if (value != null)
                    return value;
            }
            catch (Exception)
            {
                // Swallow exceptions and continue polling
            }

            WaitHelper.Pause(pollingIntervalMs);
        }

        try
        {
            return getValue();
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Get elapsed time while waiting for a condition.
    /// </summary>
    /// <param name="condition">Condition to check.</param>
    /// <param name="timeoutMs">Maximum time to wait.</param>
    /// <param name="pollingIntervalMs">Polling interval.</param>
    /// <returns>Tuple of (success, elapsed milliseconds).</returns>
    public static (bool Success, int ElapsedMs) WaitForWithTiming(Func<bool> condition, int timeoutMs, int pollingIntervalMs = 100)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (condition())
                    return (true, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                // Swallow exceptions and continue polling
            }

            WaitHelper.Pause(pollingIntervalMs);
        }

        try
        {
            if (condition())
                return (true, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            return (false, (int)sw.ElapsedMilliseconds);
        }

        return (false, (int)sw.ElapsedMilliseconds);
    }
}
