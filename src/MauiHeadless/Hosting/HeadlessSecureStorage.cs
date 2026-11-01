using Microsoft.Maui.Storage;

namespace MauiHeadless.Hosting;

/// <summary>Secure storage kept in memory for one test context; registered as <see cref="ISecureStorage"/>.</summary>
public sealed class HeadlessSecureStorage : ISecureStorage
{
    private readonly Dictionary<string, string> _values = [];
    private readonly object _gate = new();

    public Task<string?> GetAsync(string key)
    {
        lock (_gate)
            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
            _values[key] = value;
        return Task.CompletedTask;
    }

    public bool Remove(string key)
    {
        lock (_gate)
            return _values.Remove(key);
    }

    public void RemoveAll()
    {
        lock (_gate)
            _values.Clear();
    }
}
