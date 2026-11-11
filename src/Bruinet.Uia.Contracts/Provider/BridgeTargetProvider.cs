using System.Runtime.InteropServices;
using Bruinet.Uia.Interop;

namespace Bruinet.Uia.Provider;

/// <summary>
/// One raw-view-only UI Automation element standing for one instrumented target, and the
/// thing that actually carries the Bruinet pattern.
/// </summary>
[ComVisible(true)]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class BridgeTargetProvider
    : IRawElementProviderSimple, IRawElementProviderFragment, IBruinetAutomationProvider
{
    private readonly BridgeFragmentRoot _root;
    private readonly IBruinetVerbTarget _target;
    private readonly int _runtimeId;

    /// <summary>Set once the target has been withdrawn; the provider then answers nothing.</summary>
    private volatile bool _retired;

    // What the target threw the last time a verb failed, for GetLastFailure: E_FAIL alone says nothing.
    private volatile string? _lastFailure;

    /// <summary>
    /// Stops this provider answering for its target, without disconnecting it from UI Automation.
    /// </summary>
    internal void Retire() => _retired = true;

    internal BridgeTargetProvider(BridgeFragmentRoot root, IBruinetVerbTarget target, int runtimeId)
    {
        _root = root;
        _target = target;
        _runtimeId = runtimeId;
    }

    internal IBruinetVerbTarget Target => _target;

    // ---- IRawElementProviderSimple -------------------------------------------------------

    public ProviderOptions ProviderOptions => ProviderOptions.ServerSideProvider;

    /// <summary>
    /// Returns this element for the Bruinet pattern, and nothing for anything else.
    /// </summary>
    /// <remarks>
    /// The id is compared against this process's registration, never against a constant: UI
    /// Automation allocates pattern ids per process, so the number differs between the app
    /// under test and the test assembly even though the pattern is the same one.
    /// </remarks>
    public object? GetPatternProvider(int patternId)
    {
        if (!BruinetPatternRegistration.TryGetCurrent(out var registration, out _))
        {
            return null;
        }

        return patternId == registration!.PatternId ? this : null;
    }

    public object? GetPropertyValue(int propertyId) => propertyId switch
    {
        UiaPropertyIds.AutomationId => BruinetUiaIds.TargetAutomationIdFor(_target.AutomationId),
        UiaPropertyIds.ClassName => BruinetUiaIds.TargetClassName,
        UiaPropertyIds.ControlType => UiaControlTypeIds.Custom,
        UiaPropertyIds.ProviderDescription => BruinetUiaIds.ProviderDescription,
        UiaPropertyIds.FrameworkId => "Bruinet",

        // Named after the element it stands for, so a raw-tree dump reads as something rather
        // than as a row of identical custom elements.
        UiaPropertyIds.Name => _target.AutomationId,

        // Raw view only. See the same block on BridgeFragmentRoot.
        UiaPropertyIds.IsControlElement => false,
        UiaPropertyIds.IsContentElement => false,

        UiaPropertyIds.IsKeyboardFocusable => false,
        UiaPropertyIds.IsEnabled => !_retired && _target.IsAvailable,

        // Whether the target is still there, not where it is. See BoundingRectangle: asking the
        // app where its element sits costs a hop onto its UI thread, and UI Automation reads
        // this property on every element it walks past.
        UiaPropertyIds.IsOffscreen => _retired || !_target.IsAvailable,

        _ => null,
    };

    /// <summary>Null: only the fragment root has a host provider.</summary>
    public IRawElementProviderSimple? HostRawElementProvider => null;

    // ---- IRawElementProviderFragment -----------------------------------------------------

    public object? Navigate(NavigateDirection direction)
    {
        var siblings = _root.Children;
        var index = Array.IndexOf(siblings, this);

        return direction switch
        {
            NavigateDirection.Parent => _root,
            NavigateDirection.NextSibling
                => index >= 0 && index + 1 < siblings.Length ? siblings[index + 1] : null,
            NavigateDirection.PreviousSibling
                => index > 0 ? siblings[index - 1] : null,

            // A leaf. The bridge is deliberately one level deep: it mirrors nothing, it only
            // offers verbs, and a nested bridge tree would be a second view of the app's
            // structure that could disagree with the real one.
            _ => null,
        };
    }

    /// <summary>
    /// Identifies this element to clients across queries.
    /// </summary>
    /// <remarks>
    /// The leading 3 is <c>UiaAppendRuntimeId</c>, which tells UI Automation to prefix the host
    /// window's own runtime id. Without it two apps running the bridge would hand out colliding
    /// ids and a client would cache one app's element for another's.
    /// </remarks>
    public int[] GetRuntimeId() => [3, _runtimeId];

    /// <summary>
    /// Empty, deliberately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This used to report the real element's rectangle, and it cost the suite five times its
    /// runtime.</b> Reading a MAUI element's layout has to happen on the app's UI thread, so
    /// every read was a dispatch and a blocking wait. UI Automation reads bounds and
    /// <c>IsOffscreen</c> on every element a walk goes past - far more often than it calls a
    /// method - so a page publishing five targets turned each of the client's tree walks into
    /// ten round trips onto a UI thread that was busy being the app. Tests that had taken
    /// milliseconds took ten seconds, and then UI Automation's own transaction timeout started
    /// firing on unrelated calls.
    /// </para>
    /// <para>
    /// <b>And it was never the right property to answer.</b> This element is not the control; it
    /// is a carrier for verbs, in the raw view only, that nothing hit-tests and no assistive
    /// technology sees. Publishing a rectangle invited clients to treat it as somewhere on
    /// screen. Where the real element sits is a question about the real element, and the answer
    /// is a verb - <c>GetState("Bounds")</c> - paid for by whoever actually asks.
    /// </para>
    /// </remarks>
    public UiaRect BoundingRectangle => default;

    public object[]? GetEmbeddedFragmentRoots() => null;

    /// <summary>Does nothing. Focus is a verb, applied to the real element.</summary>
    public void SetFocus()
    {
    }

    public IRawElementProviderFragmentRoot? FragmentRoot => _root;

    // ---- IBruinetAutomationProvider ------------------------------------------------------

    /// <inheritdoc/>
    public int Invoke(int verb, int arg1, int arg2)
    {
        if (_retired)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (Unrecognised(verb, out var refusal))
        {
            return refusal;
        }

        var kind = (BruinetVerb)verb;

        if (kind == BruinetVerb.Ping)
        {
            return _target.IsAvailable ? HResults.S_OK : HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (!_target.IsAvailable)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (!_target.Capabilities.Contains(kind))
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        try
        {
            return _target.Invoke(kind, arg1, arg2);
        }
        catch (Exception error)
        {
            // The target contract says implementations do not throw. This is what happens when
            // one does anyway, and it is not allowed to become a COM failure of its own; what it
            // threw is kept for GetLastFailure.
            _lastFailure = Failure(kind, error);
            return HResults.E_FAIL;
        }
    }

    /// <inheritdoc/>
    public int Exchange(int verb, string argument, out string result)
    {
        result = string.Empty;

        if (_retired)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (Unrecognised(verb, out var refusal))
        {
            return refusal;
        }

        var kind = (BruinetVerb)verb;

        // Meta verbs are answered here, for every target, whatever the host implements.
        switch (kind)
        {
            case BruinetVerb.GetProtocolVersion:
                result = BruinetUiaIds.ProtocolVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                return HResults.S_OK;

            case BruinetVerb.GetCapabilities:
                result = BruinetVerbs.FormatCapabilities(_target.Capabilities);
                return HResults.S_OK;

            case BruinetVerb.Ping:
                return _target.IsAvailable ? HResults.S_OK : HResults.UIA_E_ELEMENTNOTAVAILABLE;

            case BruinetVerb.GetBounds:
                return ReadBounds(out result);

            case BruinetVerb.GetLastFailure:
                result = _lastFailure ?? string.Empty;
                return HResults.S_OK;
        }

        if (!_target.IsAvailable)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (!_target.Capabilities.Contains(kind))
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        try
        {
            var hr = _target.Exchange(kind, argument ?? string.Empty, out var targetResult);
            result = targetResult ?? string.Empty;
            return hr;
        }
        catch (Exception error)
        {
            _lastFailure = Failure(kind, error);
            result = string.Empty;
            return HResults.E_FAIL;
        }
    }

    private static string Failure(BruinetVerb verb, Exception error) => $"{verb} threw {error.GetType().Name}: {error.Message}";

    private int ReadBounds(out string result)
    {
        result = string.Empty;

        if (!_target.IsAvailable)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        try
        {
            if (!_target.TryGetScreenBounds(out var bounds))
            {
                return HResults.UIA_E_ELEMENTNOTAVAILABLE;
            }

            result = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            return HResults.S_OK;
        }
        catch (Exception)
        {
            return HResults.E_FAIL;
        }
    }

    /// <summary>
    /// Refuses a verb number this build has no meaning for, telling the two reasons apart.
    /// </summary>
    /// <param name="verb">The number as it arrived on the wire.</param>
    /// <param name="refusal">The HRESULT to return, when this returns true.</param>
    /// <returns>Whether the verb was refused.</returns>
    private static bool Unrecognised(int verb, out int refusal)
    {
        if (verb <= (int)BruinetVerb.None)
        {
            refusal = HResults.E_INVALIDARG;
            return true;
        }

        if (!Enum.IsDefined((BruinetVerb)verb))
        {
            refusal = HResults.UIA_E_NOTSUPPORTED;
            return true;
        }

        refusal = HResults.S_OK;
        return false;
    }
}
