namespace Bruinet.Mocking.Sensors;

/// <summary>
/// Emits pre-loaded PCM audio chunks on a configurable timer.
/// Use as a building block for app-specific mic/audio-input test providers.
/// </summary>
public class MockAudioSource : IDisposable
{
    private readonly byte[] _pcmData;
    private readonly int _chunkSize;
    private readonly int _chunkIntervalMs;
    private CancellationTokenSource? _cts;

    // The emit loop, kept so StopAsync can raise what a ChunkEmitted handler threw (review 05, finding 14).
    private Task _emitting = Task.CompletedTask;

    public bool IsEmitting { get; private set; }
    public int ChunksEmitted { get; private set; }
    public bool FinishedPlaying { get; private set; }

    public event EventHandler<byte[]>? ChunkEmitted;
    public event EventHandler? Disconnected;

    public MockAudioSource(byte[] pcmData, int chunkSize = 3200, int chunkIntervalMs = 100)
    {
        _pcmData = pcmData;
        _chunkSize = chunkSize;
        _chunkIntervalMs = chunkIntervalMs;
    }

    public static MockAudioSource FromFile(string path, int chunkSize = 3200, int intervalMs = 100)
        => new(File.ReadAllBytes(path), chunkSize, intervalMs);

    public static MockAudioSource Silence(int durationMs = 1000, int sampleRate = 16000)
    {
        var bytes = sampleRate * 2 * durationMs / 1000;
        return new MockAudioSource(new byte[bytes]);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsEmitting)
            return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsEmitting = true;
        ChunksEmitted = 0;
        FinishedPlaying = false;
        _emitting = EmitChunksAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>Stops the audio; raises what the app's <see cref="ChunkEmitted"/> handler threw, if it did.</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        try
        {
            await _emitting.ConfigureAwait(false);
        }
        finally
        {
            IsEmitting = false;
        }
    }

    public void SimulateDisconnect()
    {
        IsEmitting = false;
        _cts?.Cancel();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        ChunksEmitted = 0;
        FinishedPlaying = false;
        IsEmitting = false;
    }

    private async Task EmitChunksAsync(CancellationToken ct)
    {
        await Task.Yield();
        var offset = 0;
        try
        {
            while (!ct.IsCancellationRequested && offset < _pcmData.Length)
            {
                var remaining = _pcmData.Length - offset;
                var size = Math.Min(_chunkSize, remaining);
                var chunk = new byte[size];
                Array.Copy(_pcmData, offset, chunk, 0, size);
                ChunkEmitted?.Invoke(this, chunk);
                ChunksEmitted++;
                offset += size;
                try
                { await Task.Delay(_chunkIntervalMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }

            FinishedPlaying = true;
        }
        finally
        {
            IsEmitting = false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
    }
}
