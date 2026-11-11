using System.Text.Json.Serialization;

namespace Bruinet.Stride.Communication;

/// <summary>
/// Response from game to test process.
/// </summary>
public class AutomationResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }

    public static AutomationResponse Ok(object? result = null) => new()
    {
        Success = true,
        Result = result
    };

    public static AutomationResponse Fail(string error, string? stackTrace = null) => new()
    {
        Success = false,
        Error = error,
        StackTrace = stackTrace
    };
}
