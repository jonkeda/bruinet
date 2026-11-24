using System.CommandLine;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Record;
using static Bruinet.Scraper.Host.HostOptions;

namespace Bruinet.Scraper.Host;

public static partial class ScrapeCommands
{
    private static Command Record(HostIo io)
    {
        var folder = new Option<string?>("--folder") { Description = "The Scraper project to save in. Default: this folder when it has a scraper.config.md, else what the Recorder has open." };
        var url = new Option<string?>("--url") { Description = "The address to open. Default: where the project's recorder.settings.json left off." };
        var account = new Option<string?>("--as") { Description = "Whose sign-in the Recorder keeps (a name, as Admin). Only when it starts; default: one of its own." };
        var json = Json();
        var command = new Command("record", "Open the Recorder: a window where a person clicks through the app and the pages and presses are recorded. It opens not recording.")
        {
            folder, url, account, json, RecorderAsk(io, "status", "How the Recorder is: recording or not, its folder, address, session and counts."),
            RecorderAsk(io, "start", "Start recording, as the window's button does."), RecorderAsk(io, "stop", "Stop recording, as the window's button does."), RecorderWait(io),
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var given = parsed.GetValue(folder) is { Length: > 0 } named ? Path.GetFullPath(named, io.CurrentDirectory)
                : File.Exists(Path.Combine(io.CurrentDirectory, ScraperConfig.FileName)) ? io.CurrentDirectory
                : null;
            var (status, problem) = await RecorderLauncher.OpenAsync(given, parsed.GetValue(url), parsed.GetValue(account), io.CurrentDirectory, cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                await io.Error.WriteLineAsync($"bruinet: {problem}").ConfigureAwait(false);
                return 2;
            }

            return await RecorderSaysAsync(io, status, parsed.GetValue(json)).ConfigureAwait(false);
        });
        return command;
    }

    private static Command RecorderAsk(HostIo io, string what, string description)
    {
        var json = Json();
        var command = new Command(what, description) { json };
        command.Action(io, async (parsed, cancellationToken) =>
            await RecorderSaysAsync(io, await RecorderLink.AskAsync(new RecorderRequest(what), cancellationToken).ConfigureAwait(false), parsed.GetValue(json)).ConfigureAwait(false));
        return command;
    }

    private static Command RecorderWait(HostIo io)
    {
        var minutes = new Option<double>("--minutes") { Description = "How long to wait at most.", DefaultValueFactory = _ => 10 };
        var json = Json();
        var command = new Command("wait", "Wait until the person stops a recording: the one that is on, or the next. Exit 1 when the time ran out first.") { minutes, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var status = await RecorderLink.AskAsync(new RecorderRequest("wait", Minutes: parsed.GetValue(minutes)), cancellationToken).ConfigureAwait(false);
            var code = await RecorderSaysAsync(io, status, parsed.GetValue(json)).ConfigureAwait(false);
            return code == 0 && status is { Ended: false } ? 1 : code;
        });
        return command;
    }

    // 0 when the Recorder did what was asked, 1 when it said why not, 2 when none is running.
    private static async Task<int> RecorderSaysAsync(HostIo io, RecorderStatus? status, bool json)
    {
        if (status is null)
        {
            await io.Error.WriteLineAsync("bruinet: no Recorder is running. Open one with bruinet scrape record.").ConfigureAwait(false);
            return 2;
        }

        if (json)
        {
            await io.Out.WriteLineAsync(ToJson(status)).ConfigureAwait(false);
        }
        else
        {
            await io.Out.WriteLineAsync($"{(status.Recording ? (status.Values ? "Recording, with values" : "Recording") : status.Ended ? "The recording ended" : "Not recording")}: {status.Pages} pages, {status.Presses} presses{(status.Session is null ? "" : $" in {status.Session}")}.").ConfigureAwait(false);
            await io.Out.WriteLineAsync($"Folder:  {status.Folder ?? "(none: choose one in the window, or pass --folder)"}").ConfigureAwait(false);
            await io.Out.WriteLineAsync($"Address: {status.Address}").ConfigureAwait(false);
        }

        if (status.Problem is null)
            return 0;
        await io.Error.WriteLineAsync(status.Problem).ConfigureAwait(false);
        return 1;
    }
}
