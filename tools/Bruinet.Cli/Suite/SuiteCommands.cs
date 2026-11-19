using System.CommandLine;
using System.Text.Json;

namespace Bruinet.Cli.Suite;

/// <summary><c>bruinet suite</c>: facts about a suite, read from its source.</summary>
internal static class SuiteCommands
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // The suite's list of places: in the app's own Scraper project (a folder of the suite with a scraper.config.md) when
    // it has one, else in the suite's .testing folder.
    private static string PlacesOf(string suite)
    {
        var name = Bruinet.Scraper.Places.PlacesList.FileName;
        return Directory.EnumerateDirectories(suite)
            .Where(folder => File.Exists(Path.Combine(folder, Bruinet.Scraper.Projects.ScraperConfig.FileName)))
            .Select(folder => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists) ?? Path.Combine(suite, ".testing", name);
    }

    public static Command Build(CliIo io) => new("suite", "Facts about a UI test suite, read from its source: its pages and what each has and lacks.")
    {
        Pages(io),
    };

    private static Command Pages(CliIo io)
    {
        var suite = new Option<string?>("--suite") { Description = "The suite's folder. Default: the folder here or above that has a .testing folder." };
        var map = new Option<string?>("--map") { Description = "A Markdown file whose tables hold the app's addresses in backticks. Default: .testing/170-Map.md when the suite has it." };
        var places = new Option<string?>("--places") { Description = $"The list of the app's places (bruinet scrape places), in place of the map: every page of it without a page object is listed. Default: .testing/{Bruinet.Scraper.Places.PlacesList.FileName} when the suite has it." };
        var write = new Option<bool>("--write") { Description = $"Write the checklist to the suite's .testing/{PagesChecklist.FileName}, keeping each row's Owed." };
        var check = new Option<bool>("--check") { Description = "Exit 1 when the written checklist is not what the source says now." };
        var json = new Option<bool>("--json") { Description = "Print the pages as JSON instead of the checklist." };
        var command = new Command("pages", $"A suite's page checklist: a row per page object with its kind, its named members, the specs and tests that visit it, and its gaps. Prints it, or writes .testing/{PagesChecklist.FileName}.")
        {
            suite, map, places, write, check, json,
        };
        command.Action(io, parsed =>
        {
            var folder = parsed.GetValue(suite) is { Length: > 0 } given
                ? Path.GetFullPath(given, io.CurrentDirectory)
                : PagesChecklist.FindSuite(io.CurrentDirectory) ?? throw new ArgumentException("No .testing folder in this folder or above it: give --suite.");
            if (!Directory.Exists(folder))
                throw new ArgumentException($"The suite's folder {folder} does not exist.");
            var mapFile = parsed.GetValue(map) is { Length: > 0 } givenMap ? Path.GetFullPath(givenMap, io.CurrentDirectory) : Path.Combine(folder, ".testing", "170-Map.md");
            if (parsed.GetValue(map) is { Length: > 0 } && !File.Exists(mapFile))
                throw new ArgumentException($"The map {mapFile} does not exist.");
            var placesFile = parsed.GetValue(places) is { Length: > 0 } givenPlaces ? Path.GetFullPath(givenPlaces, io.CurrentDirectory) : PlacesOf(folder);
            if (parsed.GetValue(places) is { Length: > 0 } && !File.Exists(placesFile))
                throw new ArgumentException($"The list of places {placesFile} does not exist.");
            // The list of places holds the map's addresses and more: where the suite has one, and no map is asked for, it is what the pages are held against.
            var pages = File.Exists(placesFile) && parsed.GetValue(map) is not { Length: > 0 }
                ? SuitePages.Read(folder, null, PlacesAsMap(Bruinet.Scraper.Places.PlacesList.Load(placesFile))) with { AddressesFrom = "the list of places" }
                : SuitePages.Read(folder, File.Exists(mapFile) ? mapFile : null);
            if (parsed.GetValue(json))
            {
                io.Out.WriteLine(JsonSerializer.Serialize(pages, Json));
                return 0;
            }

            var file = Path.Combine(folder, ".testing", PagesChecklist.FileName);
            var written = File.Exists(file) ? File.ReadAllText(file) : null;
            var markdown = PagesChecklist.Write(pages, written is null ? new Dictionary<string, string>(StringComparer.Ordinal) : PagesChecklist.Owed(written), out var dropped);
            if (parsed.GetValue(check))
            {
                if (written is not null && written.Replace("\r\n", "\n", StringComparison.Ordinal) == markdown)
                {
                    io.Error.WriteLine($"{file} is what the source says: {pages.Pages.Count} pages.");
                    return 0;
                }

                io.Error.WriteLine(written is null
                    ? $"{file} does not exist: write it with bruinet suite pages --write."
                    : $"{file} is not what the source says now: write it again with bruinet suite pages --write.");
                return 1;
            }

            if (!parsed.GetValue(write))
            {
                io.Out.Write(markdown);
                return 0;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var lineEnd = written?.Contains("\r\n", StringComparison.Ordinal) == true ? "\r\n" : "\n";
            File.WriteAllText(file, lineEnd == "\n" ? markdown : markdown.Replace("\n", lineEnd, StringComparison.Ordinal));
            io.Error.WriteLine($"{pages.Pages.Count} pages written to {file}; {pages.Pages.Count(page => page.Gaps.Count > 0)} with a gap.");
            foreach (var page in dropped)
                io.Error.WriteLine($"Dropped what was owed for {page}: it is a page no more.");
            return 0;
        });
        return command;
    }

    // The pages of a list of places as a map's table: what each is known from and how far the walk is with it, then its address.
    private static string PlacesAsMap(Bruinet.Scraper.Places.PlacesList list)
    {
        var text = new System.Text.StringBuilder();
        foreach (var place in list.Places.Where(place => place.Kind == Bruinet.Scraper.Places.PlaceKind.Page && place.State != Bruinet.Scraper.Places.PlaceState.Refused && place.Note?.StartsWith("lands on ", StringComparison.Ordinal) != true))
        {
            var state = place.State switch
            {
                Bruinet.Scraper.Places.PlaceState.Known => place.Address is null ? "not opened: no address" : "not opened",
                Bruinet.Scraper.Places.PlaceState.FormNotSaved => "a form, not saved",
                Bruinet.Scraper.Places.PlaceState.Saved => "a form, saved",
                _ => "seen",
            };
            text.Append("| ").Append(state).Append(", from ").AppendJoin(", ", place.Sources).Append(" | `").Append(place.Key).Append("` |\n");
        }

        return text.ToString();
    }
}
