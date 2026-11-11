using System.Globalization;
using Bruinet.Uia;
using Bruinet.Uia.Provider;

namespace Bruinet.Uia.TestHost;

/// <summary>
/// A verb target that remembers what it was asked to do and will read it back.
/// </summary>
/// <remarks>
/// <para>
/// The point of the spike is to prove that arguments survive the trip into another process
/// and that results survive the trip back. A target that only returned success would prove
/// the call happened and nothing about what arrived, so this one echoes: every
/// <see cref="Invoke"/> is appended to a log the client can read back with
/// <see cref="BruinetVerb.GetState"/>, which makes an argument that arrives mangled visible
/// rather than merely successful.
/// </para>
/// <para>
/// Also the negative case. <see cref="BruinetVerb.SwipeUp"/> is deliberately absent from
/// <see cref="Capabilities"/>, so a client asking for it exercises the "this element does not
/// do that" path rather than a crash.
/// </para>
/// </remarks>
internal sealed class RecordingTarget : IBruinetVerbTarget
{
    private readonly List<string> _log = [];
    private readonly Lock _gate = new();

    private string _text = string.Empty;

    internal RecordingTarget(string automationId) => AutomationId = automationId;

    public string AutomationId { get; }

    public IReadOnlyCollection<BruinetVerb> Capabilities { get; } =
    [
        BruinetVerb.Tap,
        BruinetVerb.DoubleTap,
        BruinetVerb.SwipeLeft,
        BruinetVerb.Pan,
        BruinetVerb.Focus,
        BruinetVerb.SetText,
        BruinetVerb.GetText,
        BruinetVerb.GetState,
    ];

    /// <summary>Always. The spike has nothing that goes away; step 28 tests that separately.</summary>
    public bool IsAvailable => true;

    public int Invoke(BruinetVerb verb, int arg1, int arg2)
    {
        // Before the log, so a probe for a particular HRESULT does not also count as a gesture.
        if (arg1 == HostTargets.ReturnThisHResult)
        {
            return arg2;
        }

        if (arg1 == HostTargets.ThrowOnInvoke)
        {
            throw new InvalidOperationException(HostTargets.ThrownMessage);
        }

        lock (_gate)
        {
            _log.Add(string.Create(
                CultureInfo.InvariantCulture, $"{(int)verb}:{arg1}:{arg2}"));
        }

        return HResults.S_OK;
    }

    public int Exchange(BruinetVerb verb, string argument, out string result)
    {
        result = string.Empty;

        // The provider substitutes an empty string for null before it gets here.
        if (argument.StartsWith(HostTargets.ReturnHResultPrefix, StringComparison.Ordinal)
            && int.TryParse(
                argument[HostTargets.ReturnHResultPrefix.Length..],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var chosen))
        {
            result = "answered";
            return chosen;
        }

        switch (verb)
        {
            case BruinetVerb.SetText:
                lock (_gate)
                {
                    _text = argument;
                }

                return HResults.S_OK;

            case BruinetVerb.GetText:
                lock (_gate)
                {
                    result = _text;
                }

                return HResults.S_OK;

            case BruinetVerb.GetState:
                lock (_gate)
                {
                    result = argument switch
                    {
                        "log" => string.Join("|", _log),
                        "count" => _log.Count.ToString(CultureInfo.InvariantCulture),
                        _ => string.Empty,
                    };
                }

                return HResults.S_OK;

            default:
                return HResults.UIA_E_NOTSUPPORTED;
        }
    }

    /// <summary>A fixed rectangle, so the bounds path is exercised with a checkable value.</summary>
    public bool TryGetScreenBounds(out BruinetBounds bounds)
    {
        bounds = new BruinetBounds(11, 22, 33, 44);
        return true;
    }
}
