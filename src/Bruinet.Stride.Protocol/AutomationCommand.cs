using System.Text.Json.Serialization;

namespace Bruinet.Stride.Communication;

/// <summary>
/// Command sent from test process to game for automation.
/// </summary>
public class AutomationCommand
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public object[]? Args { get; set; }

    public static AutomationCommand Query(string method, string? target = null, params object[] args) => new()
    {
        Type = "Query",
        Method = method,
        Target = target,
        Args = args.Length > 0 ? args : null
    };

    public static AutomationCommand Action(string method, string? target = null, params object[] args) => new()
    {
        Type = "Action",
        Method = method,
        Target = target,
        Args = args.Length > 0 ? args : null
    };

    public static AutomationCommand GameQuery(string method, params object[] args) => new()
    {
        Type = "GameQuery",
        Method = method,
        Args = args.Length > 0 ? args : null
    };
}
