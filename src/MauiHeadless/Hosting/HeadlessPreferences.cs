using Microsoft.Maui.Storage;

namespace MauiHeadless.Hosting;

/// <summary>Preferences kept in memory for one test context; registered as <see cref="IPreferences"/>.</summary>
public sealed class HeadlessPreferences : IPreferences
{
    private readonly Dictionary<(string? SharedName, string Key), object?> _values = [];
    private readonly object _gate = new();

    public bool ContainsKey(string key, string? sharedName = null)
    {
        lock (_gate)
            return _values.ContainsKey((sharedName, key));
    }

    public void Remove(string key, string? sharedName = null)
    {
        lock (_gate)
            _values.Remove((sharedName, key));
    }

    public void Clear(string? sharedName = null)
    {
        lock (_gate)
        {
            foreach (var key in _values.Keys.Where(k => k.SharedName == sharedName).ToList())
                _values.Remove(key);
        }
    }

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        lock (_gate)
            _values[(sharedName, key)] = value;
    }

    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        lock (_gate)
            return _values.TryGetValue((sharedName, key), out var value) && value is T typed ? typed : defaultValue;
    }
}
