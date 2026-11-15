using System.IO.Pipes;
using Bruinet.Bdd.Runner;
using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>The messages between a host and the runner process, and their framing.</summary>
public sealed class BddRunnerChannelTests
{
    private static readonly BddRunnerClips Clips = new(
        new BddRunnerClip("Hello.", @"C:\scene\intro.wav", 1.5),
        new Dictionary<int, BddRunnerClip> { [12] = new("Tap Sync.", null, 0.8) },
        null);

    public static TheoryData<BddRunnerMessage> Messages =>
    [
        new BddRunnerHello(BddRunnerProtocol.Version),
        new BddRunnerOpen(@"C:\suite\bdd.config.md", @"C:\suite\bin\Suite.dll", @"C:\suite\Features\a.feature.md", "Finn's \"first\" scenario"),
        new BddRunnerOpened(7, "Discovery report:\r\n- a", "Command catalog:"),
        new BddRunnerNext(),
        new BddRunnerSkip("Skipped by Run from here."),
        new BddRunnerSkip(null),
        new BddRunnerRecord(2, "Sync", "Features/a.feature.md#Sync", @"C:\rec\scene-02", "abc", Clips, 400, true),
        new BddRunnerRehearse(Clips, 250, false),
        new BddRunnerFinishRecording(),
        new BddRunnerDropRecording(),
        new BddRunnerRecorded(@"C:\rec\scene-02\timeline.json", ["No recorder here."]),
        new BddRunnerSay(@"C:\scene\intro.wav", 1.5),
        new BddRunnerClose(),
        new BddRunnerCancel(),
        new BddRunnerExit(),
        new BddRunnerDone(),
        new BddRunnerFailed("No app here.\nReally."),
    ];

    [Theory]
    [MemberData(nameof(Messages))]
    public void AMessage_IsOneLine_AndReadsBackAsItself(BddRunnerMessage message)
    {
        var line = BddRunnerChannel.Write(message);

        Assert.DoesNotContain('\n', line);
        var read = BddRunnerChannel.Read(line);
        Assert.Equal(message.GetType(), read.GetType());
        Assert.Equal(line, BddRunnerChannel.Write(read));
    }

    [Fact]
    public void AStep_KeepsItsStatusItsReportAndItsTrace()
    {
        var step = new BddRunnerStep(3, BddStepResultStatus.Failed, "Expected 2 rows.", true, ["1 Passed", "4 Failed"], ["Page: Main"]);

        var read = Assert.IsType<BddRunnerStep>(BddRunnerChannel.Read(BddRunnerChannel.Write(step)));

        Assert.Equal((3, BddStepResultStatus.Failed, "Expected 2 rows.", true), (read.Index, read.Status, read.Message, read.HasNext));
        Assert.Equal(step.Report, read.Report);
        Assert.Equal(step.Diagnostics, read.Diagnostics);
    }

    [Fact]
    public void ARecording_KeepsItsClipsByLine()
    {
        var read = Assert.IsType<BddRunnerRecord>(BddRunnerChannel.Read(BddRunnerChannel.Write(new BddRunnerRecord(2, "Sync", "a#Sync", "folder", null, Clips, 400, true))));

        Assert.Equal(Clips.Intro, read.Clips.Intro);
        Assert.Equal(new BddRunnerClip("Tap Sync.", null, 0.8), read.Clips.Says[12]);
        Assert.Null(read.Clips.Outro);
        Assert.Null(read.Hash);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"type\":\"fromTheFuture\"}")]
    [InlineData("{\"steps\":1}")]
    public void ALineThatIsNoMessage_SaysSo(string line)
    {
        var exception = Assert.Throws<InvalidDataException>(() => BddRunnerChannel.Read(line));

        Assert.Contains(line, exception.Message);
    }

    [Fact]
    public async Task MessagesCrossAPipe_InOrder_AndTheEndReadsAsNull()
    {
        using var server = new AnonymousPipeServerStream(PipeDirection.Out);
        using var client = new AnonymousPipeClientStream(PipeDirection.In, server.ClientSafePipeHandle);
        var sender = new BddRunnerChannel(Stream.Null, server);
        using var receiver = new BddRunnerChannel(client, Stream.Null);

        await sender.SendAsync(new BddRunnerOpened(2, "d", "c"));
        await sender.SendAsync(new BddRunnerStep(0, BddStepResultStatus.Passed, null, true, [], []));
        sender.Dispose();

        Assert.IsType<BddRunnerOpened>(await receiver.ReceiveAsync());
        Assert.Equal(0, Assert.IsType<BddRunnerStep>(await receiver.ReceiveAsync()).Index);
        Assert.Null(await receiver.ReceiveAsync());
    }

    [Fact]
    public void TheSameVersion_IsNoMismatch()
        => Assert.Null(BddRunnerProtocol.Mismatch(BddRunnerProtocol.Version));

    [Fact]
    public void AnOlderRunner_SaysRebuildTheSuite()
        => Assert.Contains("Rebuild the suite", BddRunnerProtocol.Mismatch(BddRunnerProtocol.Version - 1));

    [Fact]
    public void ANewerRunner_SaysUpdateTheHost()
        => Assert.Contains("Update this host", BddRunnerProtocol.Mismatch(BddRunnerProtocol.Version + 1));
}
