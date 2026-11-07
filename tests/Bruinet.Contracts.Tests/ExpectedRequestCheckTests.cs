using System.Net.Http.Headers;

namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class ExpectedRequestCheckTests
{
    private static readonly DtoContract Json = new("POST contact", typeof(object))
    {
        Request = new(RequestBodyKind.Json, "test",
        [
            new("id", ExpectedValue.Omitted),
            new("name", ExpectedValue.Text),
            new("contact_type_id", ExpectedValue.Integer),
            new("start", ExpectedValue.Moment, Optional: true),
            new("type", ExpectedValue.ApprovedDifference),
            new("employees[*].id", ExpectedValue.Integer, Optional: true),
            new("custom_attribute_values", ExpectedValue.Any, Optional: true)
        ])
    };

    [Fact]
    public void Compare_RequestLikeAndroid_Passes()
    {
        var result = ExpectedRequestCheck.Compare(Json, "application/json",
            """{"name":"A","contact_type_id":3,"start":"2026-11-07T00:00:00Z","employees":[{"id":4}],"custom_attribute_values":[{"custom_attribute_id":1,"attribute_value":""}]}""");

        Assert.True(result.Passed, result.ToMarkdown());
    }

    [Theory]
    [InlineData("""{"id":5,"name":"A","contact_type_id":3}""", "$.id")]
    [InlineData("""{"name":"A"}""", "$.contact_type_id")]
    [InlineData("""{"name":"A","contact_type_id":"3"}""", "$.contact_type_id")]
    [InlineData("""{"name":"A","contact_type_id":3,"start":"07-11-2026"}""", "$.start")]
    [InlineData("""{"name":"A","contact_type_id":3,"extra":1}""", "$.extra")]
    [InlineData("""{"name":null,"contact_type_id":3}""", "$.name")]
    public void Compare_DifferenceFromAndroid_FailsAtTheField(string body, string path)
    {
        var result = ExpectedRequestCheck.Compare(Json, "application/json", body);

        Assert.False(result.Passed);
        Assert.Contains(path, result.ToMarkdown());
    }

    [Fact]
    public void Compare_OtherBodyKind_Fails()
    {
        var result = ExpectedRequestCheck.Compare(Json, "application/x-www-form-urlencoded", "name=A&contact_type_id=3");

        Assert.False(result.Passed);
    }

    [Theory]
    [InlineData("custom_attributes[0][value]", "custom_attributes[*].value")]
    [InlineData("ids[]", "ids[*]")]
    [InlineData("contact[id]", "contact.id")]
    public void FormPath_ReadsBracketsAsJsonPaths(string key, string path)
        => Assert.Equal(path, ExpectedRequestCheck.FormPath(key));

    [Fact]
    public async Task DescribeMultipart_MatchesTheAppsPartList()
    {
        using var content = new MultipartFormDataContent("boundary-1");
        content.Add(new StringContent("Kitchen & bath"), "description");
        var file = new ByteArrayContent("photo-bytes"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "photo.jpg");
        var bytes = await content.ReadAsByteArrayAsync();
        var upload = new DtoContract("POST project/{id}/file", typeof(object))
        {
            Request = new(RequestBodyKind.Multipart, "test",
                [new("description", ExpectedValue.Text), new("file", ExpectedValue.File)])
        };

        var body = ExpectedRequestCheck.DescribeMultipart(content.Headers.ContentType!.ToString(), bytes);
        var result = ExpectedRequestCheck.Compare(upload, "multipart/form-data", body);

        Assert.Equal("description=Kitchen%20%26%20bath&file=%5Bfile%20photo.jpg%3B%20image%2Fjpeg%3B%2011%20bytes%5D", body);
        Assert.True(result.Passed, result.ToMarkdown());
    }
}
