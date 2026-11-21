namespace Bruinet.Api.Contracts.Tests;

// A saved form against what the service stored: the field map from the sent body, and typed, sent, stored per field.
public sealed class FormCheckTests
{
    private static readonly Dictionary<string, string> Typed = new()
    {
        ["Name"] = "BRN-1121-0930 Arend",
        ["Amount"] = "12,50",
        ["Start"] = "21-11-2026",
        ["Note"] = "only on the screen",
        ["City"] = "Delft",
    };

    private const string Sent = """
        {"id":null,"name":"BRN-1121-0930 Arend","lines":[{"amount":12.5}],"start":"2026-11-21T00:00:00","address":{"city":"Delft"},"type":0}
        """;

    [Fact]
    public void Map_FindsEachTypedValueInTheSentBody_AndSaysWhichHasNoPlace()
    {
        var places = FormCheck.Map(Typed, Sent);

        Assert.Equal(
            ["Name=name", "Amount=lines.0.amount", "Start=start", "Note=", "City=address.city"],
            places.Select(place => $"{place.Field}={place.Path}"));
    }

    [Fact]
    public void Compare_PassesWhenTheStoredRecordHoldsWhatWasTyped_WithAWarningForAFieldNeverSent()
    {
        var stored = """{"id":7,"name":"BRN-1121-0930 Arend","lines":[{"amount":12.50}],"start":"2026-11-21","address":{"city":"Delft"}}""";

        var result = FormCheck.Compare("Contact", FormCheck.Map(Typed, Sent), Sent, stored);

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Equal(4, result.ItemCount);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("Note", warning.Path);
    }

    [Fact]
    public void Compare_FailsOnARoundedAmount_ADayOff_AndAFieldTheServiceDropped()
    {
        var stored = """{"id":7,"name":"BRN-1121-0930 Arend","lines":[{"amount":12}],"start":"2026-11-20T23:00:00Z"}""";

        var result = FormCheck.Compare("Contact", FormCheck.Map(Typed, Sent), Sent, stored);

        Assert.False(result.Passed);
        Assert.Equal(
            [
                "$.address.city|City: typed 'Delft', sent \"Delft\", but the stored record has no such field.",
                "$.lines.0.amount|Amount: typed '12,50', sent 12.5, stored 12.",
                "$.start|Start: typed '21-11-2026', sent \"2026-11-21T00:00:00\", stored \"2026-11-20T23:00:00Z\".",
            ],
            result.Failures.Select(issue => $"{issue.Path}|{issue.Message}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AMapKeptBesideAPageObject_IsUsedWithTheValuesTypedNow()
    {
        var kept = FormCheck.ToJson(FormCheck.Map(Typed, Sent));

        var places = FormCheck.FromJson(kept, new Dictionary<string, string> { ["Name"] = "BRN-1122-0800 Bas", ["New"] = "x" });

        Assert.Equal(["Name=name", "New="], places.Select(place => $"{place.Field}={place.Path}"));
    }

    [Fact]
    public void WithNoRecordReadBack_TheCheckIsBlocked()
    {
        var result = FormCheck.Compare("Contact", FormCheck.Map(Typed, Sent), Sent, null);

        Assert.False(result.Passed);
        Assert.Single(result.Blocked);
    }
}
