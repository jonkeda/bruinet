namespace Bruinet.Uia.Tests;

/// <summary>
/// Step 8: pins the parts of the contract that cannot change without breaking a deployed app.
/// </summary>
/// <remarks>
/// <para>
/// These are not tests of behaviour. They are tests that particular constants still have
/// particular values, because those values cross a process boundary between two assemblies
/// that are built and deployed separately. A refactor that "tidies" a GUID or reorders an enum
/// compiles, passes every behavioural test, and breaks every app under test that has not been
/// rebuilt - and does so silently, since the symptom is a pattern that is simply not there.
/// </para>
/// <para>
/// So the values are written out literally below rather than referenced. A test that asserted
/// <c>PatternGuid == BruinetUiaIds.PatternGuid</c> would pass no matter what anyone changed it
/// to, which is exactly the failure it is meant to catch.
/// </para>
/// </remarks>
[Trait("Category", "Uia")]
public class ContractTests
{
    /// <summary>The pattern GUID is the contract between the two processes.</summary>
    [Fact]
    public void PatternGuid_IsUnchanged()
    {
        Assert.Equal(
            new Guid("e1ff558b-b42c-400f-9597-d3ed8d84fda4"),
            BruinetUiaIds.PatternGuid);
    }

    /// <summary>The two interface ids are registered with the pattern and are equally frozen.</summary>
    [Fact]
    public void InterfaceIds_AreUnchanged()
    {
        Assert.Equal("d890790c-64e5-47fb-8539-c1a73c307c46", BruinetUiaIds.ProviderInterfaceIid);
        Assert.Equal("1a5456c2-a3a3-4175-9679-ec46b85915a8", BruinetUiaIds.ClientInterfaceIid);
    }

    /// <summary>
    /// The provider and client interfaces must not share an id.
    /// </summary>
    /// <remarks>
    /// They have identical members, so the obvious tidy-up is to collapse them into one type.
    /// UI Automation takes both ids at registration and treats them as different things; giving
    /// it the same id twice is not something it complains about at registration time.
    /// </remarks>
    [Fact]
    public void ProviderAndClientInterfaces_AreDistinct()
    {
        Assert.NotEqual(BruinetUiaIds.ProviderInterfaceIid, BruinetUiaIds.ClientInterfaceIid);
        Assert.NotEqual(
            typeof(IBruinetAutomationProvider).GUID, typeof(IBruinetAutomationPattern).GUID);
    }

    /// <summary>
    /// The declared interfaces carry the ids the contract says they do.
    /// </summary>
    /// <remarks>
    /// The constants and the attributes are two separate statements of the same fact, and the
    /// registration uses the constants while UI Automation's QueryInterface uses the attributes.
    /// If they drift, registration succeeds and no element ever appears to carry the pattern.
    /// </remarks>
    [Fact]
    public void DeclaredInterfaces_CarryTheContractIds()
    {
        Assert.Equal(
            new Guid(BruinetUiaIds.ProviderInterfaceIid), typeof(IBruinetAutomationProvider).GUID);
        Assert.Equal(
            new Guid(BruinetUiaIds.ClientInterfaceIid), typeof(IBruinetAutomationPattern).GUID);
    }

    /// <summary>
    /// The method table has exactly two entries, in this order.
    /// </summary>
    /// <remarks>
    /// UI Automation dispatches by index. Inserting a method renumbers every method after it,
    /// so a client built before the insertion calls the wrong one on a provider built after -
    /// with arguments that may even unmarshal cleanly.
    /// </remarks>
    [Fact]
    public void MethodTable_IsTwoMethodsInThisOrder()
    {
        var methods = typeof(IBruinetAutomationProvider).GetMethods();

        Assert.Equal(2, methods.Length);
        Assert.Equal(nameof(IBruinetAutomationProvider.Invoke), methods[0].Name);
        Assert.Equal(nameof(IBruinetAutomationProvider.Exchange), methods[1].Name);
    }

    /// <summary>The client interface mirrors the provider interface method for method.</summary>
    [Fact]
    public void ClientInterface_MirrorsTheProviderInterface()
    {
        var provider = typeof(IBruinetAutomationProvider).GetMethods();
        var client = typeof(IBruinetAutomationPattern).GetMethods();

        Assert.Equal(provider.Length, client.Length);

        for (var i = 0; i < provider.Length; i++)
        {
            Assert.Equal(provider[i].Name, client[i].Name);
            Assert.Equal(
                provider[i].GetParameters().Select(p => p.ParameterType.Name),
                client[i].GetParameters().Select(p => p.ParameterType.Name));
        }
    }

