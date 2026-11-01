namespace Bruinet.Mocking.Sensors;

/// <summary>
/// Captures PCM chunks for test assertions.
/// Use as a building block for app-specific speaker/audio-output test providers.
/// </summary>
public class MockAudioSink : IDisposable
{
    private readonly List<byte[]> _chunks = new();
    private readonly object _lock = new();

    public bool IsActive { get; private set; }
    public int SampleRate { get; private set; }

    public bool HasData { get { lock (_lock) return _chunks.Count > 0; } }
    public int ChunkCount { get { lock (_lock) return _chunks.Count; } }
    public int TotalBytes { get { lock (_lock) return _chunks.Sum(c => c.Length); } }

    public event EventHandler? Disconnected;

    public void Start(int sampleRate)
    {
        SampleRate = sampleRate;
        IsActive = true;
    }

    public void Stop() => IsActive = false;

    public void Feed(byte[] pcmData)
    {
        lock (_lock)
            _chunks.Add(pcmData.ToArray());
    }

    public byte[] GetAllData()
    {
        lock (_lock)
            return _chunks.SelectMany(c => c).ToArray();
    }

    public IReadOnlyList<byte[]> GetChunks()
    {
        lock (_lock)
            return _chunks.ToList();
    }

    public void Reset()
    {
        lock (_lock)
            _chunks.Clear();
        IsActive = false;
        SampleRate = 0;
    }

    public void SimulateDisconnect()
    {
        IsActive = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { }
}
