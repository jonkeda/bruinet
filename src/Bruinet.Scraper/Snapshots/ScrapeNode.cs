namespace Bruinet.Scraper.Snapshots;

/// <summary>
/// One node of a captured UI tree: an element, or the root of a shadow tree. The same shape for every
/// platform; what is platform-specific is in <see cref="Attributes"/>.
/// </summary>
public sealed record ScrapeNode
{
    /// <summary>The node's number in its snapshot, from 1, in document order.</summary>
    public required int Number { get; init; }

    /// <summary>The parent's number; 0 for the root.</summary>
    public required int Parent { get; init; }

    /// <summary>What the node is.</summary>
    public ScrapeNodeKind Kind { get; init; } = ScrapeNodeKind.Element;

    /// <summary>The platform's type of the node: the tag name in Html, the control type under UI Automation.</summary>
    public string Type { get; init; } = "";

    /// <summary>The accessibility role, when the node has one.</summary>
    public string? Role { get; init; }

    /// <summary>The accessible name: <c>aria-label</c> in Html, the name under UI Automation.</summary>
    public string? Name { get; init; }

    /// <summary>The node's own text, not its children's.</summary>
    public string? Text { get; init; }

    /// <summary>Where the node is, in the page's or window's coordinates; null when it has no box.</summary>
    public ScrapeBounds? Bounds { get; init; }

    /// <summary>For a frame's host (an <c>iframe</c>): the address of the document inside it, whose nodes are its children.</summary>
    public string? Frame { get; init; }

    /// <summary>The platform's other attributes, by name: in Html <c>id</c>, <c>class</c>, <c>name</c>, <c>type</c>, <c>placeholder</c> and every <c>data-*</c>.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = Empty;

    /// <summary>An attribute's value, or null.</summary>
    public string? Attribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}

/// <summary>What a <see cref="ScrapeNode"/> is.</summary>
public enum ScrapeNodeKind
{
    /// <summary>An element or control.</summary>
    Element,

    /// <summary>The root of an element's shadow tree; its children are the shadow tree's elements.</summary>
    ShadowRoot,
}

/// <summary>A node's box.</summary>
public readonly record struct ScrapeBounds(int X, int Y, int Width, int Height);