    /// <summary>
    /// Verb numbers that have shipped keep their numbers.
    /// </summary>
    /// <remarks>
    /// Spot-checks the first value of each range rather than every verb: the risk being guarded
    /// against is a range being moved wholesale, which is what an "organise the enum" change
    /// does. An individual verb cannot move without moving its range or colliding inside it,
    /// and <see cref="VerbNumbers_AreUnique"/> catches the collision.
    /// </remarks>
    [Theory]
    [InlineData(BruinetVerb.GetCapabilities, 1)]
    [InlineData(BruinetVerb.Tap, 100)]
    [InlineData(BruinetVerb.SwipeLeft, 103)]
    [InlineData(BruinetVerb.Focus, 200)]
    [InlineData(BruinetVerb.SetText, 300)]
    [InlineData(BruinetVerb.ScrollTo, 400)]
    [InlineData(BruinetVerb.NavigateBack, 500)]
    [InlineData(BruinetVerb.GetState, 600)]
    [InlineData(BruinetVerb.SetDate, 700)]
    [InlineData(BruinetVerb.SelectIndex, 800)]
    [InlineData(BruinetVerb.CurrentAlert, 900)]
    public void Verb_HasItsWireValue(BruinetVerb verb, int expected)
    {
        Assert.Equal(expected, (int)verb);
    }

