namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class ExchangeContractCheckTests
{
    private const string RemoteItem = "https://api.acceptance.example/item/101";
    private const string ValidItem = """{"id":101,"name":"Alpha","kind":1}""";

    private static readonly DtoContract ItemList = new("GET item", typeof(ItemDto[]));
    private static readonly DtoContract ItemDetail = new("GET item/{id}", typeof(ItemDto))
    {
        Required = new HashSet<string>(["$.id", "$.name"], StringComparer.Ordinal)
    };

    private static readonly ContractEvidence Acceptance = new("Acceptance")
    {
        Never = new HashSet<string>(["Production"], StringComparer.OrdinalIgnoreCase)
    };

    [Theory]
    [InlineData("GET contact", "/contact", true)]
    [InlineData("GET contact", "/contact/", true)]
    [InlineData("GET contact", "/contact/101", false)]
    [InlineData("GET contact/{id}", "/contact/101", true)]
    [InlineData("GET contact/{id}", "/contact/101/notes", false)]
    [InlineData("GET contact", "/v2/contact", true)]
    [InlineData("GET contact/{id}", "/v2/contact/55313", true)]
    [InlineData("GET contact", "/v2/contact-type", false)]
    [InlineData("GET contact", "/v2/list/contact", false)]
    [InlineData("GET contact-type", "/contact-type", true)]
    [InlineData("GET list/custom-attributes", "/list/custom-attributes", true)]
    [InlineData("GET project/{id}", "/project/project-status", false)]
    [InlineData("PATCH project/project-status/{id}", "/project/project-status/201", true)]
    [InlineData("GET contact/{name}", "/contact/abc", true)]
    public void MatchesPath_UsesTemplateSegments(string endpoint, string path, bool expected)
        => Assert.Equal(expected, new DtoContract(endpoint, typeof(object)).MatchesPath(path));

    [Fact]
    public void Newest_PicksLatestMatchingExchangeSinceStart()
    {
        var start = DateTimeOffset.UtcNow;
        var exchanges = new[]
        {
            Exchange("https://h/item/1", start.AddMinutes(-5)),
            Exchange("https://h/item/2?x=1", start.AddMinutes(1)),
            Exchange("https://h/item", start.AddMinutes(2)),
            Exchange("https://h/item/3", start.AddMinutes(3), method: "PUT")
        };

        Assert.Equal("https://h/item/2?x=1", ContractExchanges.Newest(exchanges, ItemDetail, start)?.Path);
    }

    [Fact]
    public void Newest_PrefersPageWithDataOverNewerEmptyPage()
    {
        var start = DateTimeOffset.UtcNow;
        var exchanges = new[]
        {
            Exchange("https://h/item?page=1", start.AddSeconds(1)) with { ResponseBody = """[{"id":1,"name":"A","kind":1}]""" },
            Exchange("https://h/item?page=2", start.AddSeconds(2)) with { ResponseBody = "[]" }
        };

        Assert.Equal("https://h/item?page=1", ContractExchanges.Newest(exchanges, ItemList, start)?.Path);
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("""{"items":[],"count":0}""", false)]
    [InlineData("""[{"id":1}]""", true)]
    [InlineData("""{"items":[{"id":1}]}""", true)]
    [InlineData("""{"id":1}""", true)]
    [InlineData("not json", true)]
    public void HasData_DetectsEmptyPages(string body, bool expected)
        => Assert.Equal(expected, ContractExchanges.HasData(body));

    [Fact]
    public void Check_ValidRemoteExchange_Passes()
        => Assert.True(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow)).Passed);

    [Fact]
    public void Check_NotObserved_IsBlocked()
        => AssertSingle(Run(null), ContractSeverity.Blocked, "did not call");

    [Fact]
    public void Check_LocalMock_IsBlocked()
        => AssertSingle(Run(Exchange("http://127.0.0.1:9090/item/101", DateTimeOffset.UtcNow)), ContractSeverity.Blocked, "local mock");

    [Fact]
    public void Check_NeverEvidenceEnvironment_IsBlocked()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { Environment = "Production" }), ContractSeverity.Blocked, "Production");

    [Fact]
    public void Check_RunTargetingNeverEvidenceEnvironment_IsBlocked()
    {
        var result = ExchangeContractCheck.Check(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { Environment = "Production" },
            ItemDetail, DtoContractValidatorTests.Options, Acceptance with { Environment = "Production" });

        AssertSingle(result, ContractSeverity.Blocked, "never used");
    }

    [Fact]
    public void Check_OtherEnvironment_IsBlocked()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { Environment = "Test" }), ContractSeverity.Blocked, "not Acceptance");

    [Fact]
    public void Check_ServerError_Fails()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { StatusCode = 500 }), ContractSeverity.Failure, "500");

    [Fact]
    public void Check_TransportError_Fails()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { Error = "HttpRequestException" }),
            ContractSeverity.Failure, "HttpRequestException");

    [Fact]
    public void Check_NonJson_Fails()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { ContentType = "text/html" }), ContractSeverity.Failure, "text/html");

    [Fact]
    public void Check_TruncatedBody_IsBlocked()
        => AssertSingle(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { IsTruncated = true }), ContractSeverity.Blocked, "truncated");

    [Fact]
    public void Check_TruncatedArray_ChecksTheCompleteLeadingItems()
    {
        var body = """[{"id":1,"name":"A","kind":1},{"id":2,"name":"B","kind":1},{"id":3,"na""";
        var exchange = Exchange("https://h/item", DateTimeOffset.UtcNow) with { IsTruncated = true, ResponseBody = body };

        var result = ExchangeContractCheck.Check(exchange, ItemList, DtoContractValidatorTests.Options, Acceptance);

        Assert.True(result.Passed, result.ToMarkdown());
        Assert.Contains(result.Issues, issue => issue.Severity == ContractSeverity.Warning && issue.Message.Contains("first 2 complete items", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""[{"a":1},{"a":[2,3]},{"a":""", 2)]
    [InlineData("""[{"a":1}]""", 1)]
    [InlineData("""{"a":1""", null)]
    [InlineData("""[{"a":""", null)]
    public void TruncatedJson_KeepsOnlyCompleteItems(string body, int? expected)
        => Assert.Equal(expected, TruncatedJson.CompleteArrayItems(body)?.Count);

    [Fact]
    public void Check_InvalidBody_FailsThroughValidator()
        => Assert.False(Run(Exchange(RemoteItem, DateTimeOffset.UtcNow) with { ResponseBody = """{"id":101}""" }).Passed);

    [Fact]
    public void RunFolder_WritesTheReportAndASummaryOfEveryReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bruinet-contracts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var folder = new ContractRunFolder(directory, () => [new ItemModule()]);

            var report = folder.Write("Items-Create", "Acceptance", [new ContractResult(ItemDetail.Endpoint)]);

            Assert.Contains("# Items-Create contract report", File.ReadAllText(report), StringComparison.Ordinal);
            var summary = File.ReadAllText(Path.Combine(directory, "summary.md"));
            Assert.Contains("| Items | `GET item/{id}` | read | Validated |", summary, StringComparison.Ordinal);
            Assert.Contains("| Items | `GET item` | read | Skipped |", summary, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static ContractResult Run(ContractExchange? exchange)
        => ExchangeContractCheck.Check(exchange, ItemDetail, DtoContractValidatorTests.Options, Acceptance);

    private static void AssertSingle(ContractResult result, ContractSeverity severity, string messagePart)
    {
        var issue = Assert.Single(result.Issues);
        Assert.Equal(severity, issue.Severity);
        Assert.Contains(messagePart, issue.Message, StringComparison.Ordinal);
        Assert.False(result.Passed);
    }

    private static ContractExchange Exchange(string path, DateTimeOffset timestamp, string method = "GET")
        => new(Guid.NewGuid().ToString(), timestamp, method, path, 200, "application/json; charset=utf-8", ValidItem)
        {
            Environment = "Acceptance"
        };

    public sealed class ItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ItemKind Kind { get; set; }
    }

    public enum ItemKind
    {
        Plain = 1
    }

    private sealed class ItemModule : IContractModule
    {
        public string Name => "Items";
        public IReadOnlyList<DtoContract> Contracts => [ItemList, ItemDetail];
        public IReadOnlyList<MockContractRequest> MockRequests => [];
    }
}
