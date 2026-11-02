using System.CommandLine;
using Bruinet.Cli.Sql;
using Bruinet.Mocking.Http;

namespace Bruinet.Cli.Http;

/// <summary>
/// <c>bruinet http</c>: the review of a suite's HTTP recordings (AI11), as <c>bruinet sql</c>'s: which are stale after a
/// replay, what a refresh changed against git, and the gate on recordings that went in unread.
/// </summary>
internal static class HttpCommands
{
    public static Command Build(CliIo io) => new("http", "HTTP replay's recordings: the stale ones, what a refresh changed, and the gate on recordings that went in unread.")
    {
        StaleCommand(io),
        DiffCommand(io),
        ReviewedCommand(io),
    };

    private static Option<string> Recordings() => new("--recordings") { Description = "The folder of HTTP recordings (Recordings/http).", Required = true };

    private static Command StaleCommand(CliIo io)
    {
        var recordings = Recordings();
        var command = new Command("stale", "The recordings whose newest replay missed a call or left recorded calls unused: the tests to record again. Exit 1 when any.") { recordings };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var stale = HttpRecordingReview.Stale(Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory));
            foreach (var line in stale)
            {
                io.Out.WriteLine(line);
            }

            io.Error.WriteLine(stale.Count == 0 ? "no recording stale" : $"{stale.Count} recordings stale: record them again (BRUINET_HTTP_REPLAY=Refresh)");
            return Task.FromResult(stale.Count == 0 ? 0 : 1);
        });
        return command;
    }

    private static Command DiffCommand(CliIo io)
    {
        var recordings = Recordings();
        var since = new Option<string>("--base") { Description = "The git revision to compare with.", DefaultValueFactory = _ => "HEAD" };
        var accept = new Option<bool>("--accept") { Description = "After the summary, accept every recording as it is now (reviewed.json)." };
        var command = new Command("diff", "What each re-recorded test's calls changed against git: calls added and gone, statuses and answers. --accept marks them read.") { recordings, since, accept };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory);
            var revision = parsed.GetValue(since)!;
            var root = SqlCommands.GitText(folder, "rev-parse", "--show-toplevel").Trim();
            var prefix = Path.GetRelativePath(root, folder).Replace('\\', '/').TrimEnd('/') + "/";
            var before = SqlCommands.GitText(folder, "ls-tree", "-r", "--name-only", "--full-name", revision, "--", ":/" + prefix)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".calls.json", StringComparison.OrdinalIgnoreCase))
                .Select(path => path[prefix.Length..])
                .ToList();
            var changed = 0;
            foreach (var path in HttpRecordingReview.Recordings(folder).Union(before, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                var now = Path.Combine(folder, path);
                var nowJson = File.Exists(now) ? File.ReadAllText(now) : null;
                // "--": the argument is a revision, never a file to look for (a scenario's long name passes Windows' path limit).
                var oldJson = before.Contains(path, StringComparer.Ordinal) ? SqlCommands.GitText(folder, "show", $"{revision}:{prefix}{path}", "--") : null;
                if (nowJson is not null && !HttpRecordingReview.IsRecording(nowJson) || oldJson is not null && !HttpRecordingReview.IsRecording(oldJson)
                    || nowJson is not null && oldJson is not null && HttpRecordingReview.Hash(nowJson) == HttpRecordingReview.Hash(oldJson))
                {
                    continue;
                }

                var lines = HttpRecordingDiff.Compare(oldJson is null ? null : HttpRecording.Parse(oldJson), nowJson is null ? null : HttpRecording.Parse(nowJson));
                changed++;
                io.Out.WriteLine(path);
                foreach (var line in lines.DefaultIfEmpty("rewritten, no answer changed"))
                {
                    io.Out.WriteLine("  " + line);
                }
            }

            io.Error.WriteLine($"{changed} recordings changed since {revision}");
            if (parsed.GetValue(accept))
            {
                HttpRecordingReview.Accept(folder);
                io.Error.WriteLine($"accepted: wrote {Path.Combine(folder, HttpRecordingReview.FileName)}");
            }

            return Task.FromResult(0);
        });
        return command;
    }

    private static Command ReviewedCommand(CliIo io)
    {
        var recordings = Recordings();
        var command = new Command("reviewed", "The gate: HTTP recordings that went in unread (changed since bruinet http diff --accept). Exit 1 when any.") { recordings };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var unread = HttpRecordingReview.Unreviewed(Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory));
            foreach (var line in unread)
            {
                io.Out.WriteLine(line);
            }

            io.Error.WriteLine(unread.Count == 0 ? "every recording reviewed" : $"{unread.Count} recordings unread: bruinet http diff --recordings <folder>, then --accept");
            return Task.FromResult(unread.Count == 0 ? 0 : 1);
        });
        return command;
    }
}
