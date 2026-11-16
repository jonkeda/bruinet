using System.Diagnostics;
using System.Drawing;
using Bruinet.Core.Recording;
using Bruinet.Recording;

[assembly: ScreenRecorder(typeof(Bruinet.Appium.IosRecorder), Order = 21)]

namespace Bruinet.Appium;

/// <summary>
/// Records an iOS simulator with <c>xcrun simctl io recordVideo</c>, in one file for as long as the
/// scene lasts. It runs on a Mac only, where the simulator does.
/// </summary>
public sealed class IosRecorder : IScreenRecorder
{
    // How long recordVideo has to finish its file after SIGINT, and to start.
    private static readonly TimeSpan FinishTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public bool CanRecord(RecordingTarget target) => target is RecordingTarget.IosSimulator && OperatingSystem.IsMacOS();

    /// <inheritdoc />
    public async Task<IRecording> StartAsync(RecordingTarget target, RecordingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (target is not RecordingTarget.IosSimulator simulator || !OperatingSystem.IsMacOS())
        {
            throw new NotSupportedException($"{nameof(IosRecorder)} records an iOS simulator on a Mac, not {target}.");
        }

        var (width, height, scale) = IosHost.Screen(simulator.Udid);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
        var info = new ProcessStartInfo("xcrun")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in (string[])["simctl", "io", simulator.Udid, "recordVideo", "--codec=h264", "--force", options.OutputPath])
        {
            info.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = info };
        var started = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is not { } text)
            {
                return;
            }

            lock (errors)
            {
                errors.AppendLine(text);
            }

            // simctl prints this once its first frame is captured: the video's time zero.
            if (text.Contains("Recording started", StringComparison.Ordinal))
            {
                started.TrySetResult(RecordingClock.Now);
            }
        };
        process.OutputDataReceived += (_, _) => { };
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        try
        {
            var startedAt = await started.Task.WaitAsync(StartTimeout, cancellationToken).ConfigureAwait(false);
            return new Recording(process, options.OutputPath, new Size(width, height), scale, startedAt, errors);
        }
        catch (TimeoutException timeout)
        {
            process.Kill();
            process.Dispose();
            throw new InvalidOperationException($"simctl recordVideo did not start in {StartTimeout.TotalSeconds:0} s: {errors.ToString().Trim()}", timeout);
        }
    }

    /// <summary>The screen in points, which an element's bounds are in, for a screen of <paramref name="pixels"/>.</summary>
    internal static Rectangle Points(Size pixels, double scale)
        => new(Point.Empty, new Size((int)Math.Round(pixels.Width / scale), (int)Math.Round(pixels.Height / scale)));

    private sealed class Recording(Process process, string outputPath, Size size, double scale, long startedAt, System.Text.StringBuilder errors) : IRecording
    {
        public long StartedAt { get; } = startedAt;

        public async Task<RecordingResult> StopAsync(CancellationToken cancellationToken = default)
        {
            var stoppedAt = RecordingClock.Now;

            // recordVideo finishes its file on SIGINT; killing it would leave the file unreadable.
            using (var signal = Process.Start(new ProcessStartInfo("kill") { ArgumentList = { "-INT", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, UseShellExecute = false }))
            {
                if (signal is not null)
                {
                    await signal.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(FinishTimeout);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            if (!File.Exists(outputPath))
            {
                lock (errors)
                {
                    throw new InvalidOperationException($"simctl recordVideo left no video at {outputPath}: {errors.ToString().Trim()}");
                }
            }

            string[] warnings = [];
            return new RecordingResult(outputPath, RecordingClock.Between(StartedAt, stoppedAt), size, StartedAt, warnings)
            {
                Source = Points(size, scale),
            };
        }

        public ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
