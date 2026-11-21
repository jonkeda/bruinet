using System.Globalization;
using Bruinet.Api.OpenApi;

namespace Bruinet.Api.Mocking;

/// <summary>
/// A fake service from a description: a stub for every operation it has, answering with an example of what the
/// description says the answer is. A stub a test writes itself wins over these, so a suite fakes a whole back end
/// with one line and says only what its test is about.
/// </summary>
public static class DescribedStubs
{
    /// <summary>The priority of a described stub: after every stub a test writes (<see cref="ApiStubBuilder.DefaultPriority"/>).</summary>
    public const int Priority = 1000;

    /// <summary>
    /// Stubs every operation of a description: its first successful status (or 200), with a body that fits the schema
    /// of that answer (<see cref="SchemaSample"/>). A value in a path matches anything.
    /// </summary>
    /// <param name="server">The mock server.</param>
    /// <param name="description">The description: one someone wrote, or an observed one.</param>
    /// <returns>How many operations were stubbed.</returns>
    public static int StubDescribed(this MockApiServer server, ApiDescription description)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(description);
        foreach (var operation in description.Operations)
        {
            var answer = operation.Responses
                .Select(each => (Status: int.TryParse(each.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var status) ? status : 200, each.Value))
                .OrderBy(each => each.Status is >= 200 and < 300 ? 0 : 1).ThenBy(each => each.Status)
                .FirstOrDefault((Status: 200, Value: null));
            var path = string.Join('/', operation.Path.Split('/').Select(segment => segment.StartsWith('{') && segment.EndsWith('}') ? "*" : segment));
            var stub = server.Stub(operation.Method, path).AtPriority(Priority);
            if (SchemaSample.Of(answer.Value) is { } body)
            {
                stub.ReturnsText(body.ToJsonString(), answer.Status, "application/json");
            }
            else
            {
                stub.ReturnsStatus(answer.Status);
            }
        }

        return description.Operations.Count;
    }
}
