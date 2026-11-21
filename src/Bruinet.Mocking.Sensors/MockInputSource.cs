namespace Bruinet.Mocking.Sensors;

/// <summary>
/// Emits input events programmatically for testing gesture/button pipelines.
/// Generic — does not depend on any specific app's button types.
/// </summary>
public class MockInputSource<TEvent> : IDisposable
{
    public bool IsActive { get; private set; }
    public int EventCount { get; private set; }

    public event EventHandler<TEvent>? InputReceived;
    public event EventHandler? Disconnected;

    public void Start() => IsActive = true;
    public void Stop() => IsActive = false;

    public void Emit(TEvent evt)
    {
        InputReceived?.Invoke(this, evt);
        EventCount++;
    }

    public void SimulateDisconnect()
    {
        IsActive = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        EventCount = 0;
    }

    public void Dispose() { }
}
