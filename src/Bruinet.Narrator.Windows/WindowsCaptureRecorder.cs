using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

[assembly: Bruinet.Recording.ScreenRecorder(typeof(Bruinet.Recording.Windows.WindowsCaptureRecorder))]

namespace Bruinet.Recording.Windows;

/// <summary>
/// Records a window or a monitor through Windows.Graphics.Capture: frames from the compositor, so a
/// window behind others still records as it looks. Not input (AD-005): no focus, no foreground. A
/// minimized window gives no frames.
/// </summary>
public sealed class WindowsCaptureRecorder : IScreenRecorder
{
    private readonly Func<string, Size, int, IFrameSink> _sink;

    /// <summary>Creates the recorder; frames are encoded by ffmpeg over a pipe.</summary>
    public WindowsCaptureRecorder()
        : this((path, size, fps) => new FfmpegFrameSink(path, size, fps))
    {
    }

    internal WindowsCaptureRecorder(Func<string, Size, int, IFrameSink> sink) => _sink = sink;

    /// <inheritdoc />
    public bool CanRecord(RecordingTarget target)
        => target is RecordingTarget.Window or RecordingTarget.Monitor { Crop: null }
           && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
           && GraphicsCaptureSession.IsSupported();

    /// <inheritdoc />
    public async Task<IRecording> StartAsync(RecordingTarget target, RecordingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!CanRecord(target))
        {
            throw new NotSupportedException($"{nameof(WindowsCaptureRecorder)} cannot record {target} here.");
        }

        var item = target switch
        {
            RecordingTarget.Window window => Direct3D.ForWindow(window.Handle),
            RecordingTarget.Monitor monitor => Direct3D.ForMonitor(monitor.Handle),
            _ => throw new NotSupportedException(target.ToString()),
        };
        var recording = new Recording(item, options, _sink, target is RecordingTarget.Window shown ? TryWindowArea(shown.Handle) : null);
        try
        {
            await recording.FirstFrame.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            return recording;
        }
        catch (TimeoutException exception)
        {
            await recording.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"No frame came from {target} in 10 seconds: is the window minimized?", exception);
        }
        catch
        {
            await recording.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // What a window's frames show: its visible frame (DWM's extended frame bounds), in physical
    // pixels whatever this process's DPI awareness, as the Windows driver reports its interactions.
    private static Rectangle? TryWindowArea(nint window)
        => DwmGetWindowAttribute(window, ExtendedFrameBounds, out var frame, Marshal.SizeOf<Rect>()) == 0
            ? Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom)
            : null;

    private const int ExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect value, int size);

    private sealed class Recording : IRecording
    {
        private const DirectXPixelFormat Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;

        private readonly IDirect3DDevice _device;
        private readonly Direct3D11CaptureFramePool _pool;
        private readonly GraphicsCaptureSession _session;
        private readonly FrameVideo _video;
        private readonly string _path;
        private readonly Rectangle? _source;
        private SizeInt32 _poolSize;
        private byte[] _pixels = [];
        private bool _stopped;

        public Recording(GraphicsCaptureItem item, RecordingOptions options, Func<string, Size, int, IFrameSink> sink, Rectangle? source)
        {
            _path = options.OutputPath;
            _source = source;
            _device = Direct3D.CreateDevice();
            _poolSize = item.Size;
            var size = FfmpegProcess.Even(new Size(item.Size.Width, item.Size.Height));
            _video = new FrameVideo(size, options.FramesPerSecond, sink(options.OutputPath, size, options.FramesPerSecond));
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, Format, 2, _poolSize);
            _pool.FrameArrived += OnFrameArrived;
            _session = _pool.CreateCaptureSession(item);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                _session.IsCursorCaptureEnabled = options.Cursor;
            }

            _session.StartCapture();
        }

        public Task<long> FirstFrame => _video.FirstFrame;

        public long StartedAt => _video.FirstFrame.IsCompletedSuccessfully ? _video.FirstFrame.Result : 0;

        public async Task<RecordingResult> StopAsync(CancellationToken cancellationToken = default)
        {
            StopCapture();
            return await _video.StopAsync(_path, cancellationToken).ConfigureAwait(false) with { Source = _source };
        }

        public async ValueTask DisposeAsync()
        {
            StopCapture();
            await _video.DisposeAsync().ConfigureAwait(false);
            _device.Dispose();
        }

        private void StopCapture()
        {
            lock (_pool)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _pool.FrameArrived -= OnFrameArrived;
                _session.Dispose();
                _pool.Dispose();
            }
        }

        // On the pool's own thread: copy the frame to memory and hand it to the video.
        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (_pool)
            {
                if (_stopped)
                {
                    return;
                }

                using var frame = sender.TryGetNextFrame();
                if (frame is null)
                {
                    return;
                }

                var time = RecordingClock.FromSystemRelativeTime(frame.SystemRelativeTime);
                using (var bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Premultiplied).AsTask().GetAwaiter().GetResult())
                {
                    var length = bitmap.PixelWidth * bitmap.PixelHeight * 4;
                    if (_pixels.Length != length)
                    {
                        _pixels = new byte[length];
                    }

                    bitmap.CopyToBuffer(_pixels.AsBuffer());
                    var content = new Size(
                        Math.Min(frame.ContentSize.Width, bitmap.PixelWidth),
                        Math.Min(frame.ContentSize.Height, bitmap.PixelHeight));
                    _video.Offer(time, _pixels, content, bitmap.PixelWidth * 4);
                }

                if (frame.ContentSize.Width != _poolSize.Width || frame.ContentSize.Height != _poolSize.Height)
                {
                    _poolSize = frame.ContentSize;
                    _pool.Recreate(_device, Format, 2, _poolSize);
                }
            }
        }
    }
}
