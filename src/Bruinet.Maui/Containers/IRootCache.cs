namespace Bruinet.Maui.Containers;

/// <summary>A scope that keeps its resolved root element between operations.</summary>
internal interface IRootCache
{
    /// <summary>Forgets the root, so the next use finds it again.</summary>
    void InvalidateCache();
}
