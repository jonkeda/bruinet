using Bruinet.Core.Calls;
using Bruinet.Core.Logging;

namespace Bruinet.Core.Configuration;

/// <summary>
/// What every stack's context options share (public API review, proposal 11): one home for the
/// timeouts, the call log, the context's name and the near-miss rule. A stack's
/// <c>…TestContextOptions</c> derives from this and adds only its own settings.
/// </summary>
/// <remarks>
/// Which value wins: what the fixture sets in code, then what the settings file holds, then these
/// defaults. The run switches (<c>BRUINET_HEAD</c>, <c>BRUINET_CALL_LOG</c>,
/// <c>BRUINET_TIMEOUT_SCALE</c>) apply on top of all three.
/// </remarks>
public abstract class TestContextOptionsBase
{
    /// <summary>The budgets every call takes from; <see cref="TimeoutSettings.Default"/> unless set.</summary>
    public TimeoutSettings Timeouts { get; set; } = TimeoutSettings.Default;

    /// <summary>The call log; <c>BRUINET_CALL_LOG</c>'s, or none, when null.</summary>
    public ITestLogger? Logger { get; set; }

    /// <summary>The context's name, for logs and messages; the stack's own name when null.</summary>
    public string? Name { get; set; }

    /// <summary>When a call that passed is reported as having nearly failed.</summary>
    public NearMissSettings NearMiss { get; set; } = NearMissSettings.Default;
}
