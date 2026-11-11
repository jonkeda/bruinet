using Bruinet.Mocking.Sensors;

namespace Bruinet.Mocking.Tests.Sensors;

public class MockAudioSourceTests
{
    [Fact]
    public async Task StartAsync_EmitsChunks()
    {
        var chunks = new List<byte[]>();
        using var source = new MockAudioSource(new byte[9600]);
        source.ChunkEmitted += (_, c) => chunks.Add(c);

        await source.StartAsync();
        await Task.Delay(500);
        await source.StopAsync();

        Assert.True(chunks.Count >= 3);
        Assert.Equal(chunks.Count, source.ChunksEmitted);
    }

    [Fact]
    public async Task StopAsync_StopsEmitting()
    {
        using var source = new MockAudioSource(new byte[320000]);
        await source.StartAsync();
        await Task.Delay(50);
        await source.StopAsync();

        Assert.False(source.IsEmitting);
    }

    [Fact]
    public async Task A_Handler_ThatThrows_StopsTheAudio_AndStopAsyncRaisesIt()
    {
        using var source = new MockAudioSource(new byte[320000], chunkIntervalMs: 5);
        source.ChunkEmitted += (_, _) => throw new InvalidOperationException("the app's audio handler failed");

        await source.StartAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => source.StopAsync());

        Assert.Equal("the app's audio handler failed", error.Message);
        Assert.False(source.IsEmitting);
    }

    [Fact]
    public void SimulateDisconnect_FiresEvent()
    {
        var disconnected = false;
        using var source = new MockAudioSource(new byte[3200]);
        source.Disconnected += (_, _) => disconnected = true;

        source.SimulateDisconnect();

        Assert.True(disconnected);
        Assert.False(source.IsEmitting);
    }

    [Fact]
    public async Task Silence_CreatesCorrectSize()
    {
        using var source = MockAudioSource.Silence(1000, 16000);
        var chunks = new List<byte[]>();
        source.ChunkEmitted += (_, c) => chunks.Add(c);

        await source.StartAsync();
        while (!source.FinishedPlaying)
            await Task.Delay(10);

        var totalBytes = chunks.Sum(c => c.Length);
        Assert.Equal(32000, totalBytes); // 16000 * 2 bytes * 1 second
    }

    [Fact]
    public async Task Reset_ClearsState()
    {
        using var source = new MockAudioSource(new byte[3200]);
        await source.StartAsync();
        while (!source.FinishedPlaying)
            await Task.Delay(10);

        source.Reset();

        Assert.Equal(0, source.ChunksEmitted);
        Assert.False(source.FinishedPlaying);
    }
}

public class MockAudioSinkTests
{
    [Fact]
    public void Feed_CapturesData()
    {
        using var sink = new MockAudioSink();
        sink.Start(24000);

        sink.Feed(new byte[] { 1, 2, 3 });
        sink.Feed(new byte[] { 4, 5 });

        Assert.Equal(2, sink.ChunkCount);
        Assert.Equal(5, sink.TotalBytes);
        Assert.True(sink.HasData);
    }

    [Fact]
    public void GetAllData_ConcatenatesChunks()
    {
        using var sink = new MockAudioSink();
        sink.Feed(new byte[] { 1, 2 });
        sink.Feed(new byte[] { 3, 4 });

        var data = sink.GetAllData();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, data);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        using var sink = new MockAudioSink();
        sink.Start(24000);
        sink.Feed(new byte[] { 1 });

        sink.Reset();

        Assert.False(sink.HasData);
        Assert.Equal(0, sink.ChunkCount);
        Assert.False(sink.IsActive);
        Assert.Equal(0, sink.SampleRate);
    }

    [Fact]
    public void SimulateDisconnect_FiresEvent()
    {
        var disconnected = false;
        using var sink = new MockAudioSink();
        sink.Start(24000);
        sink.Disconnected += (_, _) => disconnected = true;

        sink.SimulateDisconnect();

        Assert.True(disconnected);
        Assert.False(sink.IsActive);
    }
}

public class MockFrameSourceTests
{
    private static readonly byte[] Frame1 = { 0xFF, 0xD8, 1 };
    private static readonly byte[] Frame2 = { 0xFF, 0xD8, 2 };

    [Fact]
    public void CaptureFrame_CyclesThroughFrames()
    {
        using var source = new MockFrameSource(Frame1, Frame2);

        Assert.Equal(Frame1, source.CaptureFrame());
        Assert.Equal(Frame2, source.CaptureFrame());
        Assert.Equal(Frame1, source.CaptureFrame()); // wraps around
        Assert.Equal(3, source.FramesCaptured);
    }

    [Fact]
    public void CaptureFrame_ReturnsNull_WhenUnavailable()
    {
        using var source = new MockFrameSource(Frame1);
        source.IsAvailable = false;

        Assert.Null(source.CaptureFrame());
    }

    [Fact]
    public void SimulateDisconnect_SetsUnavailable()
    {
        var disconnected = false;
        using var source = new MockFrameSource(Frame1);
        source.Disconnected += (_, _) => disconnected = true;

        source.SimulateDisconnect();

        Assert.True(disconnected);
        Assert.False(source.IsAvailable);
    }

    [Fact]
    public void Reset_RestoresState()
    {
        using var source = new MockFrameSource(Frame1);
        source.CaptureFrame();
        source.SimulateDisconnect();

        source.Reset();

        Assert.True(source.IsAvailable);
        Assert.Equal(0, source.FramesCaptured);
    }
}

public class MockInputSourceTests
{
    [Fact]
    public void Emit_FiresEvent()
    {
        string? received = null;
        using var source = new MockInputSource<string>();
        source.InputReceived += (_, e) => received = e;

        source.Emit("test-event");

        Assert.Equal("test-event", received);
        Assert.Equal(1, source.EventCount);
    }

    [Fact]
    public void SimulateDisconnect_FiresEvent()
    {
        var disconnected = false;
        using var source = new MockInputSource<string>();
        source.Start();
        source.Disconnected += (_, _) => disconnected = true;

        source.SimulateDisconnect();

        Assert.True(disconnected);
        Assert.False(source.IsActive);
    }

    [Fact]
    public void Reset_ClearsCount()
    {
        using var source = new MockInputSource<int>();
        source.Emit(1);
        source.Emit(2);

        source.Reset();

        Assert.Equal(0, source.EventCount);
    }
}
