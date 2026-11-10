namespace Bruinet.Core.Calls;

/// <summary>
/// A stretch of code that owns where the view is, such as a list's row search: a read made inside
/// it neither checks that its element is visible nor scrolls it into view. A row predicate that
/// scrolled each partly shown row's title into view moved the list under the search that was
/// stepping through it (.my/uitests D23). Actions still check visibility.
/// </summary>
public static class ReadsInPlace
{
    private static readonly AsyncLocal<bool> Now = new();

    /// <summary>Whether reads leave the view where it is.</summary>
    public static bool Active => Now.Value;

    /// <summary>Starts a stretch of reads in place; the previous state comes back when it ends.</summary>
    public static IDisposable Begin()
    {
        var before = Now.Value;
        Now.Value = true;
        return new Scope(before);
    }

    private sealed class Scope(bool before) : IDisposable
    {
        public void Dispose() => Now.Value = before;
    }
}
