namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class RoundTripCheckTests
{
    private static readonly string[] Fields = ["name", "zip_code", "contact_type_id", "email"];

    [Fact]
    public void SameValues_Pass()
    {
        var result = RoundTripCheck.Compare("rt",
            """{"name":"A","zip_code":"1234 AB","contact_type_id":3,"email":""}""",
            """{"id":9,"name":"A","zip_code":"1234 AB","contact_type_id":3,"email":null}""",
            Fields);

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Empty(result.Issues);
        Assert.Equal(4, result.ItemCount);
    }

    [Fact]
    public void ChangedValue_Fails_WithBothValues()
    {
        var result = RoundTripCheck.Compare("rt",
            """{"name":"A","zip_code":"1234 AB"}""",
            """{"name":"A","zip_code":"1234AB"}""",
            Fields);

        var failure = Assert.Single(result.Failures);
        Assert.Equal("$.zip_code", failure.Path);
        Assert.Equal("Sent \"1234 AB\", read back \"1234AB\".", failure.Message);
    }

    [Fact]
    public void MissingField_Fails_UnlessNothingWasSent()
    {
        var result = RoundTripCheck.Compare("rt",
            """{"name":"A","email":""}""",
            """{"id":9}""",
            Fields);

        var failure = Assert.Single(result.Failures);
        Assert.Equal("$.name", failure.Path);
    }

    [Fact]
    public void NumberReadBackAsString_IsWarning()
    {
        var result = RoundTripCheck.Compare("rt", """{"contact_type_id":3}""", """{"contact_type_id":"3"}""", Fields);

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("12.5", "12.50", RoundTripCheck.ValueMatch.Equal)]
    [InlineData("12.5", "12.4999", RoundTripCheck.ValueMatch.Different)]
    [InlineData("12.5", "12", RoundTripCheck.ValueMatch.Different)]
    [InlineData("12.5", "\"12.5\"", RoundTripCheck.ValueMatch.SameValueOtherKind)]
    public void Decimals_CompareByValue(string sent, string read, RoundTripCheck.ValueMatch expected)
    {
        using var sentJson = System.Text.Json.JsonDocument.Parse(sent);
        using var readJson = System.Text.Json.JsonDocument.Parse(read);

        Assert.Equal(expected, RoundTripCheck.Match(sentJson.RootElement, readJson.RootElement));
    }

    [Fact]
    public void MissingBodies_AreBlocked()
    {
        Assert.Single(RoundTripCheck.Compare("rt", null, "{}", Fields).Blocked);
        Assert.Single(RoundTripCheck.Compare("rt", "{}", null, Fields).Blocked);
    }

    [Fact]
    public void MappedFields_CompareNestedSentIdsWithFlatReadIds()
    {
        var result = RoundTripCheck.Compare("rt",
            """{"name":"A","contact":{"id":7},"employees":[{"id":1},{"id":2}]}""",
            """{"name":"A","contact_id":7,"employee_ids":[2,1]}""",
            [new RoundTripField("name", "name"),
             new RoundTripField("contact.id", "contact_id"),
             new RoundTripField("employees", "employee_ids", RoundTripKind.IdList)]);

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Equal(3, result.ItemCount);
    }

    [Theory]
    [InlineData("""[1,2,3]""", true, ContractSeverity.Warning)]
    [InlineData("""[2]""", true, null)]
    [InlineData("""[1]""", false, ContractSeverity.Failure)]
    public void IdsIncluded_AllowsIdsTheServiceAdds(string readIds, bool passes, ContractSeverity? severity)
    {
        var result = RoundTripCheck.Compare("Round trip", """{"employees":[{"id":2}]}""", $$"""{"employee_ids":{{readIds}}}""",
            [new RoundTripField("employees", "employee_ids", RoundTripKind.IdsIncluded)]);

        Assert.Equal(passes, result.Passed);
        if (severity is null)
            Assert.Empty(result.Issues);
        else
            Assert.Contains(result.Issues, issue => issue.Severity == severity);
    }

    [Fact]
    public void CalendarDay_FailsWhenTheServiceShiftsTheDay()
    {
        var result = RoundTripCheck.Compare("rt",
            """{"startDate":"2026-03-10T00:00:00Z","endDate":"2026-03-12T00:00:00Z"}""",
            """{"start_date":"2026-03-10","end_date":"2026-03-11 23:00:00"}""",
            [new RoundTripField("startDate", "start_date", RoundTripKind.CalendarDay),
             new RoundTripField("endDate", "end_date", RoundTripKind.CalendarDay)]);

        var failure = Assert.Single(result.Failures);
        Assert.Equal("$.end_date", failure.Path);
        Assert.Contains("2026-03-12", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1773100800", "\"2026-03-10T00:00:00Z\"", true)]
    [InlineData("\"2026-03-10T01:00:00+01:00\"", "1773100800", true)]
    [InlineData("\"1773100800\"", "\"2026-03-10 00:00:00\"", true)]
    [InlineData("1773100800", "\"2026-03-10T00:00:01Z\"", false)]
    [InlineData("1773100800", "\"tomorrow\"", false)]
    public void SameMoment_ComparesUnixSecondsWithIsoText(string sent, string read, bool passes)
    {
        var result = RoundTripCheck.Compare("rt", $$"""{"at":{{sent}}}""", $$"""{"at":{{read}}}""",
            [RoundTripField.Moment("at")]);

        Assert.Equal(passes, result.Passed);
        if (passes)
            Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("12.5", "\"12.50\"", true)]
    [InlineData("\"80\"", "80", true)]
    [InlineData("12.5", "\"12.4\"", false)]
    [InlineData("12.5", "\"many\"", false)]
    [InlineData("null", "\"\"", true)]
    public void Number_AcceptsNumbersSentAsStrings_WithoutWarning(string sent, string read, bool passes)
    {
        var result = RoundTripCheck.Compare("rt", $$"""{"hours":{{sent}}}""", $$"""{"hours_estimate":{{read}}}""",
            [RoundTripField.Number("hours", "hours_estimate")]);

        Assert.Equal(passes, result.Passed);
        if (passes)
            Assert.Empty(result.Issues);
    }

    [Fact]
    public void Factories_DefaultTheReadPathToTheSentPath()
    {
        Assert.Equal(new RoundTripField("name", "name"), RoundTripField.Same("name"));
        Assert.Equal(new RoundTripField("a", "b", RoundTripKind.CalendarDay), RoundTripField.Day("a", "b"));
        Assert.Equal(RoundTripKind.IdsIncluded, RoundTripField.IdsKept("employees", "employee_ids").Kind);
    }

    [Fact]
    public void FormBody_ReadsAsText_AndMatchesNumbersReadBack()
    {
        var result = RoundTripCheck.Compare("rt",
            "ticket_number=ZZ%201&contact_id=101&cost=12.5&ticket_date=2026-11-07",
            """{"ticket_number":"ZZ 1","contact_id":101,"cost":12.50,"ticket_date":"2026-11-07 00:00:00"}""",
            [RoundTripField.Same("ticket_number"), RoundTripField.Same("contact_id"), RoundTripField.Number("cost"), RoundTripField.Day("ticket_date")]);

        Assert.Empty(result.Issues);
        Assert.Equal(4, result.ItemCount);
    }

    [Fact]
    public void FormBody_ChangedValue_Fails()
    {
        var result = RoundTripCheck.Compare("rt", "contact_id=101", """{"contact_id":102}""", ["contact_id"]);

        Assert.Single(result.Failures);
    }
}
