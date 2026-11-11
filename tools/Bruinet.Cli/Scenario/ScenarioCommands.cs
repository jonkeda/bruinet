using System.CommandLine;
using Bruinet.Bdd.LanguageServer;
using Bruinet.Bdd.Preview;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Scenario;

/// <summary><c>bruinet scenario</c>: feature files through the same server methods an IDE uses.</summary>
internal static class ScenarioCommands
{
    public static Command Build(CliIo io) => new("scenario", "Feature files: a preview page, why a scenario fails, and deleting old results.")
    {
        Preview(io),
        WhyCommand.Build(io),
        new Command("results", "A scenario's, a feature file's or a suite's run results.") { DeleteResults(io) },
    };

    // One file written as a whole page, for a look without an IDE.
    private static Command Preview(CliIo io)
    {
        var file = new Argument<string>("file") { Description = "The feature or step file." };
        var solution = Solution();
        var suite = new Option<string?>("--suite") { Description = "The suite to bind against, for a file several suites link." };
        var output = new Option<string?>("--out") { Description = "Write the page here. Default: stdout." };
        var host = new Option<bool>("--host") { Description = "The IDE's host page with the preview posted into it, as a webview shows it." };
        var command = new Command("preview", "Render a feature file's preview as an HTML page.") { file, solution, suite, output, host };
        command.Action(io, parsed =>
        {
            var path = Path.GetFullPath(parsed.GetValue(file)!, io.CurrentDirectory);
            if (!File.Exists(path))
                throw new FileNotFoundException($"No file {path}.");
            using var server = Server(SolutionFolder(parsed.GetValue(solution), Path.GetDirectoryName(path)!), images: true);
            var preview = server.Preview(new PreviewParams(new Uri(path).AbsoluteUri, parsed.GetValue(suite)));
            var page = parsed.GetValue(host)
                ? BddPreview.HostPage.Replace("</body>", $"<script>window.postMessage({System.Text.Json.JsonSerializer.Serialize(new { type = "update", html = preview.Html, suites = preview.Suites, suite = preview.Suite, bindState = preview.BindState, reason = preview.Reason })});</script>\n</body>", StringComparison.Ordinal)
                : BddPreview.Page(new BddPreviewResult(preview.Html, []), Path.GetFileName(path));
            if (parsed.GetValue(output) is { } to)
            {
                File.WriteAllText(Path.GetFullPath(to, io.CurrentDirectory), page);
                io.Error.WriteLine($"{preview.BindState} in {preview.Suite ?? "no suite"}{(preview.Reason is { } reason ? $": {reason}" : string.Empty)}; wrote {to}");
            }
            else
            {
                io.Out.Write(page);
            }

            return 0;
        });
        return command;
    }

    private static Command DeleteResults(CliIo io)
    {
        var file = new Argument<string?>("file[:line]") { Description = "The feature file whose results go; with :line, only the scenario whose heading is on that line. Leave out with --suite.", Arity = ArgumentArity.ZeroOrOne };
        var suite = new Option<string?>("--suite") { Description = "Every result of this suite (or the suite picked for a linked file)." };
        var (solution, yes, json) = (Solution(), Yes(), Json());
        var command = new Command("delete", "Delete old results from every run, for good: reports and screenshots; a run left empty goes.") { file, suite, solution, yes, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var (path, line) = parsed.GetValue(file) is { } given ? Place(given, io.CurrentDirectory) : (null, null);
            var scope = path is null ? "suite" : line is null ? "file" : "scenario";
            if (scope == "suite" && parsed.GetValue(suite) is null)
                throw new ArgumentException("Say what to delete: a feature file, or --suite.");
            using var server = Server(SolutionFolder(parsed.GetValue(solution), path is null ? io.CurrentDirectory : Path.GetDirectoryName(path)!), images: false);
            DeleteResultsResult Delete(bool dryRun) =>
                server.DeleteResults(new DeleteResultsParams(scope, path is null ? null : new Uri(path).AbsoluteUri, line, parsed.GetValue(suite), dryRun));

            var count = Delete(dryRun: true);
            if (!count.Done)
                throw new ArgumentException(count.Reason ?? "Nothing to delete.");
            await io.Error.WriteLineAsync($"{count.What} in {count.Suite}: {count.FilesDeleted} files, {count.RunsDeleted} runs left empty.").ConfigureAwait(false);
            if (count.FilesDeleted == 0)
            {
                // Under --json, never an empty stdout: the count that found nothing.
                if (parsed.GetValue(json))
                    await io.Out.WriteLineAsync(ToJson(count)).ConfigureAwait(false);
                return 0;
            }

            if (!parsed.GetValue(yes) && !await ConfirmAsync(io, "Delete them?", cancellationToken).ConfigureAwait(false))
            {
                await io.Error.WriteLineAsync("Nothing deleted.").ConfigureAwait(false);
                return 2;
            }

            var done = Delete(dryRun: false);
            await io.Out.WriteLineAsync(parsed.GetValue(json)
                ? ToJson(done)
                : $"Deleted {done.FilesDeleted} files; {done.RunsDeleted} runs removed, {done.RunsChanged} kept other results{(done.Skipped.Count > 0 ? $"; {done.Skipped.Count} runs skipped" : "")}.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static BddLanguageServer Server(string solution, bool images)
    {
        var server = new BddLanguageServer { FileImageUrls = images };
        server.Initialize(new InitializeParams(null, null, new InitializationOptions(solution)));
        return server;
    }
}
