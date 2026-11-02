using Bruinet.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bruinet.Logging;

/// <summary>
/// An app's log into <see cref="StepLog"/>: what it logs while a step runs is kept under that step, so the step's
/// result shows it. A warning and up; information too with <c>BRUINET_STEP_LOG=all</c>.
/// </summary>
public sealed class StepLogProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new StepLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class StepLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => Level(logLevel) is { } level && level >= StepLog.Minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (Level(logLevel) is { } level)
            {
                StepLog.Add(level, category, formatter(state, exception), exception);
            }
        }

        private static StepLogLevel? Level(LogLevel level) => level switch
        {
            LogLevel.Information => StepLogLevel.Information,
            LogLevel.Warning => StepLogLevel.Warning,
            LogLevel.Error or LogLevel.Critical => StepLogLevel.Error,
            _ => null,
        };
    }
}

/// <summary>Adds <see cref="StepLogProvider"/> to an app's services.</summary>
public static class StepLogServices
{
    /// <summary>The app's logging, with its log kept by the step that made it; and the exceptions nothing caught.</summary>
    /// <param name="services">The app's services, before they are built.</param>
    public static IServiceCollection AddBruinetStepLog(this IServiceCollection services)
    {
        StepLog.Watch();
        return services.AddLogging(logging => logging.AddProvider(new StepLogProvider()));
    }
}
