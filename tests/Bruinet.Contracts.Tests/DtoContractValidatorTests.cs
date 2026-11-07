using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class DtoContractValidatorTests
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly DtoContract Contract = new("GET sample", typeof(SampleDto[]))
    {
        Required = new HashSet<string>(["$[*].id", "$[*].name"], StringComparer.Ordinal),
        CodedValues = new Dictionary<string, Type>(StringComparer.Ordinal) { ["$[*].code"] = typeof(SampleKind) }
    };

    [Fact]
    public void ValidBody_Passes_WithoutIssues()
    {
        var result = Validate("""[{"id":1,"name":"A","kind":1,"code":2,"note":null,"child":{"value":"x"},"tags":["t"]}]""");

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.ItemCount);
    }

    [Fact]
    public void MissingRequiredField_Fails()
    {
        var result = Validate("""[{"name":"A","kind":1,"code":2,"note":null,"child":null,"tags":[]}]""");

        var failure = Assert.Single(result.Failures);
        Assert.Equal("$[*].id", failure.Path);
        Assert.Equal("Required field is missing.", failure.Message);
    }

    [Fact]
    public void RequiredFieldNull_Fails()
    {
        var result = Validate("""[{"id":1,"name":null,"kind":1,"code":2,"note":null,"child":null,"tags":[]}]""");

        Assert.Contains(result.Failures, issue => issue.Path == "$[*].name" && issue.Message == "Required field is null.");
    }

    [Fact]
    public void WrongJsonKind_Fails_AtTheFieldPath()
    {
        var result = Validate("""[{"id":1,"name":42,"kind":1,"code":2,"note":null,"child":null,"tags":[]}]""");

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, issue => issue.Path == "$[*].name" && issue.Message == "Expected a string, got a number.");
    }

    [Fact]
    public void UnknownEnumValue_Fails_EvenThoughDeserializationAcceptsIt()
    {
        var result = Validate("""[{"id":1,"name":"A","kind":9,"code":2,"note":null,"child":null,"tags":[]}]""");

        var failure = Assert.Single(result.Failures);
        Assert.Equal("$[*].kind", failure.Path);
        Assert.Equal("Unknown SampleKind value 9.", failure.Message);
    }

    [Fact]
    public void UnknownCodedIntegerValue_Fails()
    {
        var result = Validate("""[{"id":1,"name":"A","kind":1,"code":7,"note":null,"child":null,"tags":[]}]""");

        Assert.Contains(result.Failures, issue => issue.Path == "$[*].code" && issue.Message == "Unknown SampleKind value 7.");
    }

    [Fact]
    public void ExtraField_And_MissingOptionalField_AreWarnings()
    {
        var result = Validate("""[{"id":1,"name":"A","kind":1,"code":2,"child":null,"tags":[],"extra":true}]""");

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Contains(result.Warnings, issue => issue.Path == "$[*].extra");
        Assert.Contains(result.Warnings, issue => issue.Path == "$[*].note" && issue.Message == "DTO field is not in the response.");
    }

    [Fact]
    public void NullOnNonNullableOptionalField_IsWarning()
    {
        var result = Validate("""[{"id":1,"name":"A","kind":1,"code":2,"note":null,"child":{"value":null},"tags":[]}]""");

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Contains(result.Warnings, issue => issue.Path == "$[*].child.value"
            && issue.Message == "Field is null, but the DTO property is non-nullable.");
    }

    [Fact]
    public void NumberAsString_IsWarning_BecauseTheAppAcceptsIt()
    {
        var result = Validate("""[{"id":"1","name":"A","kind":1,"code":2,"note":null,"child":null,"tags":[]}]""");

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Contains(result.Warnings, issue => issue.Path == "$[*].id" && issue.Message == "Number arrives as a string.");
    }

    [Fact]
    public void BodyTheDtoCannotRead_Fails()
    {
        var result = Validate("""{"id":1}""");

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, issue => issue.Message.StartsWith("The app's DTO cannot read the body", StringComparison.Ordinal));
        Assert.Contains(result.Failures, issue => issue.Message == "Expected an array, got an object.");
    }

    [Fact]
    public void RepeatedIssues_AreGroupedPerPath()
    {
        var result = Validate("""
            [{"id":1,"name":"A","kind":9,"code":2,"note":null,"child":null,"tags":[]},
             {"id":2,"name":"B","kind":9,"code":2,"note":null,"child":null,"tags":[]}]
            """);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(2, failure.Count);
        Assert.Equal("$[0].kind", failure.FirstLocation);
        Assert.Equal(2, result.ItemCount);
    }

    [Fact]
    public void EmptyBody_Fails()
    {
        Assert.False(Validate(" ").Passed);
    }

    [Theory]
    [InlineData("12.5")]
    [InlineData("0")]
    [InlineData("-3.125")]
    public void DecimalNumber_Passes(string amount)
    {
        var result = ValidateMeasure($$"""{"amount":{{amount}},"ratio":0.5,"at":"2026-10-26T11:48:21Z","day":"2026-10-26"}""");

        Assert.True(result.Passed, result.ToMarkdown());
    }

    [Theory]
    [InlineData("\"12.5\"", "Decimal arrives as the string \"12.5\"; it must be a JSON number.")]
    [InlineData("\"12,5\"", "Number \"12,5\" uses a comma as the decimal separator.")]
    [InlineData("\"abc\"", "Expected a number, got \"abc\".")]
    [InlineData("true", "Expected a number, got a boolean.")]
    public void DecimalAsString_Fails(string amount, string message)
    {
        var result = ValidateMeasure($$"""{"amount":{{amount}},"ratio":0.5,"at":"2026-10-26T11:48:21Z","day":"2026-10-26"}""");

        Assert.Contains(result.Failures, issue => issue.Path == "$.amount" && issue.Message == message);
    }

    [Theory]
    [InlineData("\"12.5\"", ContractSeverity.Warning, "the DTO property declares string reading")]
    [InlineData("\"0.1\"", ContractSeverity.Failure, "loses precision as Single")]
    public void DecimalAsString_WithDeclaredStringReading_IsAcceptedOnlyWithoutPrecisionLoss(string hours, ContractSeverity severity, string message)
    {
        var result = DtoContractValidator.Validate($$"""{"hours":{{hours}}}""", new DtoContract("GET hours", typeof(HoursDto)), Options);

        Assert.Contains(result.Issues, issue => issue.Path == "$.hours" && issue.Severity == severity && issue.Message.Contains(message, StringComparison.Ordinal));
    }

    public sealed class HoursDto
    {
        [JsonPropertyName("hours")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public float Hours { get; set; }
    }

    [Trait("Pyramid", "Unit")]
    [Fact]
    public void DoubleThatLosesPrecision_Fails()
    {
        var result = ValidateMeasure("""{"amount":1,"ratio":0.12345678901234567890123,"at":"2026-10-26T11:48:21Z","day":"2026-10-26"}""");

        Assert.Contains(result.Failures, issue => issue.Path == "$.ratio" && issue.Message.Contains("loses precision", StringComparison.Ordinal));
    }

    [Trait("Pyramid", "Unit")]
    [Fact]
    public void FractionInWholeNumberField_Fails()
    {
        var result = Validate("""[{"id":1.5,"name":"A","kind":1,"code":2,"note":null,"child":null,"tags":[]}]""");

        Assert.Contains(result.Failures, issue => issue.Path == "$[*].id" && issue.Message == "Expected a whole number, got 1.5.");
    }

    [Trait("Pyramid", "Unit")]
    [Theory]
    [InlineData("2026-10-26T11:48:21Z")]
    [InlineData("2026-10-26T11:48:21.559+01:00")]
    [InlineData("2026-10-26T11:48:21.1234567-05:00")]
    public void DateTimeWithOffset_Passes(string at)
    {
        var result = ValidateMeasure($$"""{"amount":1,"ratio":0.5,"at":"{{at}}","day":"2026-10-26"}""");

        Assert.True(result.Passed, result.ToMarkdown());
    }

    [Trait("Pyramid", "Unit")]
    [Theory]
    [InlineData("2026-10-26T11:48:21", "Date-time \"2026-10-26T11:48:21\" has no time zone offset, so the moment is ambiguous.")]
    [InlineData("2026-10-26", "Expected a date-time, got the date-only value \"2026-10-26\".")]
    [InlineData("26-10-2026 11:48", "Expected an ISO 8601 date-time string, got \"26-10-2026 11:48\".")]
    public void DateTimeWithoutOffsetOrMalformed_Fails(string at, string message)
    {
        var result = ValidateMeasure($$"""{"amount":1,"ratio":0.5,"at":"{{at}}","day":"2026-10-26"}""");

        Assert.Contains(result.Failures, issue => issue.Path == "$.at" && issue.Message == message);
    }

    [Trait("Pyramid", "Unit")]
    [Theory]
    [InlineData("2026-10-26T00:00:00Z")]
    [InlineData("26-10-2026")]
    [InlineData("2026-2-3")]
    public void DateOnlyInAnyOtherFormat_Fails(string day)
    {
        var result = ValidateMeasure($$"""{"amount":1,"ratio":0.5,"at":"2026-10-26T11:48:21Z","day":"{{day}}"}""");

        Assert.Contains(result.Failures, issue => issue.Path == "$.day" && issue.Message == $"Expected a date as yyyy-MM-dd, got \"{day}\".");
    }

    private static ContractResult Validate(string json) => DtoContractValidator.Validate(json, Contract, Options);

    private static ContractResult ValidateMeasure(string json)
        => DtoContractValidator.Validate(json, new DtoContract("GET measure", typeof(MeasureDto)), Options);

    public sealed class MeasureDto
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("ratio")]
        public double Ratio { get; set; }

        [JsonPropertyName("at")]
        public DateTimeOffset At { get; set; }

        [JsonPropertyName("day")]
        public DateOnly Day { get; set; }
    }

    public enum SampleKind
    {
        First = 1,
        Second = 2
    }

    public sealed class SampleDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public SampleKind Kind { get; set; }

        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("child")]
        public SampleChild? Child { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    public sealed class SampleChild
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
