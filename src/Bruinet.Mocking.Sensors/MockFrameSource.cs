namespace Bruinet.Mocking.Sensors;

/// <summary>
/// Returns pre-loaded image frames (JPEG byte arrays) in round-robin order.
/// Use as a building block for app-specific camera test providers.
/// </summary>
public class MockFrameSource : IDisposable
{
    private readonly byte[][] _frames;
    private int _frameIndex;

    public bool IsAvailable { get; set; } = true;
    public int FramesCaptured { get; private set; }

    public event EventHandler? Disconnected;

    public MockFrameSource(byte[] singleFrame)
    {
        _frames = [singleFrame];
    }

    public MockFrameSource(params byte[][] frames)
    {
        _frames = frames;
    }

    public static MockFrameSource FromDirectory(string path, string pattern = "*.jpg")
    {
        var files = Directory.GetFiles(path, pattern).OrderBy(f => f).ToArray();
        return new MockFrameSource(files.Select(File.ReadAllBytes).ToArray());
    }

    public byte[]? CaptureFrame()
    {
        if (_frames.Length == 0 || !IsAvailable)
            return null;

        var frame = _frames[_frameIndex % _frames.Length];
        _frameIndex++;
        FramesCaptured++;
        return frame;
    }

    public void SimulateDisconnect()
    {
        IsAvailable = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _frameIndex = 0;
        FramesCaptured = 0;
        IsAvailable = true;
    }

    public void Dispose() { }
}