    /// <summary>No two verbs share a number, which would make one of them unreachable.</summary>
    [Fact]
    public void VerbNumbers_AreUnique()
    {
        var values = Enum.GetValues<BruinetVerb>().Select(v => (int)v).ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    /// <summary>Every verb sits inside a declared range, so range-based negotiation is total.</summary>
    [Fact]
    public void EveryVerb_BelongsToARange()
    {
        foreach (var verb in Enum.GetValues<BruinetVerb>())
        {
            if (verb == BruinetVerb.None)
            {
                continue;
            }

            Assert.True(
                BruinetVerbs.RangeOf((int)verb) != BruinetVerbRange.Unknown,
                $"{verb} = {(int)verb} is outside every declared range. Ranges are how a client "
                + "asks an older provider whether it supports a capability area at all.");
        }
    }

    /// <summary>
    /// Each verb travels on the method it has always travelled on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As frozen as the numbers, and pinned here for the same reason. A verb sent on the wrong
    /// one of the contract's two methods reaches a provider that has never heard of it and comes
    /// back <c>UIA_E_NOTSUPPORTED</c> - which reads as "this element does not support that", a
    /// plausible answer pointing at the app rather than at the call.
    /// </para>
    /// <para>
    /// That is not hypothetical: <c>Submit</c> was sent on <c>Invoke</c> during step 14, was
    /// refused, and fell silently back to a real Enter keystroke. The test that caught it was
    /// looking for physical input, not for a routing mistake.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(BruinetVerb.Ping, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.GetCapabilities, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.Tap, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.SwipeRight, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.Focus, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.Unfocus, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.IsFocused, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.SetText, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.AppendText, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.ClearText, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.GetText, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.Submit, BruinetVerbTransport.Exchange)]
    [InlineData(BruinetVerb.NavigateBack, BruinetVerbTransport.Invoke)]
    [InlineData(BruinetVerb.GetState, BruinetVerbTransport.Exchange)]
    public void Verb_TravelsOnItsOwnMethod(BruinetVerb verb, BruinetVerbTransport expected)
    {
        Assert.Equal(expected, BruinetVerbs.TransportFor(verb));
    }

    /// <summary>
    /// Every verb has a transport, including the ones no build implements yet.
    /// </summary>
    /// <remarks>
    /// The map falls through to <see cref="BruinetVerbTransport.Invoke"/>, so this cannot fail
    /// as written - which is the point of stating it. It is here so that a future verb carrying
    /// a string is a visible decision in the map rather than an accidental default, and so the
    /// list below is a checklist a reader can work through.
    /// </remarks>
    [Fact]
    public void EveryVerb_HasATransport()
    {
        foreach (var verb in Enum.GetValues<BruinetVerb>())
        {
            if (verb == BruinetVerb.None)
            {
                continue;
            }

            Assert.True(
                Enum.IsDefined(BruinetVerbs.TransportFor(verb)),
                $"{verb} has no transport.");
        }
    }

    /// <summary>
    /// Every verb is classified as a read, an act on its element, or an act on the app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A checklist that fails when the vocabulary grows.</b> <c>KindOf</c> falls through to
    /// <c>ElementAction</c>, so a fall-through test could never fail; this compares the whole enum
    /// against lists written out by hand, so adding a verb without deciding what it is breaks here
    /// rather than quietly changing an accessibility report.
    /// </para>
    /// <para>
    /// The decision matters because it decides what step 29's audit calls a defect. Getting a
    /// read wrong puts a label on an accessibility backlog; getting an element action wrong takes
    /// a gesture-only control off it; getting an app action wrong fills the backlog with pages,
    /// which is what the first audit actually did.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryVerb_IsClassifiedByKind()
    {
        // Reads: answer a question, change nothing.
        BruinetVerb[] reads =
        [
            BruinetVerb.None,
            BruinetVerb.GetCapabilities,
            BruinetVerb.GetProtocolVersion,
            BruinetVerb.GetBounds,
            BruinetVerb.GetLastFailure,
            BruinetVerb.Ping,
            BruinetVerb.IsFocused,
            BruinetVerb.GetText,
            BruinetVerb.ScrollPosition,
            BruinetVerb.CurrentRoute,
            BruinetVerb.GetState,
            BruinetVerb.IsIdle,
            BruinetVerb.CurrentAlert,
        ];

        // App actions: real, but not done to the element carrying the verb.
        BruinetVerb[] appActions =
        [
            BruinetVerb.NavigateBack,
            BruinetVerb.NavigateTo,
            BruinetVerb.InvokeMenuItem,
            BruinetVerb.InvokeToolbarItem,
            BruinetVerb.DismissAlert,
            BruinetVerb.OpenFlyout,
            BruinetVerb.CloseFlyout,
        ];

        // Element actions: done to the element itself. The only kind the audit examines.
        BruinetVerb[] elementActions =
        [
            BruinetVerb.Tap, BruinetVerb.DoubleTap, BruinetVerb.LongPress,
            BruinetVerb.SwipeLeft, BruinetVerb.SwipeRight, BruinetVerb.SwipeUp,
            BruinetVerb.SwipeDown, BruinetVerb.Pan, BruinetVerb.Pinch,
            BruinetVerb.Focus, BruinetVerb.Unfocus,
            BruinetVerb.SetText, BruinetVerb.AppendText, BruinetVerb.ClearText,
            BruinetVerb.Submit,
            BruinetVerb.ScrollTo, BruinetVerb.ScrollToIndex,
            BruinetVerb.SetDate, BruinetVerb.SetTime,
            BruinetVerb.SelectIndex, BruinetVerb.SelectByText,
        ];

        var classified = reads.Concat(appActions).Concat(elementActions).ToHashSet();
        var missing = Enum.GetValues<BruinetVerb>().Where(verb => !classified.Contains(verb));

        Assert.True(
            !missing.Any(),
            "These verbs are in the enum but in none of this test's lists, so nobody has decided "
            + "what kind they are: " + string.Join(", ", missing) + ". KindOf will call them "
            + "element actions by default, which puts whatever declares them on the "
            + "accessibility backlog until someone says otherwise.");

        AssertKind(reads, BruinetVerbKind.Read);
        AssertKind(appActions, BruinetVerbKind.AppAction);
        AssertKind(elementActions, BruinetVerbKind.ElementAction);
    }

    private static void AssertKind(IEnumerable<BruinetVerb> verbs, BruinetVerbKind expected)
    {
        foreach (var verb in verbs)
        {
            Assert.True(
                BruinetVerbs.KindOf(verb) == expected,
                $"{verb} is a {BruinetVerbs.KindOf(verb)}, expected {expected}.");
        }
    }

    /// <summary>A verb number from a future build reports its range rather than failing.</summary>
    [Fact]
    public void RangeOf_ClassifiesVerbsThisBuildDoesNotKnow()
    {
        Assert.Equal(BruinetVerbRange.Text, BruinetVerbs.RangeOf(398));
        Assert.Equal(BruinetVerbRange.Gestures, BruinetVerbs.RangeOf(150));
        Assert.Equal(BruinetVerbRange.Unknown, BruinetVerbs.RangeOf(0));
        Assert.Equal(BruinetVerbRange.Unknown, BruinetVerbs.RangeOf(5000));
        Assert.Equal(BruinetVerbRange.Unknown, BruinetVerbs.RangeOf(-1));
    }

    /// <summary>Capabilities survive being written and read back.</summary>
    [Fact]
    public void Capabilities_RoundTrip()
    {
        BruinetVerb[] verbs = [BruinetVerb.SwipeLeft, BruinetVerb.Tap, BruinetVerb.SetText];

        var formatted = BruinetVerbs.FormatCapabilities(verbs);

        Assert.Equal("100,103,300", formatted);
        Assert.Equal(
            [BruinetVerb.Tap, BruinetVerb.SwipeLeft, BruinetVerb.SetText],
            BruinetVerbs.ParseCapabilities(formatted));
    }

    /// <summary>
    /// A capability list from a newer provider is read, not rejected.
    /// </summary>
    /// <remarks>
    /// The asymmetric half of the versioning story. A test assembly older than the app under
    /// test will be told about verbs it has no name for; dropping them and keeping the rest is
    /// what lets an old suite run against a new app.
    /// </remarks>
    [Fact]
    public void Capabilities_IgnoreEntriesThisBuildCannotName()
    {
        var verbs = BruinetVerbs.ParseCapabilities("100,4242,300,not-a-number,");

        Assert.Equal([BruinetVerb.Tap, BruinetVerb.SetText], verbs);
    }

    /// <summary>An empty or absent capability list is a list of nothing, not a failure.</summary>
    [Fact]
    public void Capabilities_HandleNothing()
    {
        Assert.Empty(BruinetVerbs.ParseCapabilities(null));
        Assert.Empty(BruinetVerbs.ParseCapabilities(string.Empty));
        Assert.Empty(BruinetVerbs.ParseCapabilities("   "));
        Assert.Equal(string.Empty, BruinetVerbs.FormatCapabilities([]));
    }

    /// <summary>
    /// Registration is idempotent, and safe to race.
    /// </summary>
    /// <remarks>
    /// The app under test may attach a bridge from more than one window, and the test assembly
    /// resolves the pattern from whatever thread the test runs on. Two threads arriving at once
    /// must produce one registration, not two rooted handlers and two ids.
    /// </remarks>
    [Fact]
    public void Registration_IsIdempotentUnderParallelThreads()
    {
        var results = new BruinetPatternRegistration[32];

        Parallel.For(0, results.Length, i => results[i] = BruinetPatternRegistration.Current);

        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.All(results, r => Assert.Equal(results[0].PatternId, r.PatternId));
        Assert.True(results[0].PatternId > 0);
    }

    /// <summary>
    /// The pattern and its availability property get different ids.
    /// </summary>
    /// <remarks>
    /// They are allocated from the same sequence, so confusing one for the other yields a
    /// plausible-looking number that addresses the wrong thing.
    /// </remarks>
    [Fact]
    public void Registration_ProducesDistinctPatternAndPropertyIds()
    {
        var registration = BruinetPatternRegistration.Current;

        Assert.NotEqual(registration.PatternId, registration.IsPatternAvailablePropertyId);
    }

    /// <summary>Failure descriptions name the verb and say whether retrying is worthwhile.</summary>
    [Fact]
    public void Describe_SaysWhatToDoAboutIt()
    {
        var notSupported = BruinetVerbFailure.Describe(
            BruinetVerb.SwipeUp, HResults.UIA_E_NOTSUPPORTED);
        var gone = BruinetVerbFailure.Describe(
            BruinetVerb.Tap, HResults.UIA_E_ELEMENTNOTAVAILABLE);

        Assert.Contains("SwipeUp", notSupported, StringComparison.Ordinal);
        Assert.Contains("not succeed on retry", notSupported, StringComparison.Ordinal);

        Assert.Contains("Tap", gone, StringComparison.Ordinal);
        Assert.Contains("retry", gone, StringComparison.Ordinal);
    }

    /// <summary>Success and failure are told apart by sign, as COM defines it.</summary>
    [Fact]
    public void Succeeded_FollowsTheComConvention()
    {
        Assert.True(BruinetVerbFailure.Succeeded(HResults.S_OK));
        Assert.True(BruinetVerbFailure.Succeeded(HResults.S_FALSE));
        Assert.False(BruinetVerbFailure.Succeeded(HResults.UIA_E_NOTSUPPORTED));
        Assert.False(BruinetVerbFailure.Succeeded(HResults.E_FAIL));
    }
}
