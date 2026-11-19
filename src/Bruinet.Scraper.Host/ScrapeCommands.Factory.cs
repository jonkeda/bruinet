using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using static Bruinet.Scraper.Host.HostOptions;

namespace Bruinet.Scraper.Host;

// bruinet scrape factory: the loops that take an app's places to page objects and tests, group by group.
public static partial class ScrapeCommands
{
    private static Command Factory(HostIo io) => new("factory", "The factory: an app's places to page objects and tests, a small group at a time. What is left, by group.")
    {
        FactoryStatus(io), FactoryReplay(io), FactoryBench(io),
    };

    private static Command FactoryReplay(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("replay", "Read every kept place again and draft again every class that was drafted from one, with the signs, holders and templates as they are now, and say what differs from the list and the suite. Writes nothing and opens nothing: the measure of a change. Exit 1 when something differs.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.ReplayPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return report.Same ? 0 : 1;
            }

            foreach (var field in report.Fields)
                await io.Out.WriteLineAsync($"field  {field.Place}: {field.Before ?? "(not read)"} -> {field.After ?? "(not read)"}").ConfigureAwait(false);
            foreach (var change in report.Changed)
            {
                var what = string.Join("; ", new[] { change.Added.Count > 0 ? "now also " + string.Join(", ", change.Added) : "", change.Removed.Count > 0 ? "no longer " + string.Join(", ", change.Removed) : "", change.Note }.Where(part => part.Length > 0));
                await io.Out.WriteLineAsync($"class  {change.Class} ({change.Place}): {what}").ConfigureAwait(false);
            }

            foreach (var reason in report.CannotDraft.GroupBy(each => each.Reason, StringComparer.Ordinal).OrderByDescending(group => group.Count()))
                await io.Out.WriteLineAsync($"cannot draft {reason.Count()}: {reason.Key} (for example {reason.First().Place})").ConfigureAwait(false);
            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{report.Places} places read again and {report.Classes} classes drafted again in {report.Seconds} s: {(report.Same ? "nothing changed" : $"{report.Fields.Count} fields read differently ({report.ControlsGained} gained a control, {report.ControlsLost} lost one), {report.Changed.Count} classes would be drafted differently")}. Of the places that owe a class, {report.WouldDraft} can be drafted now and {report.CannotDraft.Count} cannot.")).ConfigureAwait(false);
            return report.Same ? 0 : 1;
        });
        return command;
    }

    private static Command FactoryBench(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("bench", "Hold the examples (## Examples of the config: a place and the class a person wrote for it) against what their places read as now, and say for each difference who is right. Exit 1 when an example names something the page shows and the reading misses.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.BenchPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return report.Missed > 0 || report.Problems.Count > 0 ? 1 : 0;
            }

            foreach (var difference in report.Differences)
            {
                var says = difference.Verdict switch
                {
                    BenchVerdict.DraftIsRight => "the place shows it, the example does not name it",
                    BenchVerdict.ExampleIsRight => "the example names it, the page shows it, the reading misses it",
                    _ => "the example names it, the capture does not show it",
                };
                await io.Out.WriteLineAsync($"{difference.Class} ({difference.Place}): '{difference.Label}': {says}").ConfigureAwait(false);
            }

            foreach (var problem in report.Problems)
                await io.Out.WriteLineAsync(problem).ConfigureAwait(false);
            await io.Error.WriteLineAsync(report.Examples == 0
                ? "No examples: name a place and its class under ## Examples in the config."
                : string.Create(CultureInfo.InvariantCulture, $"{report.Examples} examples: {report.Missed} missed by the reading, {report.Differences.Count(d => d.Verdict == BenchVerdict.DraftIsRight)} where the place shows more than the example, {report.Differences.Count(d => d.Verdict == BenchVerdict.NotOnThePage)} the capture does not show.")).ConfigureAwait(false);
            return report.Missed > 0 || report.Problems.Count > 0 ? 1 : 0;
        });
        return command;
    }

    private static Command FactoryStatus(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("status", "What the places of the app still owe, by group: pages and dialog forms without a class, pages of one record, fields without a control, forms not saved. Exit 1 when something is owed.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.OwedPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            var owed = report.PagesWithoutClass + report.DialogFormsNotDrafted + report.RecordPages;
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return owed > 0 ? 1 : 0;
            }

            await io.Out.WriteLineAsync("| Group | Kinds | Pages | Pages without a class | Dialog forms not drafted | Record pages (need a record) | Fields without a control | Forms not saved |").ConfigureAwait(false);
            await io.Out.WriteLineAsync("| --- | --- | --- | --- | --- | --- | --- | --- |").ConfigureAwait(false);
            foreach (var group in report.Groups)
            {
                await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                    $"| `{group.Group}` | {string.Join(", ", group.Kinds)} | {group.Pages} | {group.PagesWithoutClass.Count} | {group.DialogFormsNotDrafted.Count} | {group.RecordPages.Count} | {group.FieldsWithoutControl} | {group.FormsNotSaved} |")).ConfigureAwait(false);
            }

            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{report.Places} places, {report.Kept} with a kept capture. Owed: {report.PagesWithoutClass} pages without a class, {report.DialogFormsNotDrafted} dialog forms not drafted, {report.RecordPages} pages of one record. {report.FieldsWithControl} of {report.Fields} fields have a made control. {report.FormsNotSaved} forms not saved.")).ConfigureAwait(false);
            return owed > 0 ? 1 : 0;
        });
        return command;
    }
}
