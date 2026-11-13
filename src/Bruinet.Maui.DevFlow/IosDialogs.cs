using System.Text.Json;
using Microsoft.Maui.DevFlow.Driver;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// An iOS alert, which the agent's tree does not show, read through the app's bridge (its <c>alert</c>
/// route, <c>BruinetDevFlowBridge</c>) into the dialog shape the Windows tree gives: a window holding
/// the title, the message, a prompt's field and the buttons, accepting one first. Its nodes' ids
/// start with <see cref="IdPrefix"/>; a button is pressed and the field filled through the same route.
/// </summary>
internal static class IosDialogs
{
    /// <summary>The ids of the alert's nodes: native, and a dialog, as the tree's rules expect.</summary>
    public const string IdPrefix = "native:ios:dialog:";

    public static bool IsDialogNode(string? id) => id?.StartsWith(IdPrefix, StringComparison.Ordinal) == true;

    /// <summary>The alert the route describes, as a window of the agent's shape; null when none is open.</summary>
    public static ElementInfo? Read(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("open", out var open) || !open.GetBoolean())
            return null;

        var bounds = new BoundsInfo
        {
            X = root.GetProperty("x").GetDouble(),
            Y = root.GetProperty("y").GetDouble(),
            Width = root.GetProperty("width").GetDouble(),
            Height = root.GetProperty("height").GetDouble(),
        };
        var title = Text(root, "title");
        var window = Info("window", "Window", title, bounds);
        window.Children = [];
        if (title is not null)
            window.Children.Add(Info("title", "Text", title, bounds));
        if (Text(root, "message") is { } message)
            window.Children.Add(Info("message", "Text", message, bounds));
        if (root.TryGetProperty("hasField", out var hasField) && hasField.GetBoolean())
            window.Children.Add(Info("field", "Edit", Text(root, "field"), bounds));
        var index = 0;
        foreach (var button in root.GetProperty("buttons").EnumerateArray())
            window.Children.Add(Info($"button{index++}", "Button", button.GetString(), bounds));
        return window;
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static ElementInfo Info(string name, string type, string? text, BoundsInfo bounds) => new()
    {
        Id = IdPrefix + name,
        Type = type,
        FullType = "ios:UIAlertController",
        Text = text,
        IsVisible = true,
        IsEnabled = true,
        WindowBounds = bounds,
    };
}
