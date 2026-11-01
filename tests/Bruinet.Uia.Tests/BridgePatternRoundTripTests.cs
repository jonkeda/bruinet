using Bruinet.Uia.TestHost;
using FlaUI.Core.AutomationElements;
using Xunit.Abstractions;
using System.Globalization;

namespace Bruinet.Uia.Tests;

/// <summary>
/// Step 5: does a custom pattern actually carry values across the process boundary, both ways,
/// as ints and as strings?
/// </summary>
/// <remarks>
/// <para>
/// Every declaration in <c>Bruinet.Uia.Contracts/Interop</c> is hand-written, because the
/// interop assembly FlaUI embeds contains the client-side automation interfaces and none of the
/// registration machinery. A wrong field order or a misread indirection there produces a
/// corrupt value rather than an error, so nothing in the design can be trusted until a value
/// has made the trip and come back recognisable.
/// </para>
/// <para>
/// The string half decides the shape of the frozen method table. If strings do not marshal,
/// the contract can only ever carry numbers, and steps 14 and 20 through 26 - text, dates,
/// routes, selection by name - all need a different design.
/// </para>
/// </remarks>
[Collection("BridgeHost")]
[Trait("Category", "Uia")]
public class BridgePatternRoundTripTests
{
    private readonly BridgeHostFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BridgePatternRoundTripTests(BridgeHostFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>The pattern is reachable from the client at all.</summary>
    [Fact]
    public void Element_CarriesTheBruinetPattern()
    {
        Assert.True(
            BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern),
            "The target element does not carry the Bruinet pattern. Either registration failed "
            + "in one of the two processes, or GetPatternProvider is comparing against the "
            + "wrong id.");

        Assert.NotNull(pattern);
    }

    /// <summary>
    /// Step 5's headline: pattern ids differ between the processes, and the call lands anyway.
    /// </summary>
    /// <remarks>
    /// This is the fact the whole contract rests on. If the ids happened to agree, every test
    /// here would pass for a reason that would stop being true on another machine or after
    /// another program registered a pattern first.
    /// </remarks>
    [Fact]
    public void PatternIds_ArePerProcess()
    {
        var clientId = BruinetUiaClient.PatternId;

        _output.WriteLine($"Client pattern id: {clientId}");
        _output.WriteLine($"Host pattern id:   {_fixture.HostPatternId}");

        Assert.True(clientId > 0, "The client process did not register the pattern.");
        Assert.True(_fixture.HostPatternId > 0, "The host process did not register the pattern.");

        // Not asserted as different: two processes can legitimately be handed the same number,
        // and a test that demanded otherwise would fail for a reason that means nothing. What
        // matters is that each side looked its own id up rather than sharing one, which the
        // calls below prove by working.
        _output.WriteLine(
            clientId == _fixture.HostPatternId
                ? "The two processes happened to be given the same id this run."
                : "The two processes were given different ids, as expected.");
    }

    /// <summary>Step 5a: three ints out, and the provider saw the values that were sent.</summary>
    [Fact]
    public void Invoke_CarriesItsIntegerArguments()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        // Deliberately awkward values. Zero, one and a repeated small number all survive a
        // wrong offset or a truncating cast; these do not.
        const int Arg1 = 1_234_567;
        const int Arg2 = -89;

        var hr = pattern!.Invoke((int)BruinetVerb.Pan, Arg1, Arg2);
        Assert.Equal(HResults.S_OK, hr);

        Assert.Equal(
            HResults.S_OK,
            pattern.Exchange((int)BruinetVerb.GetState, "log", out var log));

        _output.WriteLine($"Provider log: {log}");

