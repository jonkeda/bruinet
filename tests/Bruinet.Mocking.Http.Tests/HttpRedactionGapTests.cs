namespace Bruinet.Mocking.Http.Tests;

/// <summary>
/// The redaction's gaps closed (review 09, finding 5; R2-T23): a streamed answer's parts, an XML body, a Location's
/// query and more default names are written ***, and an Azure resource key after a key-like name stops the save.
/// </summary>
public sealed class HttpRedactionGapTests
{
    private static readonly HttpReplayOptions Options = new();

    [Fact]
    public void A_StreamedAnswersParts_AreRedactedByName()
    {
        var exchange = new HttpExchange
        {
            Chunks =
            [
                new HttpChunk { Data = "data: {\"delta\":\"Hi\",\"access_token\":\"abc123\"}\n\n" },
                new HttpChunk { Data = "data: [DONE]\n\n" },
            ],
        };

        HttpRedaction.Redact(exchange, Options);

        Assert.Contains("\"access_token\":\"***\"", exchange.Chunks![0].Data, StringComparison.Ordinal);
        Assert.Contains("\"delta\":\"Hi\"", exchange.Chunks[0].Data, StringComparison.Ordinal);
        Assert.Equal("data: [DONE]\n\n", exchange.Chunks[1].Data);
    }

    [Fact]
    public void An_XmlBody_HasItsNamedElementsAndAttributesRedacted()
    {
        var exchange = new HttpExchange
        {
            RequestHeaders = { ["Content-Type"] = "text/xml" },
            RequestBody = "<soap:Envelope><soap:Body><Login user=\"mia\" password=\"hunter2\"><wsse:Password>hunter2</wsse:Password></Login></soap:Body></soap:Envelope>",
        };

        HttpRedaction.Redact(exchange, Options);

        Assert.DoesNotContain("hunter2", exchange.RequestBody, StringComparison.Ordinal);
        Assert.Contains("<wsse:Password>***</wsse:Password>", exchange.RequestBody, StringComparison.Ordinal);
        Assert.Contains("password=\"***\"", exchange.RequestBody, StringComparison.Ordinal);
        Assert.Contains("user=\"mia\"", exchange.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LocationsQuery_IsRedacted_TheOAuthCodeIncluded()
    {
        var exchange = new HttpExchange
        {
            ResponseHeaders = { ["Location"] = "https://app.test/callback?code=SplxlOBeZQQYbYS6WxSbIA&state=xyz&access_token=t0k" },
        };

        HttpRedaction.Redact(exchange, Options);

        Assert.Equal("https://app.test/callback?code=***&state=xyz&access_token=***", exchange.ResponseHeaders["Location"]);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("secret")]
    [InlineData("code_verifier")]
    [InlineData("client_assertion")]
    [InlineData("subscription-key")]
    public void The_Defaults_NameTheCommonSecrets(string field)
        => Assert.Contains(field, HttpReplayOptions.DefaultRedactedFields);

    [Fact]
    public void A_BodysErrorCode_IsKept()
    {
        var exchange = new HttpExchange
        {
            ResponseHeaders = { ["Content-Type"] = "application/json" },
            ResponseBody = "{\"code\":\"NOT_FOUND\"}",
        };

        HttpRedaction.Redact(exchange, Options);

        Assert.Equal("{\"code\":\"NOT_FOUND\"}", exchange.ResponseBody);
    }

    [Fact]
    public void An_AzureResourceKey_AfterAKeyLikeName_StopsTheSave()
    {
        var recording = new HttpRecording
        {
            Exchanges = [new HttpExchange { Url = "https://x.test/", ResponseBody = "{\"apiKey\": \"0123456789abcdef0123456789abcdef\"}" }],
        };

        var error = Assert.Throws<HttpRecordingSecretException>(() => HttpRedaction.Scan(recording, "x.json"));

        Assert.Contains("an Azure resource key", error.Message, StringComparison.Ordinal);
    }
}
