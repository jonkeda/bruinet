using System.Text.Json;
using System.Text.Json.Serialization;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Places;

/// <summary>
/// The places of an app, kept between runs: what the map, the suite, the recordings, the app's code and the walk each
/// found, added up. The places that are known and not yet opened are its frontier: a walk takes from it and puts back
/// what it found, so a walk that stops loses one place and the next goes on.
/// </summary>
public sealed class PlacesList
{
    /// <summary>The list's file in a suite's <c>.testing</c> folder.</summary>
    public const string FileName = "175-Places.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SortedDictionary<string, Place> _places = new(StringComparer.Ordinal);

    /// <summary>The places, by key.</summary>
    public IReadOnlyCollection<Place> Places => _places.Values;

    /// <summary>The place of a key, or null.</summary>
    public Place? this[string key] => _places.GetValueOrDefault(key);

    /// <summary>The places the walk can open next, pages first seen by nobody, in the order of their keys.</summary>
    public IEnumerable<Place> Frontier => _places.Values.Where(place => place.OnFrontier);

    /// <summary>Reads a list; an empty one when the file is not there.</summary>
    public static PlacesList Load(string path)
    {
        var list = new PlacesList();
        if (!File.Exists(path))
            return list;
        foreach (var place in JsonSerializer.Deserialize<List<Place>>(File.ReadAllText(path), Json) ?? [])
            list._places[place.Key] = place;
        return list;
    }

    /// <summary>The list as its file's text: one place after another, by key, so two runs that found the same write the same.</summary>
    public string ToJson() => JsonSerializer.Serialize(_places.Values.ToList(), Json).ReplaceLineEndings("\n") + "\n";

    /// <summary>Writes the list.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = ToJson();

        // An editor or a search index holds the file for a moment: an hour's walk does not end on that.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(path, text);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }
        }
    }

    /// <summary>
    /// Adds what is known about a place to what the list holds of it. A state only goes forward (a place that was
    /// seen is not made unknown by a source that only knows of it), accounts and sources add up, and what was read
    /// later replaces what was read before.
    /// </summary>
    /// <returns>True when the place is new to the list.</returns>
    public bool Add(Place found)
    {
        ArgumentNullException.ThrowIfNull(found);
        if (!_places.TryGetValue(found.Key, out var held))
        {
            _places[found.Key] = found with { Accounts = Sorted(found.Accounts), RefusedTo = Sorted(found.RefusedTo), Sources = Sorted(found.Sources) };
            return true;
        }

        // Refused to one account, a place another account saw stays as it was seen: only who was refused is added.
        if (found.State == PlaceState.Refused && held.State is not (PlaceState.Known or PlaceState.Refused))
        {
            _places[found.Key] = held with { RefusedTo = Sorted(held.RefusedTo.Concat(found.RefusedTo)) };
            return false;
        }

        var read = found.State != PlaceState.Known;
        _places[found.Key] = held with
        {
            Kind = held.State == PlaceState.Known && read ? found.Kind : held.Kind,
            State = Later(held.State, found.State),
            Address = held.Address ?? found.Address,
            Parent = held.Parent ?? found.Parent,
            Via = held.Via ?? found.Via,
            Title = Text(read, found.Title, held.Title),
            Heading = Text(read, found.Heading, held.Heading),
            Fields = Read(read, found.Fields, held.Fields),
            Buttons = Read(read, found.Buttons, held.Buttons),
            Tabs = Read(read, found.Tabs, held.Tabs),
            Columns = Read(read, found.Columns, held.Columns),
            Calls = Sorted(held.Calls.Concat(found.Calls)),
            NotPressed = read ? found.NotPressed : held.NotPressed,
            Accounts = Sorted(held.Accounts.Concat(found.Accounts)),
            RefusedTo = Sorted(held.RefusedTo.Concat(found.RefusedTo)),
            Sources = Sorted(held.Sources.Concat(found.Sources)),
            Snapshot = found.Snapshot ?? held.Snapshot,
            SameAs = read ? found.SameAs : held.SameAs,
            Component = found.Component ?? held.Component,
            Seen = found.Seen ?? held.Seen,
            Tries = read ? found.Tries : held.Tries,
            Note = Text(read, found.Note, held.Note),
        };
        return false;
    }

    /// <summary>Puts a place in the list as it is given, whatever the list held of it.</summary>
    public void Set(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        _places[place.Key] = place;
    }

    /// <summary>A page that is known of and not opened: from an address on the app's own site.</summary>
    /// <param name="address">A full address, or a path with its hash route.</param>
    /// <param name="source">Where it is known from.</param>
    /// <param name="via">How it was reached; null when it was not.</param>
    public static Place Known(string address, string source, string? via = null)
    {
        var (key, path) = KeyAndPath(address);
        // A route with a parameter in it (/view/{id}) is no address: nothing opens it until a record's own is found.
        return new Place { Key = key, Kind = PlaceKind.Page, State = PlaceState.Known, Address = address.Contains('{', StringComparison.Ordinal) ? null : path, Via = via, Sources = [source] };
    }

    /// <summary>A page's key and the address that opens it (its path, query and hash route, without the host).</summary>
    public static (string Key, string Path) KeyAndPath(string address)
    {
        var full = address.StartsWith('/') ? "http://x" + address : address;
        if (!Uri.TryCreate(full, UriKind.Absolute, out var uri))
            return (address, address);
        return (Loose(PageKey.Of(uri.AbsoluteUri)), uri.PathAndQuery + uri.Fragment);
    }

    /// <summary>A key as two spellings of one place compare: <c>/contact</c>, <c>/contact#</c> and <c>/contact#/</c> are one.</summary>
    public static string Loose(string key)
    {
        var loose = key.TrimEnd('/');
        if (loose.EndsWith('#'))
            loose = loose[..^1].TrimEnd('/');
        return loose.Length == 0 ? "/" : loose;
    }

    // What was read of a place replaces what was only known of it; what is only known never replaces what was read.
    private static string? Text(bool read, string? found, string? held) => read ? found ?? held : held ?? found;

    private static IReadOnlyList<string> Read(bool read, IReadOnlyList<string> found, IReadOnlyList<string> held) => read && found.Count > 0 ? found : held;

    private static PlaceState Later(PlaceState held, PlaceState found) => (held, found) switch
    {
        (PlaceState.Known, _) => found,
        (PlaceState.Refused, PlaceState.Known) => held,
        (PlaceState.Refused, _) => found,
        (_, PlaceState.Known or PlaceState.Refused) => held,
        (PlaceState.Saved, _) => PlaceState.Saved,
        (_, PlaceState.Saved) => PlaceState.Saved,
        (PlaceState.FormNotSaved, _) => PlaceState.FormNotSaved,
        _ => found,
    };

    private static string[] Sorted(IEnumerable<string> values) => [.. values.Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
