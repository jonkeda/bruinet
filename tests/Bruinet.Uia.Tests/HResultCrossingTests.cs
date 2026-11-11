using Bruinet.Uia.TestHost;
using FlaUI.Core.AutomationElements;
using Xunit.Abstractions;

namespace Bruinet.Uia.Tests;

/// <summary>
/// Step 43: which HRESULTs a custom pattern actually delivers to the client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 26 found that success codes do not survive, by accident.</b> A test would not go red;
/// the app's log said <c>S_FALSE</c> and the client read <c>S_OK</c>. UI Automation's marshalling
/// of a custom pattern reports every success HRESULT as <c>S_OK</c>, so <c>S_FALSE</c> - the code
/// three verbs used for "understood, refused, changed nothing" - had never reached a caller.
/// </para>
/// <para>
/// <b>This is that finding turned into a test, before anything else relies on it.</b> Step 43
/// gives the meaning a new home in <c>BRUINET_E_DECLINED</c>, a customer-defined failure code, and
/// the whole fix rests on failure codes crossing intact - including one Microsoft never issued.
/// Assuming that and being wrong would put the programme back where step 26 found it, with a
/// refusal reported as success and a test that cannot go red.
/// </para>
/// <para>
/// The host answers with whatever HRESULT it is asked for, on both methods, so the question can
/// be put directly rather than inferred from a verb that happens to decline.
/// </para>
/// </remarks>
[Collection("BridgeHost")]
[Trait("Category", "Uia")]
public class HResultCrossingTests
{
    private readonly BridgeHostFixture _fixture;
    private readonly ITestOutputHelper _output;