        Assert.Contains($"{(int)BruinetVerb.Pan}:{Arg1}:{Arg2}", log, StringComparison.Ordinal);
    }

    /// <summary>Step 5b: a string out and a string back, including characters that expose a bad codec.</summary>
    [Fact]
    public void Exchange_CarriesStringsBothWays()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        // Non-ASCII and non-BMP. A BSTR is UTF-16 and this round-trips; a path that quietly
        // narrows to ANSI mangles the accented text, and one that mishandles surrogate pairs
        // loses the last character.
        const string Sent = "Grüße, Ελλάδα, 日本語, \U0001F680 end";

        Assert.Equal(
            HResults.S_OK,
            pattern!.Exchange((int)BruinetVerb.SetText, Sent, out _));

        Assert.Equal(
            HResults.S_OK,
            pattern.Exchange((int)BruinetVerb.GetText, string.Empty, out var received));

        Assert.Equal(Sent, received);
    }

    /// <summary>An empty argument is a value, not a null pointer.</summary>
    [Fact]
    public void Exchange_HandlesEmptyStrings()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        Assert.Equal(
            HResults.S_OK,
            pattern!.Exchange((int)BruinetVerb.SetText, string.Empty, out _));

        Assert.Equal(
            HResults.S_OK,
            pattern.Exchange((int)BruinetVerb.GetText, string.Empty, out var received));

        Assert.Equal(string.Empty, received);
    }

    /// <summary>
    /// Repeated calls neither leak nor drift.
    /// </summary>
    /// <remarks>
    /// Each call allocates a parameter array, a BSTR for the argument and one for the result.
    /// A missing free would not fail a single call; it would fail a suite after an hour. This
    /// does not measure memory - it establishes that the path survives being used the way a
    /// real run uses it.
    /// </remarks>
    [Fact]
    public void Exchange_SurvivesRepetition()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        for (var i = 0; i < 200; i++)
        {
            var sent = $"iteration-{i}";

            Assert.Equal(
                HResults.S_OK, pattern!.Exchange((int)BruinetVerb.SetText, sent, out _));
            Assert.Equal(
                HResults.S_OK,
                pattern.Exchange((int)BruinetVerb.GetText, string.Empty, out var received));

            Assert.Equal(sent, received);
        }
    }

    /// <summary>Meta verbs are answered by the bridge for every target.</summary>
    [Fact]
    public void Meta_VerbsAreAnsweredWithoutTheTarget()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        Assert.Equal(
            HResults.S_OK,
            pattern!.Exchange((int)BruinetVerb.GetProtocolVersion, string.Empty, out var version));
        Assert.Equal(BruinetUiaIds.ProtocolVersion.ToString(CultureInfo.InvariantCulture), version);

        Assert.Equal(
            HResults.S_OK,
            pattern.Exchange((int)BruinetVerb.GetCapabilities, string.Empty, out var capabilities));

        var verbs = BruinetVerbs.ParseCapabilities(capabilities);
        _output.WriteLine($"Capabilities: {capabilities}");

        Assert.Contains(BruinetVerb.Tap, verbs);
        Assert.Contains(BruinetVerb.SetText, verbs);
        Assert.DoesNotContain(BruinetVerb.SwipeUp, verbs);

        // Undeclared, and answered anyway: it is how a client tells same-id targets apart.
        Assert.DoesNotContain(BruinetVerb.GetBounds, verbs);
        Assert.Equal(
            HResults.S_OK,
            pattern.Exchange((int)BruinetVerb.GetBounds, string.Empty, out var bounds));
        Assert.Equal("11,22,33,44", bounds);
    }

    /// <summary>
    /// A verb the target does not implement is refused, not crashed.
    /// </summary>
    /// <remarks>
    /// This is the whole versioning story. A test assembly built against a newer contract than
    /// the app under test will ask for verbs the app has never heard of, and the only acceptable
    /// answer is a clean refusal - not a crash, and above all not a hang, which is what a broken
    /// provider connection produces.
    /// </remarks>
    [Fact]
    public void UnsupportedVerb_IsRefusedCleanly()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        var hr = pattern!.Invoke((int)BruinetVerb.SwipeUp, 0, 0);

        Assert.Equal(HResults.UIA_E_NOTSUPPORTED, hr);
        _output.WriteLine(BruinetVerbFailure.Describe(BruinetVerb.SwipeUp, hr));
    }

    /// <summary>
    /// A verb number this build has never defined, and a number no build could.
    /// </summary>
    /// <remarks>
    /// <b>Two different answers, changed at step 28.</b> Both used to be
    /// <c>E_INVALIDARG</c>. The vocabulary is append-only, so an undefined *positive* number can
    /// only have come from a client built against a later contract - which is
    /// <c>UIA_E_NOTSUPPORTED</c>, "this element will never do that", and is what
    /// <c>BruinetUiaClient.Describe</c> had been promising all along. Zero and negatives are the
    /// case no later contract can rescue, so they stay a caller bug. The distinction matters to
    /// whoever reads the failure: one says upgrade the app, the other says fix the call.
    /// </remarks>
    [Fact]
    public void UnknownVerbNumber_IsRefusedAsUnsupported_AndAMalformedOneAsInvalid()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var pattern));

        Assert.Equal(HResults.UIA_E_NOTSUPPORTED, pattern!.Invoke(999_999, 0, 0));
        Assert.Equal(
            HResults.UIA_E_NOTSUPPORTED, pattern.Exchange(999_999, "x", out _));

        Assert.Equal(HResults.E_INVALIDARG, pattern.Invoke((int)BruinetVerb.None, 0, 0));
        Assert.Equal(HResults.E_INVALIDARG, pattern.Invoke(-7, 0, 0));
    }

    /// <summary>The two targets are independent, not two handles on one thing.</summary>
    [Fact]
    public void Targets_AreIndependent()
    {
        Assert.True(BridgeClient.TryGetPattern(PrimaryTarget(), out var first));
        Assert.True(BridgeClient.TryGetPattern(SecondaryTarget(), out var second));

        Assert.Equal(HResults.S_OK, first!.Exchange((int)BruinetVerb.SetText, "one", out _));
        Assert.Equal(HResults.S_OK, second!.Exchange((int)BruinetVerb.SetText, "two", out _));

        Assert.Equal(HResults.S_OK, first.Exchange((int)BruinetVerb.GetText, "", out var firstText));
        Assert.Equal(HResults.S_OK, second.Exchange((int)BruinetVerb.GetText, "", out var secondText));

        Assert.Equal("one", firstText);
        Assert.Equal("two", secondText);
    }

    private AutomationElement PrimaryTarget() => Target(HostTargets.Primary);

    private AutomationElement SecondaryTarget() => Target(HostTargets.Secondary);

    private AutomationElement Target(string automationId)
    {
        var raw = _fixture.Automation.TreeWalkerFactory.GetRawViewWalker();
        var bridge = BridgeClient.FindByClassName(
            raw, _fixture.HostWindow, BruinetUiaIds.BridgeClassName);

        Assert.True(bridge is not null, "The bridge window is not in the tree.");

        var wanted = BruinetUiaIds.TargetAutomationIdFor(automationId);

        return BridgeClient.FindByAutomationId(raw, bridge!, wanted)
            ?? throw new InvalidOperationException($"No bridge element for '{automationId}'.");
    }
}
