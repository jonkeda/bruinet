using System.Globalization;
using System.Text;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Code;

/// <summary>What a draft of a place is asked for.</summary>
public enum PlaceDraftKind
{
    /// <summary>A form's class: a member per field and per button. A dialog's on its page, or a page's own.</summary>
    Form,

    /// <summary>A list's row: a member per column.</summary>
    Row,

    /// <summary>A test that opens the form and checks that it shows every field its class declares; it saves nothing.</summary>
    FormTest,
}

/// <summary>A drafted class: its name, its code, and what was left out with why.</summary>
/// <param name="ClassName">The class.</param>
/// <param name="Code">Its code.</param>
/// <param name="Members">The members it has, each as its name and the label or column it stands for.</param>
/// <param name="NotDrafted">What the place shows that got no member, with why.</param>
public sealed record PlaceDraft(string ClassName, string Code, IReadOnlyList<(string Name, string Label)> Members, IReadOnlyList<string> NotDrafted)
{
    /// <summary>The members a person presses, by name: the form's buttons and the openers of a page's cards. A test of the class checks that each is there.</summary>
    public IReadOnlyList<string> Pressed { get; init; } = [];
}

/// <summary>
/// Drafts a class from a place of the list by rules, in the suite's own shape (<see cref="DraftTemplates"/>): a form
/// with a member per field, typed by the control a sign named, and a member per button; a list's row with a member
/// per column; a test that holds a form against its class. A draft is a start a person renames and prunes.
/// </summary>
public static class PlaceDrafter
{
    // What a page object or a control has already: a member of that name would hide it.
    private static readonly string[] Reserved =
    [
        "AssertVisible", "AssertLoaded", "Click", "Close", "Context", "Form", "Header", "IsLoaded", "IsVisible", "Name", "Open", "Page", "Root", "Rows", "Self", "Tabs", "Url", "WaitLoaded",
    ];

    /// <summary>The template of the page of one record (<c>## Record page</c>): its <c>{Address}</c> has <c>{id}</c> where the record's id is.</summary>
    public const string RecordPage = "Record page";

    /// <summary>The template of the test of a record's page (<c>## Record page test</c>).</summary>
    public const string RecordPageTest = "Record page test";

    /// <summary>The template of the test of a dialog that opens on a record's page (<c>## Record form test</c>).</summary>
    public const string RecordFormTest = "Record form test";

    /// <summary>
    /// The template of the test of a dialog that a button in a list's row opens (<c>## Row form test</c>): the test
    /// presses the button of the first row that has one. <c>{Icon}</c> is the button's icon as the walk named it, and
    /// <c>{TabSteps}</c> the tabs to press first, each written with <c>## Tab step</c>.
    /// </summary>
    public const string RowFormTest = "Row form test";

    /// <summary>
    /// The template of a card's button when the card has several that each open a dialog (<c>## Opener member by icon</c>):
    /// <c>{Label}</c> is the card's title and <c>{Icon}</c> the button's icon as the walk named it. Without it such a
    /// card gets one member, as a card with one button does.
    /// </summary>
    public const string OpenerByIcon = "Opener member by icon";

    /// <summary>
    /// The template of the wait for a page's list before a button of the page is pressed (<c>## List step</c>), put
    /// where a test's template says <c>{ListStep}</c> when the dialog's page shows a list: a page whose list is still
    /// loading may not answer a press yet.
    /// </summary>
    public const string ListStep = "List step";

    /// <summary>The template of one tab pressed before a row's button (<c>## Tab step</c>), with <c>{Label}</c> and <c>{Page}</c>.</summary>
    public const string TabStep = "Tab step";

    /// <summary>The template of the test of a dialog a card's own button opens on a record's page (<c>## Record card form test</c>).</summary>
    public const string RecordCardFormTest = "Record card form test";

    /// <summary>The line a draft starts with: the place it was made from, the capture, and a hash of what it holds (<see cref="Shape"/>).</summary>
    public static ScrapedHeader Header(Place place, PlaceDraftKind kind, PlaceSigns? signs = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        return new ScrapedHeader { Page = place.Key, Variant = kind.ToString().ToLowerInvariant(), From = place.Snapshot, Hash = Shape(place, kind, signs) };
    }

