using Bruinet.Core.Locators;

namespace Bruinet.Core.Exceptions;

/// <summary>
/// Exception thrown when a locator strategy is not supported by a specific driver.
/// For example, FlaUI does not support XPath locators.
/// </summary>
public class LocatorNotSupportedException : BruinetException
{
    /// <summary>
    /// The locator strategy that is not supported.
    /// </summary>
    public LocatorStrategy Strategy { get; }

    /// <summary>
    /// The name of the driver that does not support the strategy.
    /// </summary>
    public string DriverName { get; }

    /// <summary>
    /// Creates a new LocatorNotSupportedException.
    /// </summary>
    /// <param name="strategy">The unsupported locator strategy.</param>
    /// <param name="driverName">The name of the driver.</param>
    /// <param name="suggestion">Optional suggestion for an alternative approach.</param>
    public LocatorNotSupportedException(LocatorStrategy strategy, string driverName, string? suggestion = null)
        : base(BuildMessage(strategy, driverName, suggestion))
    {
        Strategy = strategy;
        DriverName = driverName;
    }

    private static string BuildMessage(LocatorStrategy strategy, string driverName, string? suggestion)
    {
        var message = $"Locator strategy '{strategy}' is not supported by the {driverName} driver.";
        if (!string.IsNullOrEmpty(suggestion))
        {
            message += $" {suggestion}";
        }
        return message;
    }
}
