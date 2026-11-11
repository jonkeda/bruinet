using System.Globalization;
using System.Text.Json;
using Bruinet.Bdd;
using Bruinet.Core.Exceptions;

namespace Bruinet.Mocking.Steps;

/// <summary>
/// Steps about the requests the mock server received in this scenario (since
/// <see cref="BddExecutionContext.ScenarioStartedUtc"/>). A feature file opts in with <c>Using MockRequests</c>;
/// the fixture's scan takes this namespace in (<c>[TestModuleScan(typeof(MockRequestSteps), NamespacePrefix = "Bruinet.Mocking.Steps")]</c>).
/// </summary>
/// <remarks>
/// The server is the composition's <see cref="MockApiServer"/>, else the shared one (<see cref="MockApiServer.Shared"/>).
/// A path may hold <c>*</c> as a wildcard. "No request was sent" is checked when the step runs: put it after a step that
/// waited for what the app shows, so a request the app was about to send has been sent.
/// </remarks>
/// <param name="services">The scenario's services, which may hold the server.</param>
[BddStepDefinitions(Namespace = Namespace)]
public sealed class MockRequestSteps(IServiceProvider services) : BddStepDefinitionsBase
{
    /// <summary>The namespace a feature file names in <c>Using</c>.</summary>
    public const string Namespace = "MockRequests";

    /// <summary>The value that, in a <c>has</c> table, means the field is not in the body or is null.</summary>
    public const string Absent = "(absent)";

    private MockApiServer Server => services.GetService(typeof(MockApiServer)) as MockApiServer
        ?? MockApiServer.Shared
        ?? throw new InvalidOperationException("No mock server: register a MockApiServer in the fixture's composition, or hold a lease on the shared one.");

    /// <summary>No request with the method was sent to the path in this scenario.</summary>
    /// <param name="context">The scenario.</param>
    /// <param name="method">The HTTP method, <c>POST</c>.</param>
    /// <param name="path">The path, <c>/contact</c>.</param>
    [BddThen("no {method} was sent to {path}")]
    public void NoneSent(BddExecutionContext context, string method, string path)
    {
        var sent = Sent(context, method, path);
        if (sent.Count > 0)
        {
            throw new ControlAssertionException($"No {method.ToUpperInvariant()} to {path} was expected in this scenario.", 0, Describe(sent));
        }
    }

    /// <summary>Exactly this many requests with the method were sent to the path in this scenario; waits for them to arrive.</summary>
    /// <param name="context">The scenario.</param>
    /// <param name="count">How many.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    [BddThen("{count} {method} requests were sent to {path}")]
    public void CountSent(BddExecutionContext context, int count, string method, string path)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > 0)
        {
            WaitFor(context, method, path, count);
        }

        var sent = Sent(context, method, path);
        if (sent.Count != count)
        {
            throw new ControlAssertionException($"{count} {method.ToUpperInvariant()} requests to {path} were expected in this scenario.", count, Describe(sent));
        }
    }

    /// <summary>One request with the method was sent to the path in this scenario; waits for it to arrive.</summary>
    /// <param name="context">The scenario.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    [BddThen("one {method} request was sent to {path}")]
    public void OneSent(BddExecutionContext context, string method, string path) => CountSent(context, 1, method, path);

    /// <summary>
    /// The last request with the method to the path in this scenario has each row's field and value: a <c>Field</c> and a
    /// <c>Value</c> column. A field is its wire name, dotted for a nested one (<c>address.city</c>, <c>lines.0.amount</c>);
    /// in a form body it is the form field. <see cref="Absent"/> means the field is not sent or is null. Waits for the
    /// request to arrive.
    /// </summary>
    /// <param name="context">The scenario.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <param name="table">The fields.</param>
    [BddThen("the last {method} to {path} has")]
    public void LastHas(BddExecutionContext context, string method, string path, BddTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!WaitFor(context, method, path, 1))
        {
            throw new ControlAssertionException($"A {method.ToUpperInvariant()} to {path} was expected in this scenario.", 1, 0);
        }

        var last = Sent(context, method, path)[^1];
        var read = Reader(last);
        List<string> wrong = [];
        foreach (var row in table.Rows)
        {
            var field = Cell(row, "Field");
            var expected = Cell(row, "Value");
            var actual = read(field);
            if (!Matches(expected, actual))
            {
                wrong.Add($"{field}: expected {expected}, sent {actual ?? Absent}");
            }
        }

        if (wrong.Count > 0)
        {
            throw new ControlAssertionException(
                $"The last {method.ToUpperInvariant()} to {path} differs:{Environment.NewLine}{string.Join(Environment.NewLine, wrong)}",
                null,
                last.Body);
        }
    }

    private List<RecordedRequest> Sent(BddExecutionContext context, string method, string path)
    {
        var since = context.ScenarioStartedUtc ?? DateTime.MinValue;
        return [.. Server.Requests(method, path).Where(request => request.ReceivedAtUtc >= since)];
    }

    // Waits until count requests arrived; on a timeout the caller's check fails with what did arrive.
    private bool WaitFor(BddExecutionContext context, string method, string path, int count)
    {
        var since = context.ScenarioStartedUtc ?? DateTime.MinValue;
        try
        {
            Server.WaitForRequest(method, path, request => request.ReceivedAtUtc >= since, skip: count - 1);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static bool Matches(string expected, string? actual)
        => expected == Absent ? actual is null : string.Equals(expected, actual, StringComparison.Ordinal);

    private static string Cell(BddTableRow row, string column)
        => row.Cells.FirstOrDefault(cell => cell.Key.Equals(column, StringComparison.OrdinalIgnoreCase)).Value?.Trim()
            ?? throw new InvalidOperationException($"The table needs a {column} column (line {row.Source}).");

    private static string Describe(IEnumerable<RecordedRequest> requests)
        => string.Join(", ", requests.Select(request => string.Create(CultureInfo.InvariantCulture, $"{request.Method} {request.Path} at {request.ReceivedAtUtc:HH:mm:ss.fff}")));

    // A field's value as the table writes it: a JSON string unquoted, a number or true/false as written, an object or array as
    // its JSON, null for absent or null.
    private static Func<string, string?> Reader(RecordedRequest request)
    {
        var contentType = request.Headers.TryGetValue("Content-Type", out var type) ? type : string.Empty;
        if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var form = request.BodyAsForm();
            return field => form.TryGetValue(field, out var value) ? value : null;
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(request.Body ?? string.Empty);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The last {request} has no JSON or form body to read fields from: {Shorten(request.Body)}", exception);
        }

        return field => Read(root, field);
    }

    private static string Shorten(string? body)
        => body is null ? "(no body)" : body.Length <= 200 ? body : string.Concat(body.AsSpan(0, 200), "...");

    private static string? Read(JsonElement element, string field)
    {
        foreach (var part in field.Split('.'))
        {
            if (element.ValueKind == JsonValueKind.Object && TryProperty(element, part, out var child))
            {
                element = child;
            }
            else if (element.ValueKind == JsonValueKind.Array && int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index < element.GetArrayLength())
            {
                element = element[index];
            }
            else
            {
                return null;
            }
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText(),
        };
    }

    // The wire name as written, else ignoring case: a table may write Name for name.
    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
