namespace Bruinet.Uia.TestHost;

/// <summary>The targets this host publishes, named where the tests can reach them.</summary>
/// <remarks>
/// Public so the harness can address a target by the same constant the host registered it
/// under. A test that spelled the id out itself would keep passing after the host renamed it.
/// </remarks>
public static class HostTargets
{
    /// <summary>The target the tests drive.</summary>
    public const string Primary = "SpikeTarget";

    /// <summary>A second target, so the fragment has siblings to navigate between.</summary>
    public const string Secondary = "SpikeSecondTarget";

    /// <summary>
    /// Passed as <c>arg1</c> to any invoked verb, makes the target return <c>arg2</c> as its
    /// HRESULT.
    /// </summary>
    /// <remarks>
    /// <b>So a test can ask which HRESULTs survive the wire rather than assuming.</b> Step 26
    /// found that every success code arrives as <c>S_OK</c>; step 43 needed to know whether a
    /// customer-defined failure code crosses intact before giving one a meaning. Neither question
    /// can be answered without a provider that will return an arbitrary value on demand.
    /// </remarks>
    public const int ReturnThisHResult = unchecked((int)0xB121DE11);

    /// <summary>
    /// An <c>Exchange</c> argument of this form makes the target return that HRESULT.
    /// </summary>
    /// <remarks>
    /// The same question on the other method. The two are marshalled by different code inside UI
    /// Automation, which is why step 26 measured both and found them to agree.
    /// </remarks>
    public const string ReturnHResultPrefix = "return-hresult:";

    /// <summary>An <c>Invoke</c> whose first argument is this makes the target throw, as an app's handler can.</summary>
    /// <remarks>What the bridge does then: <c>E_FAIL</c>, and the exception's message kept for <c>GetLastFailure</c>.</remarks>
    public const int ThrowOnInvoke = unchecked((int)0xB121DE12);

    /// <summary>The message the target throws with on <see cref="ThrowOnInvoke"/>.</summary>
    public const string ThrownMessage = "the app's handler failed";
}