    public HResultCrossingTests(BridgeHostFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Every failure code the bridge speaks arrives exactly as it was sent.
    /// </summary>
    /// <remarks>
    /// <c>BRUINET_E_DECLINED</c> is the one that matters and the one nothing had tested: it is
    /// ours, not Microsoft's, and the customer-defined bit is a part of the encoding UI
    /// Automation had no reason to preserve.
    /// </remarks>
    [Theory]
    [InlineData(HResults.BRUINET_E_DECLINED)]
    [InlineData(HResults.UIA_E_NOTSUPPORTED)]
    [InlineData(HResults.UIA_E_ELEMENTNOTAVAILABLE)]
    [InlineData(HResults.UIA_E_TIMEOUT)]
    [InlineData(HResults.E_INVALIDARG)]
    [InlineData(HResults.E_FAIL)]
    public void FailureHResults_CrossIntact(int sent)
    {
        Assert.True(BridgeClient.TryGetPattern(Target(), out var pattern));

        var onInvoke = pattern!.Invoke(
            (int)BruinetVerb.Tap, HostTargets.ReturnThisHResult, sent);

        var onExchange = pattern.Exchange(
            (int)BruinetVerb.GetState, HostTargets.ReturnHResultPrefix + sent, out _);

        _output.WriteLine(
            $"sent 0x{sent:X8}  invoke 0x{onInvoke:X8}  exchange 0x{onExchange:X8}");

        Assert.Equal(sent, onInvoke);
        Assert.Equal(sent, onExchange);
    }

    /// <summary>
    /// Success codes other than <c>S_OK</c> do not survive, on either method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The finding itself, pinned so it cannot be rediscovered.</b> If a future Windows makes
    /// this pass through, this test fails and somebody reads the comment on
    /// <c>HResults.S_FALSE</c> - which is a much better outcome than the code quietly starting to
    /// work and nobody knowing they may rely on it again.
    /// </para>
    /// <para>
    /// Two arbitrary success codes as well as <c>S_FALSE</c>, because the rule being tested is
    /// about the severity bit rather than about one value.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(HResults.S_FALSE)]
    [InlineData(2)]
    [InlineData(0x00040001)]
    public void SuccessHResults_ArriveAsSOk(int sent)
    {
        Assert.True(BridgeClient.TryGetPattern(Target(), out var pattern));

        var onInvoke = pattern!.Invoke(
            (int)BruinetVerb.Tap, HostTargets.ReturnThisHResult, sent);

        var onExchange = pattern.Exchange(
            (int)BruinetVerb.GetState, HostTargets.ReturnHResultPrefix + sent, out _);

        _output.WriteLine(
            $"sent 0x{sent:X8}  invoke 0x{onInvoke:X8}  exchange 0x{onExchange:X8}");

        Assert.Equal(HResults.S_OK, onInvoke);
        Assert.Equal(HResults.S_OK, onExchange);
    }

    /// <summary>
    /// A failing <c>Exchange</c> loses its payload, so a verb picks one channel or the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured while building step 43, and it narrowed the design.</b> The plan said an
    /// outcome could travel "as a value in the payload, or as a failure HRESULT". It cannot travel
    /// as both: COM does not marshal an <c>[out]</c> parameter back when the method returns a
    /// failure, so the string arrives empty and whatever it was going to explain is gone.
    /// </para>
    /// <para>
    /// <b>So the two channels are exclusive, and the choice is per verb.</b> A refusal that needs
    /// to say <i>why</i> must return <c>S_OK</c> and put the reason in the payload - which is what
    /// <c>InvokeMenuItem</c> does, and the reason it must not be "tidied up" into a failure code.
    /// A refusal whose verb and code say everything - going back with nothing to pop - uses
    /// <see cref="HResults.BRUINET_E_DECLINED"/> and carries no payload.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFailingExchange_ArrivesWithNoPayload()
    {
        Assert.True(BridgeClient.TryGetPattern(Target(), out var pattern));

        var hr = pattern!.Exchange(
            (int)BruinetVerb.GetState,
            HostTargets.ReturnHResultPrefix + HResults.BRUINET_E_DECLINED,
            out var payload);

        Assert.Equal(HResults.BRUINET_E_DECLINED, hr);

        Assert.True(
            payload.Length == 0,
            $"A failing Exchange delivered '{payload}'. If that has started working, a refusal "
            + "can explain itself and carry a failure code at the same time, and several verbs "
            + "written around the restriction could be simplified - see BRUINET_E_DECLINED.");
    }

    private AutomationElement Target()
    {
        var raw = _fixture.Automation.TreeWalkerFactory.GetRawViewWalker();
        var bridge = BridgeClient.FindByClassName(
            raw, _fixture.HostWindow, BruinetUiaIds.BridgeClassName);

        Assert.True(bridge is not null, "The bridge window is not in the tree.");

        var wanted = BruinetUiaIds.TargetAutomationIdFor(HostTargets.Primary);

        return BridgeClient.FindByAutomationId(raw, bridge!, wanted)
            ?? throw new InvalidOperationException($"No bridge element for '{wanted}'.");
    }

    /// <summary>
    /// A verb the app's own handler threw on answers <c>E_FAIL</c>, and what it threw crosses to the client
    /// through <c>GetLastFailure</c>, so a test can say the app threw rather than "none answered" (review 05, finding 8).
    /// </summary>
    [Fact]
    public void A_Verb_ThatThrew_IsEFail_AndGetLastFailure_SaysWhat()
    {
        Assert.True(BridgeClient.TryGetPattern(Target(), out var pattern));

        var hr = pattern!.Invoke((int)BruinetVerb.Tap, HostTargets.ThrowOnInvoke, 0);
        var asked = pattern.Exchange((int)BruinetVerb.GetLastFailure, string.Empty, out var said);

        _output.WriteLine($"invoke 0x{hr:X8}  last failure 0x{asked:X8} '{said}'");
        Assert.Equal(HResults.E_FAIL, hr);
        Assert.Equal(HResults.S_OK, asked);
        Assert.Contains("Tap threw InvalidOperationException", said, StringComparison.Ordinal);
        Assert.Contains(HostTargets.ThrownMessage, said, StringComparison.Ordinal);
    }
}
