using System.Globalization;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Places;

public static partial class PlacesSeeds
{
    /// <summary>
    /// The places a person reached by pressing, from the sessions the Recorder made: for each press with a page
    /// capture after it, the page it led to, or the tab, the dialog or the part it showed on the same page, with
    /// from where and by which press, as the walk writes it. A press in a row of a list is named after the row,
    /// never after what the row says.
    /// </summary>
    public static IEnumerable<Place> FromPresses(ScrapeProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        foreach (var session in project.Sessions())
        {
            // A press led to the last capture before the next press: the page as it settled, not as it was on the way
            // (a frame still showing the page it leaves, a dialog still without its content).
            RecordingEvent? last = null;
            RecordingEvent? before = null;
            RecordingEvent? press = null;
            RecordingEvent? after = null;
            foreach (var e in session.Events)
            {
                if (e.Kind == RecordingEvent.PressKind)
                {
                    if (Reached(project, session, before, press, after) is { } place)
                        yield return place;
                    (before, press, after) = (last, e, null);
                }
                else if (e.Kind == RecordingEvent.PageKind && e.Snapshot is not null)
                {
                    last = e;
                    if (press is not null)
                        after = e;
                }
            }

            if (Reached(project, session, before, press, after) is { } reached)
                yield return reached;
        }
    }

    private static Place? Reached(ScrapeProject project, RecordingSession session, RecordingEvent? before, RecordingEvent? press, RecordingEvent? after)
    {
        if (before is null || press is null || after is null || !project.IsOwnPage(after.Location))
            return null;
        var parent = PlacesList.KeyAndPath(before.Location).Key;
        var pressedOn = session.ReadSnapshot(before);
        var inRow = press.Role == "row" || (press.Node >= 1 && press.Node <= pressedOn.Nodes.Count && InRow(pressedOn, pressedOn[press.Node]));
        // A row, or a link in a row, says what a record says: the place is named after the row.
        var label = press.Role == "row" || (inRow && press.Role == "link") ? PlaceOpener.FirstRow : Place.Clean(press.Name ?? "");
        if (label.Length == 0)
            return null;
        var via = $"'{label}' on {parent}";
        var kept = $"{session.Id}/{after.Snapshot}";
        var seen = after.At.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var isTab = press.Role == "tab";
        var opener = press.Role == "link" && !inRow ? OpenerKind.Link : PlaceOpener.KindOf(label, isTab, inRow);

        var (key, _) = PlacesList.KeyAndPath(after.Location);
        if (key != parent)
            return Opened(after.Location, Recording, via) with { Opener = opener, Title = after.Title, Snapshot = kept, Seen = seen };

        var dialogBefore = PageFacts.Of(pressedOn, project.ControlSigns, project.PlaceSigns).Dialog is not null;
        var dialogAfter = PageFacts.Of(session.ReadSnapshot(after), project.ControlSigns, project.PlaceSigns).Dialog is not null;
        // A press that closed a dialog led back to the page: no place of its own.
        if (dialogBefore && !dialogAfter)
            return null;
        var kind = isTab ? PlaceKind.Tab : dialogAfter && !dialogBefore ? PlaceKind.Dialog : PlaceKind.Part;
        return new Place { Key = Place.PartKey(parent, label), Kind = kind, State = PlaceState.Seen, Parent = parent, Via = via, Opener = opener, Sources = [Recording], Snapshot = kept, Seen = seen };
    }

    private static bool InRow(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        for (var at = node; at.Parent > 0; at = snapshot[at.Parent])
        {
            if (at.Type == "tr" || at.Role == "row")
                return true;
        }

        return false;
    }
}
