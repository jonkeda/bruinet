namespace Bruinet.Samples.Shared.Commands;

/// <summary>
/// Options to customize AsyncRelayCommand behavior.
/// </summary>
[Flags]
public enum AsyncRelayCommandOptions
{
    /// <summary>Default behavior: no concurrent executions, exceptions thrown to caller.</summary>
    None = 0,

    /// <summary>Allow concurrent executions of the same command.</summary>
    AllowConcurrentExecutions = 1 << 0,

    /// <summary>Flow exceptions to TaskScheduler instead of throwing to caller.</summary>
    FlowExceptionsToTaskScheduler = 1 << 1,

    /// <summary>Execute only once until Reset() called. Use for submit/navigation commands.</summary>
    OnceOnly = 1 << 2,

    /// <summary>Skip IsBusy tracking for background operations.</summary>
    SkipBusyTracking = 1 << 3,
}
