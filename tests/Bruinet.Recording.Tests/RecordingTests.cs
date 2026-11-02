using System.Diagnostics;
using System.Drawing;
using Bruinet.Core.Recording;
using Xunit;

namespace Bruinet.Recording.Tests;

/// <summary>Runs only where ffmpeg is installed; skipped, saying how to get it, elsewhere.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (Ffmpeg.Find() is null)
        {
            Skip = Ffmpeg.HowToGet;
        }
    }
}

public sealed class RecordingClockTests
{
    [Fact]
    public void A_system_relative_time_is_the_stopwatch_time()
    {
        var ticks = Stopwatch.GetTimestamp();
        var systemRelative = TimeSpan.FromTicks((long)((decimal)ticks * TimeSpan.TicksPerSecond / Stopwatch.Frequency));

        Assert.InRange(RecordingClock.FromSystemRelativeTime(systemRelative) - ticks, -Stopwatch.Frequency / 1000, Stopwatch.Frequency / 1000);
    }

    [Fact]
    public void Add_and_Between_agree()
    {
        var start = RecordingClock.Now;

        var between = RecordingClock.Between(start, RecordingClock.Add(start, TimeSpan.FromMilliseconds(1500)));

        Assert.InRange(between.TotalMilliseconds, 1499.9, 1500.1);
    }
}

public sealed class RecordingTargetTests
{
    [Theory]
    [InlineData("132456", 132456L)]
    [InlineData("0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("NATIVE_APP", null)]
    public void A_window_from_a_drivers_handle(string? handle, long? expected)
    {
        Assert.Equal(expected is { } value ? new RecordingTarget.Window((nint)value) : null, RecordingTarget.ForWindow(handle));
    }
}

public sealed class ScreenRecordersTests
{
    [Fact]
    public void The_first_recorder_that_can_record_a_target_records_it()
    {
        var window = new FakeRecorder(target => target is RecordingTarget.Window);
        var anything = new FakeRecorder(_ => true);
        var recorders = new ScreenRecorders([window, anything]);

        Assert.Same(window, recorders.Find(new RecordingTarget.Window(1)));
        Assert.Same(anything, recorders.Find(new RecordingTarget.AndroidDevice("emulator-5554")));
    }

    [Fact]
    public void No_recorder_for_a_target_is_null()
    {
        Assert.Null(new ScreenRecorders([]).Find(new RecordingTarget.IosSimulator("A")));
    }

    [Fact]
    public void Discover_offers_the_window_recorder_first_and_the_region_recorder_last()
    {
        var recorders = ScreenRecorders.Discover().Recorders;

        Assert.IsType<Windows.WindowsCaptureRecorder>(recorders[0]);
        Assert.IsType<FfmpegRegionRecorder>(recorders[^1]);
    }

    private sealed class FakeRecorder(Func<RecordingTarget, bool> canRecord) : IScreenRecorder
    {
        public bool CanRecord(RecordingTarget target) => canRecord(target);

        public Task<IRecording> StartAsync(RecordingTarget target, RecordingOptions options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

public sealed class FfmpegTests
{
    [Fact]
    public void BRUINET_FFMPEG_names_the_executable()
    {
        var exe = Environment.ProcessPath!;

        Assert.Equal(exe, Ffmpeg.Find(name => name == Ffmpeg.Variable ? exe : null));
    }

    [Fact]
    public void A_named_executable_that_is_not_there_is_not_found()
    {
        Assert.Null(Ffmpeg.Find(name => name == Ffmpeg.Variable ? @"C:\nowhere\ffmpeg.exe" : null));
    }

    [Fact]
    public void Otherwise_it_is_looked_for_on_PATH()
    {
        var folder = Directory.CreateTempSubdirectory("bruinet-ffmpeg-").FullName;
        var exe = Path.Combine(folder, "ffmpeg.exe");
        File.WriteAllBytes(exe, []);
        try
        {
            Assert.Equal(exe, Ffmpeg.Find(name => name == "PATH" ? $@"C:\nowhere;""{folder}""" : null));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Not_found_says_how_to_get_it()
    {
        Assert.Contains("winget install", new FfmpegNotFoundException().Message, StringComparison.Ordinal);
    }
}

public sealed class FrameVideoTests
{
    [Fact]
    public async Task A_still_screen_is_still_in_the_video_at_the_frame_rate()
    {
        var sink = new CountingSink();
        var video = new FrameVideo(new Size(4, 2), 20, sink);
        var start = RecordingClock.Now;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        video.Offer(start, new byte[32], new Size(4, 2), 16);
        await Task.Delay(500);
        var result = await video.StopAsync("video.mp4", CancellationToken.None);
        clock.Stop();

        Assert.Equal(start, result.StartedAt);
        // Frames are written at the rate until the stop, so a busy runner's late stop adds its time.
        Assert.InRange(sink.Frames, 9, (int)Math.Ceiling(20 * clock.Elapsed.TotalSeconds) + 1);
        Assert.Equal(TimeSpan.FromSeconds(sink.Frames / 20.0), result.Duration);
        Assert.True(sink.Completed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task A_frame_of_another_size_is_cropped_and_padded_and_said()
    {
        var sink = new CountingSink();
        await using var video = new FrameVideo(new Size(2, 2), 10, sink);
        byte[] wide = [.. Enumerable.Repeat((byte)7, 3 * 1 * 4)];

        video.Offer(RecordingClock.Now, wide, new Size(3, 1), 12);
        var result = await video.StopAsync("video.mp4", CancellationToken.None);

        Assert.Equal([7, 7, 7, 7, 7, 7, 7, 7, 0, 0, 0, 0, 0, 0, 0, 0], sink.Last);
        Assert.Contains(result.Warnings, warning => warning.Contains("3x1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stopping_before_any_frame_says_nothing_was_recorded()
    {
        await using var video = new FrameVideo(new Size(2, 2), 10, new CountingSink());

        await Assert.ThrowsAsync<InvalidOperationException>(() => video.StopAsync("video.mp4", CancellationToken.None));
    }
}

internal sealed class CountingSink : IFrameSink
{
    public int Frames { get; private set; }

    public byte[] Last { get; private set; } = [];

    public bool Completed { get; private set; }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bgra, CancellationToken cancellationToken)
    {
        Frames++;
        Last = bgra.ToArray();
        return ValueTask.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        Completed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