    /// <summary>
    /// A short hash of what a draft is made from: a form's fields with their controls and its buttons, or a list's
    /// columns. The same place read later with another hash has changed under its class.
    /// </summary>
    public static string Shape(Place place, PlaceDraftKind kind, PlaceSigns? signs = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        var parts = kind == PlaceDraftKind.Row ? place.Columns : [.. place.Fields.Select(PlacesReading.ReadField).Where(field => field.Control != "(a table's own)").Select(field => $"{field.Label}|{field.Control}"), "|", .. Buttons(place, signs)];
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))))[..8];
    }

    /// <summary>Drafts a form's class.</summary>
    /// <param name="place">The form: a dialog, or a page.</param>
    /// <param name="templates">The suite's templates.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="namespace">Its namespace.</param>
    /// <param name="pageClass">For a dialog: the class of the page it is on.</param>
    /// <param name="opens">For a page: what opens on it by a card's own button (<c>Avatar: fa-pen</c>), each given an opener when the templates have an <c>Opener member</c>.</param>
    /// <param name="signs">How an icon is named on this app's pages (<see cref="ScrapeProject.PlaceSigns"/>); none when left out.</param>
    /// <param name="under">Of <paramref name="opens"/>, what lies under a tab or behind another press: its opener is a member, and is not there as the page opens.</param>
    public static PlaceDraft Form(Place place, DraftTemplates templates, string className, string @namespace, string? pageClass, IReadOnlyList<string>? opens = null, PlaceSigns? signs = null, IReadOnlyCollection<string>? under = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(templates);
        var dialog = place.Kind != PlaceKind.Page;
        if (dialog && string.IsNullOrEmpty(pageClass))
            throw new ArgumentException($"{place.Key} is on a page: the draft needs that page's class.", nameof(pageClass));
        var used = new HashSet<string>(Reserved, StringComparer.Ordinal) { className };
        used.UnionWith(templates.Reserved);
        var members = new List<(string Name, string Label)>();
        var notDrafted = new List<string>();
        var code = new List<string>();
        foreach (var field in place.Fields)
        {
            var (label, kind, control, recognized, made) = PlacesReading.ReadField(field);
            // A list's filter row is its table's, not the form's.
            if (control == "(a table's own)")
                continue;
            if (recognized && !made)
            {
                notDrafted.Add($"{label} ({kind}): its control, {control}, is still to make");
                continue;
            }

            var holder = PlacesReading.HolderOf(field);
            if (templates.MemberFor(recognized ? control : null, kind, holder) is not { } template)
            {
                notDrafted.Add($"{label} ({kind}): {(!recognized ? "no sign says which control it is" : templates.MemberFor(control, kind, templates.Fields.FirstOrDefault(row => row.Holder.Length > 0)?.Holder) is null ? $"no member is given for {control}" : $"it is {(holder is null ? "in no holder the suite names" : "in a " + holder)}: no member is given for a {control} there")}");
                continue;
            }

            var name = Unique(Identifier(label), used, templates.Reserved);
            members.Add((name, label));
            var required = field.Contains(", required", StringComparison.Ordinal) ? "    /// <summary>Required.</summary>\n" : "";
            code.Add(required + Fill(template, className, pageClass, name, label));
        }

        var buttons = members.Count;
        foreach (var button in Buttons(place, signs))
        {
            var name = Unique(Identifier(button), used, templates.Reserved);
            members.Add((name, button));
            code.Add(Fill(templates.Block("Button member"), className, pageClass, name, button));
        }

        SectionMembers(place, templates, className, pageClass, used, members, code, notDrafted);

        // What a person presses: the buttons, and the openers of the page's cards (its tabs lie between the two).
        List<string> pressed = [.. members.Skip(buttons).Select(member => member.Name), .. PageMembers(place, templates, className, pageClass, opens, used, members, code, signs)];
        LeaveOutWhatIsNotThere(place, under, signs, pressed, members, notDrafted);

        var summary = dialog
            ? $"The form \"{Xml(Label(place))}\" opens on {Xml(place.Parent ?? "")}: drafted from the list of places."
            : $"The form at {Xml(place.Key)}: drafted from the list of places.";
        var body = string.Join("\n\n", code.Select(Indent));
        if (notDrafted.Count > 0)
            body += (body.Length > 0 ? "\n\n" : "") + "    // Not drafted:\n" + string.Join('\n', notDrafted.Select(line => "    // " + line));
        // A page of one record has a template of its own when the suite has one: it cannot open by its address alone.
        var record = !dialog && PlaceKinds.IsRecord(place) && templates.Blocks.ContainsKey(RecordPage);
        var text = templates.Block(dialog ? "Dialog" : record ? RecordPage : "Form page")
            .Replace("{Header}", Header(place, PlaceDraftKind.Form, signs).ToString(), StringComparison.Ordinal)
            .Replace("{Namespace}", @namespace, StringComparison.Ordinal)
            .Replace("{Summary}", summary, StringComparison.Ordinal)
            .Replace("{Address}", record ? PlaceKinds.RecordAddress(place) : place.Key, StringComparison.Ordinal)
            .Replace("{Members}", body, StringComparison.Ordinal)
            .Replace("{Self}", className, StringComparison.Ordinal)
            .Replace("{Page}", pageClass ?? className, StringComparison.Ordinal);
        return new PlaceDraft(className, text.TrimEnd() + "\n", members, notDrafted) { Pressed = pressed };
    }

    // An opener under a tab is not on the page as it opens: its dialog's test presses it after the tab. And what a test
    // did not find on the app is said, not drafted.
    private static void LeaveOutWhatIsNotThere(Place place, IReadOnlyCollection<string>? under, PlaceSigns? signs, List<string> pressed, List<(string Name, string Label)> members, List<string> notDrafted)
    {
        var later = (under ?? []).SelectMany(label => new[] { label, CardOf(label, signs) ?? label }).ToHashSet(StringComparer.Ordinal);
        pressed.RemoveAll(name => later.Contains(members.First(member => member.Name == name).Label));
        notDrafted.AddRange(place.NotShown.Select(label => $"{label}: a test did not find it on the app as the place opens"));
    }

    // A part of the form that folds is named by its header: a member of its own kind, when the suite has one.
    private static void SectionMembers(Place place, DraftTemplates templates, string className, string? pageClass, HashSet<string> used, List<(string Name, string Label)> members, List<string> code, List<string> notDrafted)
    {
        foreach (var section in Sections(place))
        {
            if (!templates.Blocks.TryGetValue(SectionMember, out var sectionMember))
            {
                notDrafted.Add($"{section} (section): the templates have no '{SectionMember}'");
                continue;
            }

            var name = Unique(Identifier(section) + "Section", used, templates.Reserved);
            members.Add((name, section));
            code.Add(Fill(sectionMember, className, pageClass, name, section));
        }
    }

    // A page's tabs, and the buttons its cards carry that open a dialog: both are the page's to name.
    // Returns the names of the openers.
    private static List<string> PageMembers(Place place, DraftTemplates templates, string className, string? pageClass, IReadOnlyList<string>? opens, HashSet<string> used, List<(string Name, string Label)> members, List<string> code, PlaceSigns? signs)
    {
        if (templates.Blocks.TryGetValue("Tab member", out var tabMember))
        {
            foreach (var tab in place.Tabs)
            {
                var name = Unique(TabName(tab), used, templates.Reserved);
                members.Add((name, tab));
                code.Add(Fill(tabMember, className, pageClass, name, tab));
            }
        }

        var openers = members.Count;
        if (templates.Blocks.TryGetValue("Opener member", out var openerMember))
        {
            foreach (var card in (opens ?? []).Select(opened => CardOf(opened, signs)).Where(card => card is not null && !place.NotShown.Contains(card, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
            {
                // A card with several buttons that each open a dialog: a member per button, told apart by its icon.
                var icons = (opens ?? []).Where(opened => CardOf(opened, signs) == card).Select(opened => opened[(card!.Length + 2)..]).Distinct(StringComparer.Ordinal).ToList();
                if (icons.Count > 1 && templates.Blocks.TryGetValue(OpenerByIcon, out var byIcon))
                {
                    foreach (var icon in icons)
                    {
                        var own = Unique(OpenerName(card!) + IconWord(icon, signs), used, templates.Reserved);
                        members.Add((own, $"{card}: {icon}"));
                        code.Add(Fill(byIcon, className, pageClass, own, card!).Replace("{Icon}", icon, StringComparison.Ordinal));
                    }

                    continue;
                }

                var name = Unique(OpenerName(card!), used, templates.Reserved);
                members.Add((name, card!));
                code.Add(Fill(openerMember, className, pageClass, name, card!));
            }
        }

        return [.. members.Skip(openers).Select(member => member.Name)];
    }

    /// <summary>Drafts a list's row: a member per column the place's table has.</summary>
    public static PlaceDraft Row(Place place, DraftTemplates templates, string className, string @namespace, PlaceSigns? signs = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(templates);
        var used = new HashSet<string>(Reserved, StringComparer.Ordinal) { className };
        var members = new List<(string Name, string Label)>();
        var notDrafted = new List<string>();
        var code = new List<string>();
        foreach (var column in place.Columns)
        {
            // A column of buttons has no header, or an icon for one: a row's buttons are the person's to name.
            if (column.Length == 0 || !column.Any(char.IsLetter) || IsIcon(column, signs))
            {
                notDrafted.Add($"the column '{column}': it has no name");
                continue;
            }

            var name = Unique(Identifier(column), used, templates.Reserved);
            members.Add((name, column));
            code.Add(Fill(templates.Block("Column member"), className, null, name, column));
        }

        var body = string.Join("\n\n", code.Select(Indent));
        if (notDrafted.Count > 0)
            body += (body.Length > 0 ? "\n\n" : "") + "    // Not drafted:\n" + string.Join('\n', notDrafted.Select(line => "    // " + line));
        var text = templates.Block("Row")
            .Replace("{Header}", Header(place, PlaceDraftKind.Row).ToString(), StringComparison.Ordinal)
            .Replace("{Namespace}", @namespace, StringComparison.Ordinal)
            .Replace("{Summary}", $"A row of the list at {Xml(place.Key)}: a member per column the list shows. Drafted from the list of places.", StringComparison.Ordinal)
            .Replace("{Members}", body, StringComparison.Ordinal)
            .Replace("{Self}", className, StringComparison.Ordinal);
        return new PlaceDraft(className, text.TrimEnd() + "\n", members, notDrafted);
    }

    /// <summary>Drafts the test that holds a form against its class: it opens the form, checks its fields, and closes it without saving.</summary>
    /// <param name="place">The form.</param>
    /// <param name="templates">The suite's templates.</param>
    /// <param name="className">The test class's name.</param>
    /// <param name="namespace">Its namespace.</param>
    /// <param name="formClass">The form's class.</param>
    /// <param name="pageClass">The page it is on, or is.</param>
    /// <param name="pagesNamespace">The namespace the form's and the page's classes are in.</param>
    /// <param name="signs">How an icon is named on this app's pages (<see cref="ScrapeProject.PlaceSigns"/>); none when left out.</param>
    /// <param name="onPage">For a dialog: the page it opens on, so a dialog on a record's page gets the test that opens that page on a record.</param>
    /// <param name="beside">What else opens on the place's page (<see cref="Beside"/>): a card with several buttons has a member per button.</param>
    /// <param name="pressed">The members of the form's class a person presses (<see cref="PlaceDraft.Pressed"/>): the test checks that each is there, where its template has <c>{Checks}</c>.</param>
    public static PlaceDraft FormTest(Place place, DraftTemplates templates, string className, string @namespace, string formClass, string pageClass, string? pagesNamespace = null, PlaceSigns? signs = null, Place? onPage = null, IReadOnlyCollection<string>? beside = null, IReadOnlyList<string>? pressed = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(templates);
        var card = CardOf(Label(place), signs);
        var steps = templates.Blocks.TryGetValue(TabStep, out var step)
            ? string.Join("\n", Between(place).Select(tab => "        " + step.Replace("{Label}", tab.Replace("\"", "\\\"", StringComparison.Ordinal), StringComparison.Ordinal)))
            : "";
        // What the class declares beside its fields is checked too: a page's tabs, and every member a person presses.
        var scope = place.Kind == PlaceKind.Page ? "page" : "form";
        var checks = string.Join("\n", (place.Kind == PlaceKind.Page ? place.Tabs.Select(TabName) : []).Concat(pressed ?? []).Select(member => $"        {scope}.{member}.AssertVisible();"));
        var record = place.Kind == PlaceKind.Page && PlaceKinds.IsRecord(place) && templates.Blocks.ContainsKey(RecordPageTest);
        var text = templates.Block(TestTemplate(place, templates, card, signs, onPage))
            .Replace("        {TabSteps}\n", steps.Length == 0 ? "" : steps + "\n", StringComparison.Ordinal)
            .Replace("        {ListStep}\n", onPage is { Columns.Count: > 0 } && templates.Blocks.TryGetValue(ListStep, out var wait) ? "        " + wait + "\n" : "", StringComparison.Ordinal)
            .Replace("{Icon}", Label(place), StringComparison.Ordinal)
            // What opens the form is a member of its page: a card's opener, or the button named after what it says ("+ Day off" is AddDayOff).
            .Replace("{OpenerMember}", card is null ? Identifier(Label(place)) : OpenerName(card) + (templates.Blocks.ContainsKey(OpenerByIcon) ? IconOfSeveral(place, signs, beside) : ""), StringComparison.Ordinal)
            .Replace("{Checks}\n", checks.Length == 0 ? "" : checks + "\n", StringComparison.Ordinal)
            .Replace("{Checks}", checks, StringComparison.Ordinal)
            .Replace("{Header}", Header(place, PlaceDraftKind.FormTest).ToString(), StringComparison.Ordinal)
            .Replace("{Namespace}", @namespace, StringComparison.Ordinal)
            .Replace("{Self}", className, StringComparison.Ordinal)
            .Replace("{Form}", formClass, StringComparison.Ordinal)
            .Replace("{Page}", pageClass, StringComparison.Ordinal)
            .Replace("{Opener}", Label(place), StringComparison.Ordinal)
            .Replace("{Module}", Module(@namespace), StringComparison.Ordinal)
            .Replace("{PagesNamespace}", pagesNamespace ?? @namespace, StringComparison.Ordinal)
            .Replace("{Address}", record ? PlaceKinds.RecordAddress(place) : place.Key, StringComparison.Ordinal);
        return new PlaceDraft(className, text.TrimEnd() + "\n", [], []);
    }

    // Which test a place gets: by what it is, what opens it, and whether its page is the page of one record.
    private static string TestTemplate(Place place, DraftTemplates templates, string? card, PlaceSigns? signs, Place? onPage)
    {
        bool Has(string heading) => templates.Blocks.ContainsKey(heading);
        if (place.Kind == PlaceKind.Page)
            return PlaceKinds.IsRecord(place) && Has(RecordPageTest) ? RecordPageTest : "Form page test";
        if (onPage is not null && PlaceKinds.IsRecord(onPage))
        {
            if (card is null && Has(RecordFormTest))
                return RecordFormTest;
            if (card is not null && Has(RecordCardFormTest))
                return RecordCardFormTest;
        }
        // A button that shows only an icon and is no card's own is a row's: no member of the page stands for it.
        else if (card is null && IsIcon(Label(place), signs) && Has(RowFormTest))
            return RowFormTest;

        return card is not null ? "Card form test" : "Form test";
    }

    /// <summary>
    /// How a class and its place differ now: the labels the class names (<c>new(this, "Label")</c>, <c>Cell("Label")</c>)
    /// that the place does not show, and what the place shows that the class does not name.
    /// </summary>
    /// <param name="place">The place as the list holds it now.</param>
    /// <param name="code">The class's code, all its parts.</param>
    /// <param name="kind">What the class is: a form or a row.</param>
    /// <param name="signs">How an icon is named on this app's pages (<see cref="ScrapeProject.PlaceSigns"/>); none when left out.</param>
    public static (IReadOnlyList<string> NotOnPlace, IReadOnlyList<string> NotInClass) Drift(Place place, string code, PlaceDraftKind kind, PlaceSigns? signs = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(code);
        // A label a member names: new(this, "Label"), Cell("Label"), Action("Label"), and a button's text-is(\"Label\").
        var named = System.Text.RegularExpressions.Regex.Matches(code, """(?:new\((?:this|scope|[a-z]\w*), |Cell\(|Action\()"((?:[^"\\]|\\.)+)"|text-is\(\\"([^"\\]+)\\"\)""")
            .Select(match => (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Replace("\\\"", "\"", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var shown = kind == PlaceDraftKind.Row
            ? place.Columns.Where(column => column.Any(char.IsLetter)).ToList()
            : [.. place.Fields.Select(PlacesReading.ReadField).Where(field => field.Control != "(a table's own)").Select(field => field.Label), .. Buttons(place, signs), .. Sections(place)];
        return ([.. named.Where(label => !shown.Contains(label, StringComparer.Ordinal)).Order(StringComparer.Ordinal)], [.. shown.Where(label => !named.Contains(label)).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>A class's name from a place: a dialog from what opened it (<c>+ Day off</c> is <c>DayOffDialog</c>), a page from its address.</summary>
    /// <param name="place">The place.</param>
    /// <param name="kind">What is drafted.</param>
    /// <param name="signs">How an icon is named on this app's pages; none when left out.</param>
    /// <param name="beside">What else opens on the place's page (<see cref="Beside"/>): two dialogs of one card are told apart by their buttons' icons (<c>TimelineCalendarDialog</c>).</param>
    public static string ClassName(Place place, PlaceDraftKind kind, PlaceSigns? signs = null, IReadOnlyCollection<string>? beside = null)
    {
        ArgumentNullException.ThrowIfNull(place);
        if (kind == PlaceDraftKind.Row)
            return PageDrafter.ClassName(place.Key)[..^"Page".Length] + "Row";
        if (place.Kind == PlaceKind.Page)
            return PageDrafter.ClassName(place.Key);
        // A dialog a titled part's own button opens is the part's: "Avatar: <an icon>" is AvatarDialog. One a bare icon opens is its page's form.
        var label = CardOf(Label(place), signs) ?? Label(place);
        var name = IsIcon(label, signs) ? "" : Identifier(label.TrimStart('+', ' ')) + IconOfSeveral(place, signs, beside);
        // A page has such a button under each of its tabs: the tab is in the name (EquipmentBookingsFormDialog).
        return (name.Length == 0 ? PageDrafter.ClassName(place.Parent ?? place.Key)[..^"Page".Length] + string.Concat(Between(place).Select(Identifier)) + "Form" : name) + "Dialog";
    }

    // What was pressed on a place's page before what opened the place: the parts of its key between the two.
    private static string[] Between(Place place)
    {
        var parent = place.Parent ?? "";
        if (parent.Length == 0 || !place.Key.StartsWith(parent + " › ", StringComparison.Ordinal))
            return [];
        var parts = place.Key[(parent.Length + 3)..].Split(" › ");
        return parts[..^1];
    }

    // The module a namespace is of: the part after "Modules" (Shop.Modules.Schedule.Tests is Schedule); its last part otherwise.
    private static string Module(string @namespace)
    {
        var parts = @namespace.Split('.');
        var at = Array.IndexOf(parts, "Modules");
        return at >= 0 && at + 1 < parts.Length ? parts[at + 1] : parts[^1];
    }

    /// <summary>The card a button belongs to, when the walk named the button after it (<c>Avatar: fa-pen</c> is <c>Avatar</c>); null otherwise.</summary>
    public static string? CardOf(string label, PlaceSigns? signs = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        var at = label.LastIndexOf(": ", StringComparison.Ordinal);
        return at > 0 && IsIcon(label[(at + 2)..], signs) ? label[..at] : null;
    }

    // A label that is an icon's name, as the walk writes a button that shows only an icon.
    private static bool IsIcon(string label, PlaceSigns? signs) => (signs ?? PlaceSigns.None).IconName.Any(start => label.StartsWith(start, StringComparison.Ordinal));

    private static string TabName(string tab) => Identifier(tab) + "Tab";

    private static string OpenerName(string card) => "Edit" + Identifier(card);

    /// <summary>What opens on a place's page, as the walk named each: the last part of the key of every dialog of that page.</summary>
    public static IReadOnlyCollection<string> Beside(IEnumerable<Place> places, Place place)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(place);
        return place.Kind != PlaceKind.Dialog || place.Parent is null ? [] : [.. places.Where(each => each.Kind == PlaceKind.Dialog && each.Parent == place.Parent).Select(Label)];
    }

    // The icon of a card's button as a word, when the card has another button that opens a dialog too; empty otherwise.
    private static string IconOfSeveral(Place place, PlaceSigns? signs, IReadOnlyCollection<string>? beside)
    {
        var label = Label(place);
        if (beside is null || CardOf(label, signs) is not { } card)
            return "";
        var icon = label[(card.Length + 2)..];
        return beside.Any(other => other != label && CardOf(other, signs) == card) ? IconWord(icon, signs) : "";
    }

    // An icon's name without what every icon's name starts with: fa-calendar is Calendar.
    private static string IconWord(string icon, PlaceSigns? signs)
    {
        var start = (signs ?? PlaceSigns.None).IconName.Where(each => icon.StartsWith(each, StringComparison.Ordinal)).OrderByDescending(each => each.Length).FirstOrDefault() ?? "";
        return Identifier(icon[start.Length..]);
    }

    // What opened a part of a page: the last part of its key.
    private static string Label(Place place) => place.Key.Contains(" › ", StringComparison.Ordinal) ? place.Key[(place.Key.LastIndexOf(" › ", StringComparison.Ordinal) + 3)..] : place.Key;

    /// <summary>The template of a member for a section of a form that folds (<c>## Section member</c>), with <c>{Label}</c> the section's header.</summary>
    public const string SectionMember = "Section member";

    // The headers of the form's sections, as the reading marked them among the buttons.
    private static IEnumerable<string> Sections(Place place) => place.Buttons
        .Where(button => button.EndsWith(Service.PageFacts.SectionMark, StringComparison.Ordinal))
        .Select(button => WithoutCount(button[..^Service.PageFacts.SectionMark.Length])).Where(section => section.Any(char.IsLetter) && !place.NotShown.Contains(section, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal);

    // A header may end in how many it holds now (Risks (4)): the number is today's, the name is the section's.
    private static string WithoutCount(string header)
    {
        var at = header.LastIndexOf(" (", StringComparison.Ordinal);
        return at > 0 && header.EndsWith(')') && header.Length - at > 3 && header[(at + 2)..^1].All(char.IsAsciiDigit) ? header[..at] : header;
    }

    // A form's buttons that a test presses: named in words, on now or off, not an icon and not a pager's.
    private static IEnumerable<string> Buttons(Place place, PlaceSigns? signs) => place.Buttons
        .Where(button => !button.EndsWith(Service.PageFacts.SectionMark, StringComparison.Ordinal) && !button.EndsWith(Service.PageFacts.IconMark, StringComparison.Ordinal))
        .Select(button => button.EndsWith(" (disabled)", StringComparison.Ordinal) ? button[..^" (disabled)".Length] : button)
        .Where(button => button.Any(char.IsLetter) && !button.StartsWith('(') && button.Split(' ').Length <= 4 && !IsIcon(button, signs)
            && !button.Contains("page", StringComparison.OrdinalIgnoreCase) && !place.NotShown.Contains(button, StringComparer.Ordinal))
        .Distinct(StringComparer.Ordinal);

    private static string Fill(string template, string self, string? page, string name, string label) => template
        .Replace("{Self}", self, StringComparison.Ordinal)
        .Replace("{Page}", page ?? self, StringComparison.Ordinal)
        .Replace("{Name}", name, StringComparison.Ordinal)
        .Replace("{Label}", label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal), StringComparison.Ordinal);

    private static string Indent(string member) => string.Join('\n', member.Split('\n').Select(line => line.Length == 0 || line.StartsWith("    ", StringComparison.Ordinal) ? line : "    " + line));

    // A label as a member's name: its words run together, each with a capital (Number of weeks ahead (mobile) is NumberOfWeeksAheadMobile).
    private static string Identifier(string label)
    {
        // "+ Contact" adds a contact: AddContact, which is not the field Contact.
        var name = (label.TrimStart().StartsWith('+') ? "Add" : "") + string.Concat(DangerWords.Words(label).Select(word => char.ToUpperInvariant(word[0]) + word[1..]).Take(8));
        name = new string([.. name.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_')]);
        return name.Length == 0 ? "Field" : char.IsDigit(name[0]) ? "Field" + name : name;
    }

    private static string Unique(string name, HashSet<string> used, IReadOnlyList<string> reserved)
    {
        // A name the base has already (Name, Form; the suite's own: Title) says what it is: NameField, not Name2.
        if (Reserved.Contains(name, StringComparer.Ordinal) || reserved.Contains(name, StringComparer.Ordinal))
            name += "Field";
        var unique = name;
        for (var n = 2; !used.Add(unique); n++)
            unique = name + n.ToString(CultureInfo.InvariantCulture);
        return unique;
    }

    private static string Xml(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
