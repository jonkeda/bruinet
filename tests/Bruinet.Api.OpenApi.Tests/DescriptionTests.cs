using System.Text.Json.Nodes;
using Bruinet.Core.Api;

namespace Bruinet.Api.OpenApi.Tests;

// A description merged from exchanges, read from a document, compared, and held against what a service answers.
public sealed class DescriptionTests
{
    private const string Written = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Contacts", "version": "1.2" },
          "paths": {
            "/contact/{id}": {
              "parameters": [ { "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } } ],
              "get": {
                "responses": {
                  "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Contact" } } } },
                  "404": { "description": "None." }
                }
              }
            },
            "/contact": {
              "post": {
                "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Contact" } } } },
                "responses": { "201": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Contact" } } } } }
              }
            },
            "/contact/types": { "get": { "responses": { "200": { "content": { "application/json": { "schema": { "type": "array", "items": { "type": "string", "enum": [ "client", "supplier" ] } } } } } } } }
          },
          "components": {
            "schemas": {
              "Contact": {
                "type": "object",
                "required": [ "id", "name" ],
                "properties": {
                  "id": { "type": "integer" },
                  "name": { "type": "string" },
                  "parent": { "$ref": "#/components/schemas/Contact" },
                  "kind": { "type": "string", "enum": [ "client", "supplier" ], "nullable": true }
                }
              }
            }
          }
        }
        """;

    [Fact]
    public void Merge_FindsTheValuesInPaths_JoinsShapes_AndKeepsNoValue()
    {
        var description = ObservedDescription.Merge(
        [
            Call("GET", "https://api-acc.example.com/contact/12", 200, """{"id":12,"name":"Arend","city":null,"amount":5}"""),
            Call("GET", "https://api-acc.example.com/contact/13?expand=lines", 200, """{"id":13,"name":"Bas","city":"Delft","amount":2.5,"lines":[{"at":"2026-11-21T09:30:00Z"}]}"""),
            Call("GET", "https://api-acc.example.com/contact/99", 404, ""),
            Call("POST", "https://api-acc.example.com/contact", 200, """{"id":14}""", """{"name":"Cor","password":"***"}"""),
            Call("GET", "https://maps.example.org/tile/1", 200, "{}"),
            new ApiExchange("x", DateTimeOffset.UtcNow, "GET", "https://api-acc.example.com/contact/1", null, null, null) { Error = "timed out" },
        ], "Contacts", ["api-acc.example.com"]);

        Assert.Equal(["GET /contact/{id}", "POST /contact"], description.Operations.Select(operation => operation.Name).Order());
        var get = description.Find("get", "/contact/777")!;
        Assert.Equal(3, get.Observed);
        Assert.Equal(["id path integer True", "expand query string False"], get.Parameters.Select(parameter => $"{parameter.Name} {parameter.In} {parameter.Type} {parameter.Required}"));
        Assert.Equal(["200", "404"], get.Responses.Keys.Order());
        Assert.Null(get.Responses["404"]);
        Assert.Equal(
            ["amount: number!", "city: string?!", "id: integer!", "lines: array", "lines[]: object", "lines[].at: string!", "name: string!"],
            Shapes.Flatten(get.Responses["200"]).Select(line => $"{line.Key}: {line.Value}"));
        var json = description.ToJson();
        Assert.DoesNotContain("Arend", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Delft", json, StringComparison.Ordinal);
        Assert.Contains("\"x-observed-count\": 3", json, StringComparison.Ordinal);
        Assert.Equal(["https://api-acc.example.com"], description.Servers);
    }

    [Fact]
    public void AnObservedDescription_ReadsBackAsItWasWritten()
    {
        var description = ObservedDescription.Merge([Call("PUT", "https://api-acc.example.com/project/5/line/0a1b2c3d4e5f60718293a4b5c6d7e8f9", 200, """[{"id":1}]""", """{"ok":true}""")]);

        var read = ApiDescription.Read(description.ToJson());

        Assert.True(read.IsObserved);
        Assert.Equal(description.ToJson(), read.ToJson());
        Assert.Equal("PUT /project/{id}/line/{id2}", Assert.Single(read.Operations).Name);
    }

    [Fact]
    public void Read_FollowsReferences_AlsoOneThatHoldsItself_AndRefusesWhatIsNotOpenApi3()
    {
        var description = ApiDescription.Read(Written);

        Assert.False(description.IsObserved);
        Assert.Equal(3, description.Operations.Count);
        Assert.Equal("GET /contact/types", description.Find("GET", "/contact/types")!.Name);
        Assert.Equal("GET /contact/{id}", description.Find("GET", "/contact/8")!.Name);
        Assert.Equal("string?", Shapes.Flatten(description.Find("GET", "/contact/8")!.Responses["200"])["parent.kind"]);
        Assert.Throws<FormatException>(() => ApiDescription.Read("""{"swagger":"2.0"}"""));
        Assert.Throws<FormatException>(() => ApiDescription.Read("openapi: 3.0.3"));
    }

    [Fact]
    public void Diff_NamesAnOperation_AStatus_AndAFieldThatChanged()
    {
        var before = ObservedDescription.Merge(
        [
            Call("GET", "https://a.test/contact/1", 200, """{"id":1,"name":"x","amount":5}"""),
            Call("GET", "https://a.test/old", 200, "{}"),
        ]);
        var now = ObservedDescription.Merge(
        [
            Call("GET", "https://a.test/contact/1", 200, """{"id":"1","name":"x","city":"y"}"""),
            Call("GET", "https://a.test/contact/2", 403, ""),
            Call("GET", "https://a.test/new", 200, "{}"),
        ]);

        Assert.Equal(
            [
                "GET /contact/{id} 200: amount gone (was integer!)",
                "GET /contact/{id} 200: city added (string!)",
                "GET /contact/{id} 200: id is string!, was integer!",
                "GET /contact/{id}: answers 403 now",
                "GET /new: new",
                "GET /old: gone",
            ],
            DescriptionDiff.Compare(before, now));
        Assert.Empty(DescriptionDiff.Compare(before, before));
    }

    [Fact]
    public void Uncalled_AreTheOperationsNoExchangeReached()
    {
        var description = ApiDescription.Read(Written);

        var uncalled = DescriptionCheck.Uncalled(description, [Call("GET", "https://a.test/contact/5", 200, "{}"), Call("GET", "https://a.test/elsewhere", 200, "{}")]);

        Assert.Equal(["GET /contact/types", "POST /contact"], uncalled.Select(operation => operation.Name).Order());
    }

    [Fact]
    public void Check_HoldsAnAnswerAgainstItsSchema()
    {
        var description = ApiDescription.Read(Written);

        var fits = DescriptionCheck.Check(description, Call("GET", "https://a.test/contact/5", 200, """{"id":5,"name":"Arend","kind":null,"parent":{"id":1,"name":"x"}}"""));
        var wrong = DescriptionCheck.Check(description, Call("GET", "https://a.test/contact/5", 200, """{"id":"5","kind":"friend","extra":1,"parent":{"id":1}}"""));
        var unknown = DescriptionCheck.Check(description, Call("GET", "https://a.test/contact/5", 500, """{"message":"boom"}"""));
        var none = DescriptionCheck.Check(description, Call("DELETE", "https://a.test/contact/5", 200, ""));

        Assert.True(fits.Passed && !fits.Warnings.Any(), fits.ToMarkdown());
        Assert.Equal(
            [
                "$.id|A string, where the description says integer.",
                "$.kind|\"friend\" is not one of the 2 values the description names.",
                "$.name|Missing, where the description says every record has it.",
                "$.parent.name|Missing, where the description says every record has it.",
            ],
            wrong.Failures.Select(issue => $"{issue.Path}|{issue.Message}").Order(StringComparer.Ordinal));
        Assert.Equal("$.extra", Assert.Single(wrong.Warnings).Path);
        Assert.True(unknown.Passed);
        Assert.Contains("has no answer 500", Assert.Single(unknown.Warnings).Message, StringComparison.Ordinal);
        Assert.Contains("no such operation", Assert.Single(none.Warnings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASample_FitsItsOwnSchema()
    {
        var schema = ApiDescription.Read(Written).Find("POST", "/contact")!.Responses["201"]!;

        var sample = SchemaSample.Of(schema);

        Assert.Equal("client", (string?)sample!["kind"]);
        Assert.Equal(0, (int?)sample["id"]);
        Assert.True(DescriptionCheck.Check("sample", schema, sample).Passed);
        Assert.Equal("[\"client\"]", SchemaSample.Of(ApiDescription.Read(Written).Find("GET", "/contact/types")!.Responses["200"])!.ToJsonString());
    }

    [Fact]
    public void TwoTypesThatDoNotGoTogether_AreAnyValue_WithWhatWasSeen()
    {
        var merged = Shapes.Merge(Shapes.Of(JsonNode.Parse("\"x\"")), Shapes.Of(JsonNode.Parse("[1]")));

        Assert.Equal("""{"x-observed-types":["array","string"]}""", merged.ToJsonString());
        Assert.True(DescriptionCheck.Check("any", merged, JsonNode.Parse("true")).Passed);
    }

    private static ApiExchange Call(string method, string url, int status, string answer, string? sent = null)
        => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, method, url, status, "application/json", answer) { RequestBody = sent };
}
