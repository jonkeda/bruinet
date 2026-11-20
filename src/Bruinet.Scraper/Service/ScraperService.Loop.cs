using System.Diagnostics;
using System.Text.RegularExpressions;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Service;

/// <summary>How one loop of the factory runs.</summary>
public sealed record LoopOptions
{
    /// <summary>Walks the group's pages first; false to work from the captures there are.</summary>
    public bool Walk { get; init; } = true;

    /// <summary>The longest the walk takes, in minutes.</summary>
    public int WalkMinutes { get; init; } = 25;

    /// <summary>Whether the walk's browser shows.</summary>
    public bool Visible { get; init; }

    /// <summary>Runs the drafted tests on the app; false to stop after the build.</summary>
    public bool Test { get; init; } = true;

    /// <summary>
    /// The tests that must still pass after the loop, as a <c>dotnet test</c> filter (<c>Module=Projects</c>): they
    /// run before the loop writes anything and again at its end. Null for none.
    /// </summary>
    public string? Baseline { get; init; }

    /// <summary>
    /// Drafts no class: the loop drafts a save test for each dialog of the group on a record's page whose class the
    /// Scraper drafted and that has none yet. Only where the setup lets the Scraper write.
    /// </summary>
    public bool Save { get; init; }

    /// <summary>The most places one loop takes up: a small group shows a fault before it is repeated twenty times.</summary>
    public int MaxPlaces { get; init; } = 6;

    /// <summary>Places this run took up already: a loop does not take them up again.</summary>
    public IReadOnlySet<string> Skip { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The batch the loop is in.</summary>
    public int Batch { get; init; } = 1;

    /// <summary>Told what the loop is doing.</summary>
    public IProgress<string>? Progress { get; init; }
}

// One loop of the factory: a group through walk, read, draft, build and test. What does not build or does not pass is put back, with why.
public sealed partial class ScraperService
{
    /// <summary>
    /// Takes one group of places through the factory's stations: walks its pages, reads the captures, drafts a class
    /// and a test for each place that owes one, builds the suite, runs the new tests, and puts back what does not
    /// build or does not pass twice, each with a reason. Nothing a loop did not write is touched.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="listFile">The list of places.</param>
    /// <param name="group">The group: the first part of its pages' addresses (<c>/day-off</c>).</param>
    /// <param name="options">How the loop runs.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    public async Task<LoopResult> RunLoopAsync(string? projectName, string listFile, string group, LoopOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var project = Project(projectName);
        var started = DateTimeOffset.Now;
        var seconds = new Dictionary<string, double>(StringComparer.Ordinal);
        var reasons = new List<Reason>();
        var written = new List<Written>();
        var taken = new List<string>();
        var walk = "";
        string? stop = null;
        var clock = Stopwatch.StartNew();
        void Station(string name)
        {
            seconds[name] = Math.Round(clock.Elapsed.TotalSeconds, 1);
            clock.Restart();
            options.Progress?.Report($"{group}: {name} {seconds[name]} s");
        }

        if (options.Walk)
        {
            var setup = FactorySetup.Read(project.Home);
            var careful = setup.Writes == WritesAllowed.ReadOnly;
            var result = await WalkPlacesAsync(project.Name, listFile, new WalkOptions { Only = group, Again = true, MaxMinutes = options.WalkMinutes, LinksAndTabs = careful, HoldAll = careful, Progress = options.Progress }, null, options.Visible, cancellationToken).ConfigureAwait(false);
            walk = $"{result.Stop}: {result.Opened} pages opened, {result.Presses} presses, {result.Held} writes held back, {result.Found} places found";
            if (result.Stop == WalkStop.SignIn)
            {
                reasons.Add(new Reason(ReasonCodes.SignedOut, "walk", group, "", string.Join(' ', result.Notes), FixPlaces.Environment));
                stop = "the saved sign-in ran out and could not be renewed";
            }

            Station("walk");
        }

        var list = PlacesList.Load(listFile);
        if (ReadPlacesAgain(project.Name, list) > 0)
            list.Save(listFile);
        Station("read");

        IReadOnlyList<TestOutcome> before = [];
        if (stop is null && options.Baseline is { Length: > 0 } && options.Test)
        {
            before = (await TestRunner.RunEachAsync(Directory.EnumerateFiles(project.Folder, "*.csproj").First(), options.Baseline, cancellationToken).ConfigureAwait(false)).Tests;
            Station("baseline");
        }

        if (stop is null)
        {
            if (options.Save)
                DraftSaveTests(project, list, group, options, taken, written, reasons);
            else
                Draft(project, list, group, options, taken, written, reasons);
            Station("draft");

            stop = await BuildAndPutBackAsync(project, written, reasons, cancellationToken).ConfigureAwait(false);
            Station("build");
        }

        var (tests, passedFirst) = (0, 0);
        if (stop is null && options.Test && written.Any(each => each.IsTest && !each.PutBack))
        {
            (tests, passedFirst) = await TestAndPutBackAsync(project, list, written, reasons, cancellationToken).ConfigureAwait(false);
            // What stood on a class that went goes with it.
            if (written.Any(each => each.PutBack))
                stop = await BuildAndPutBackAsync(project, written, reasons, cancellationToken).ConfigureAwait(false);
            Station("test");
        }

        if (stop is null && before.Count > 0 && written.Any(each => !each.PutBack))
        {
            var after = (await TestRunner.RunEachAsync(Directory.EnumerateFiles(project.Folder, "*.csproj").First(), options.Baseline!, cancellationToken).ConfigureAwait(false)).Tests;
            var broken = Baseline.Broken(before, after);
            if (broken.Count > 0)
            {
                // Which of the loop's files broke it is not known: all of them go, and the suite is as it was.
                foreach (var each in written.Where(each => !each.PutBack).ToList())
                    PutBack(each, written, reasons, new Reason(ReasonCodes.BrokeAPassingTest, "baseline", each.Place, each.Class, $"{broken[0].Name}: {broken[0].Message}", FixPlaces.Templates));
                stop = await BuildAndPutBackAsync(project, written, reasons, cancellationToken).ConfigureAwait(false);
            }

            Station("baseline after");
        }

        string Relative(Written each) => Path.GetRelativePath(project.Folder, each.File).Replace('\\', '/');
        return new LoopResult
        {
            Batch = options.Batch,
            Group = group,
            Places = taken,
            Started = started,
            Walk = walk,
            Drafted = [.. written.Select(Relative)],
            Kept = [.. written.Where(each => !each.PutBack).Select(Relative)],
            PutBack = [.. written.Where(each => each.PutBack).Select(Relative)],
            Tests = tests,
            PassedFirst = passedFirst,
            Reasons = reasons,
            Seconds = seconds,
            Stop = stop,
        };
    }

    // A class and its test for each place of the group that owes one: a page first, so a dialog finds its page's class.
    private void Draft(ScrapeProject project, PlacesList list, string group, LoopOptions options, List<string> taken, List<Written> written, List<Reason> reasons)
    {
        var suite = SuiteSource.Read(project);
        var owed = Owed.Of(list, suite, project.PlaceSigns).Groups.FirstOrDefault(each => each.Group == group);
        if (owed is null)
            return;
        // Pages before what opens on them: a dialog is drafted on its page's class.
        foreach (var key in owed.PagesWithoutClass.Concat(RecordPagesToDraft(project, owed)).Concat(owed.DialogFormsNotDrafted).Where(key => !options.Skip.Contains(key)).Take(options.MaxPlaces))
        {
            taken.Add(key);
            try
            {
                var (form, file) = DraftPlace(project.Name, list, key, PlaceDraftKind.Form);
                if (form.Members.Count == 0)
                {
                    // A class whose test could not fail is not kept: the place is listed with why.
                    reasons.Add(new Reason(ReasonCodes.NothingToCheck, "draft", key, form.ClassName, "the place shows no field, button, tab or opener a test could check", FixPlaces.DraftKind));
                    continue;
                }

                // A class of that name is the suite's already, written by a person or under another first line: it is not
                // written a second time. Its place gets the test it lacks.
                var exists = file is null || File.Exists(file) || Regex.IsMatch(suite.Text, $@"\bclass\s+{Regex.Escape(form.ClassName)}\b");
                if (exists)
                    reasons.Add(new Reason(ReasonCodes.ClassExists, "draft", key, form.ClassName, $"{form.ClassName} is in the suite already; only a test is drafted, when it has none.", FixPlaces.Person));
                else
                {
                    Write(file!, form.Code);
                    written.Add(new Written(file!, key, form.ClassName, IsTest: false));
                }

                var (test, _) = DraftPlace(project.Name, list, key, PlaceDraftKind.FormTest);
                var testFile = TestFile(project, test);
                if (File.Exists(testFile))
                    continue; // a test of that name is a person's, or an earlier loop's: it is not written over
                if (Regex.IsMatch(suite.Text, $@"\bclass\s+{Regex.Escape(test.ClassName)}\b"))
                    continue;
                if (list[key]?.Parent is { } parent && GivePageWhatTheTestNames(project, list, parent, test.Code, key) is { } given)
                    written.Add(new Written(given.File, parent, given.Class, IsTest: false) { Before = given.Before, For = test.ClassName });
                Write(testFile, test.Code);
                written.Add(new Written(testFile, key, test.ClassName, IsTest: true) { Of = exists ? null : form.ClassName });
            }
            catch (ScraperProblem problem)
            {
                reasons.Add(ReasonClassifier.OfDraft(problem.Message, key));
            }
        }
    }

    /// <summary>What a place is called among the places a run took up when its save test was tried: a save is another try than its class.</summary>
    public static string SaveKey(string place) => "save: " + place;

    // A save test for each dialog of the group that can have one and has none: on a record's page, of a class the Scraper drafted.
    private void DraftSaveTests(ScrapeProject project, PlacesList list, string group, LoopOptions options, List<string> taken, List<Written> written, List<Reason> reasons)
    {
        if (FactorySetup.Read(project.Home).Writes == WritesAllowed.ReadOnly)
        {
            reasons.Add(new Reason(ReasonCodes.Refused, "draft", "", "", "the setup's Writes is read-only: no form is saved", FixPlaces.Person));
            return;
        }

        var dialogs = list.Places.Where(place => place.Kind == PlaceKind.Dialog && place.SameAs is null && PlaceKinds.GroupOf(place) == group
            && place.Parent is { } parent && list[parent] is { } page && PlaceKinds.IsRecord(page) && DraftedFrom(project, place.Key, "form") is not null);
        foreach (var place in dialogs.Where(place => !options.Skip.Contains(SaveKey(place.Key))).Take(options.MaxPlaces))
        {
            try
            {
                var (test, _) = DraftPlace(project.Name, list, place.Key, PlaceDraftKind.SaveTest);
                var file = TestFile(project, test);
                if (File.Exists(file))
                    continue; // it has its save test
                taken.Add(SaveKey(place.Key));
                Write(file, test.Code);
                written.Add(new Written(file, place.Key, test.ClassName, IsTest: true));
            }
            catch (ScraperProblem problem)
            {
                taken.Add(SaveKey(place.Key));
                reasons.Add(ReasonClassifier.OfDraft(problem.Message, place.Key));
            }
        }
    }

    // A dialog's test presses a member of its page. A page's class that lacks it (a person's, or one drafted before the
    // opener was on the page) gets it from a draft of the page: that member and no other, and nothing of its own changes.
    // The opener may lie under a tab of the page (the dialog's key names the tab between the two): what that tab shows
    // is the page's to name as well. Returns the file that changed, what it held, and the class; null when nothing was added.
    internal (string File, string Before, string Class)? GivePageWhatTheTestNames(ScrapeProject project, PlacesList list, string pageKey, string test, string? dialogKey = null)
    {
        if (list[pageKey] is null || (PageClassAt(project, pageKey) ?? DraftedFrom(project, pageKey, "form")) is not { } name || ClassFile(project, name) is not { } file)
            return null;
        var before = File.ReadAllText(file);
        var now = before;
        foreach (var source in new[] { pageKey }.Concat(TabsBetween(list, pageKey, dialogKey)))
        {
            try
            {
                var (draft, _) = DraftPlace(project.Name, list, source, PlaceDraftKind.Form, name, name);
                var named = draft.Members.Select(member => member.Name).Where(member => Regex.IsMatch(test, $@"\.{Regex.Escape(member)}\b")).ToList();
                if (named.Count > 0)
                    now = ClassMerge.Merge(now, draft.Code, name, addOnly: true, only: named).Code;
            }
            catch (Exception problem) when (problem is ScraperProblem or ArgumentException)
            {
                // That place cannot be drafted or the class is not in that file: the test says what it lacks when it is built.
            }
        }

        if (now == before)
            return null;
        File.WriteAllText(file, now);
        return (file, before, name);
    }

    // The tabs pressed on a page before what opens a dialog: the places between the page's key and the dialog's that are tabs.
    private static IEnumerable<string> TabsBetween(PlacesList list, string pageKey, string? dialogKey)
    {
        if (dialogKey is null || !dialogKey.StartsWith(pageKey + " › ", StringComparison.Ordinal))
            yield break;
        var parts = dialogKey[(pageKey.Length + 3)..].Split(" › ");
        var key = pageKey;
        foreach (var part in parts[..^1])
        {
            key += " › " + part;
            if (list[key] is { Kind: PlaceKind.Tab })
                yield return key;
        }
    }

    // A record's page is drafted when the suite's templates say how such a page is written and opened.
    private static IEnumerable<string> RecordPagesToDraft(ScrapeProject project, OwedGroup owed)
    {
        var file = Path.Combine(project.Home, DraftTemplates.FileName);
        return File.Exists(file) && DraftTemplates.Parse(File.ReadAllText(file)).Blocks.ContainsKey(PlaceDrafter.RecordPage) ? owed.RecordPages : [];
    }

    // Builds the suite; a file of the loop's that does not build is put back with what names its class, until it builds.
    // Returns why the factory must stop, when the suite does not build without the loop's files.
    private async Task<string?> BuildAndPutBackAsync(ScrapeProject project, List<Written> written, List<Reason> reasons, CancellationToken cancellationToken)
    {
        for (var round = 0; round < 6; round++)
        {
            var build = await CodeGate.BuildAsync(project, _templates, cancellationToken).ConfigureAwait(false);
            if (build.Succeeded)
                return null;
            var errors = build.Problems.Where(problem => problem.Severity == "error" && problem.File is not null).ToList();
            var broken = written.Where(each => !each.PutBack && errors.Any(error => SameFile(error.File!, each.File))).ToList();
            if (broken.Count == 0)
            {
                // Not one of the loop's files: everything it wrote goes, and the suite must build as it was.
                var left = written.Where(each => !each.PutBack).ToList();
                if (left.Count == 0)
                    return $"the suite does not build, and not for a file of this loop: {errors.FirstOrDefault()?.Message ?? build.Output ?? "no message"}";
                foreach (var each in left)
                    PutBack(each, written, reasons, new Reason(ReasonCodes.DoesNotBuild, "build", each.Place, each.Class, "the suite did not build with it", FixPlaces.Templates));
                continue;
            }

            foreach (var each in broken)
                PutBack(each, written, reasons, ReasonClassifier.OfBuild(errors.First(error => SameFile(error.File!, each.File)), each.Place, each.Class));
        }

        return "the suite does not build after six rounds of putting back what did not";
    }

    // Runs the loop's tests; what fails runs once more, and so does what passed. A test that fails twice goes, with
    // the class it holds; so does one that passed and then failed: it would fail in somebody's whole run.
    private async Task<(int Tests, int PassedFirst)> TestAndPutBackAsync(ScrapeProject project, PlacesList list, List<Written> written, List<Reason> reasons, CancellationToken cancellationToken)
    {
        var projectFile = Directory.EnumerateFiles(project.Folder, "*.csproj").First();
        var mine = written.Where(each => each.IsTest && !each.PutBack).ToList();
        string Filter(IEnumerable<Written> tests) => string.Join('|', tests.Select(test => $"FullyQualifiedName~.{test.Class}."));
        var first = await TestRunner.RunEachAsync(projectFile, Filter(mine), cancellationToken).ConfigureAwait(false);
        if (first.Problem is not null)
        {
            foreach (var test in mine)
                PutBack(test, written, reasons, new Reason(ReasonCodes.Unknown, "test", test.Place, test.Class, first.Problem, FixPlaces.Person));
            return (0, 0);
        }

        var failed = mine.Where(test => first.Tests.Any(outcome => outcome.Class == test.Class && !outcome.Passed)).ToList();
        var ran = mine.Count(test => first.Tests.Any(outcome => outcome.Class == test.Class));
        await ConfirmAsync(projectFile, [.. mine.Except(failed)], Filter, written, reasons, cancellationToken).ConfigureAwait(false);
        if (failed.Count == 0)
            return (ran, ran);
        var second = await TestRunner.RunEachAsync(projectFile, Filter(failed), cancellationToken).ConfigureAwait(false);
        foreach (var test in failed)
        {
            var again = second.Tests.Where(outcome => outcome.Class == test.Class).ToList();
            if (again.Count > 0 && again.All(outcome => outcome.Passed))
            {
                // Kept, and said: a test that needs a second run waits for something it does not name.
                reasons.Add(new Reason(ReasonCodes.PassedSecondTime, "test", test.Place, test.Class, first.Tests.First(outcome => outcome.Class == test.Class && !outcome.Passed).Message ?? "", FixPlaces.Templates));
                continue;
            }

            var message = again.FirstOrDefault(outcome => !outcome.Passed)?.Message ?? first.Tests.First(outcome => outcome.Class == test.Class && !outcome.Passed).Message ?? "failed";
            PutBack(test, written, reasons, ReasonClassifier.OfTest(message, test.Place, test.Class, list[test.Place] is { } place && PlaceKinds.IsRecord(place)));
        }

        return (ran, ran - failed.Count);
    }

    // What passed runs once more, together: a test that passes once and fails once waits for something it does not name.
    private static async Task ConfirmAsync(string projectFile, List<Written> passed, Func<IEnumerable<Written>, string> filter, List<Written> written, List<Reason> reasons, CancellationToken cancellationToken)
    {
        if (passed.Count == 0)
            return;
        var again = await TestRunner.RunEachAsync(projectFile, filter(passed), cancellationToken).ConfigureAwait(false);
        foreach (var test in passed)
        {
            if (again.Tests.FirstOrDefault(outcome => outcome.Class == test.Class && !outcome.Passed) is { } failure)
                PutBack(test, written, reasons, new Reason(ReasonCodes.PassedThenFailed, "test", test.Place, test.Class, failure.Message ?? "failed", FixPlaces.Templates));
        }
    }

    // Deletes a file the loop wrote, and with it what the loop wrote that names its class: a class nothing checks is not kept, and a test of nothing does not build.
    private static void PutBack(Written file, List<Written> written, List<Reason> reasons, Reason reason)
    {
        if (file.PutBack)
            return;
        file.PutBack = true;
        Remove(file);
        reasons.Add(reason);
        foreach (var other in written.Where(each => !each.PutBack && (each.Of == file.Class || file.Of == each.Class || each.For == file.Class)).ToList())
        {
            other.PutBack = true;
            Remove(other);
        }
    }

    // A file the loop made goes; a file it added to is as it was.
    private static void Remove(Written file)
    {
        if (file.Before is not null)
            File.WriteAllText(file.File, file.Before);
        else if (File.Exists(file.File))
            File.Delete(file.File);
    }

    /// <summary>
    /// Writes a drafted test into its file, over the test the Scraper drafted there before. A file without the
    /// Scraper's first line is a person's, or was a draft a person took over by removing that line: it is left alone.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="test">The drafted test.</param>
    /// <returns>The file, and whether it was written.</returns>
    public (string File, bool Written) WriteTest(string? projectName, PlaceDraft test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var file = TestFile(Project(projectName), test);
        if (File.Exists(file) && ScrapedHeader.Find(File.ReadAllText(file))?.Page is null)
            return (file, false);
        Write(file, test.Code);
        return (file, true);
    }

    // Where a drafted test goes: its namespace under the suite's own, as folders; a module's tests in its Tests folder, whatever folder its pages are in.
    internal static string TestFile(ScrapeProject project, PlaceDraft test)
    {
        var declared = NamespaceLine().Match(test.Code);
        var space = declared.Success ? declared.Groups[1].Value : project.Namespace;
        var root = RootNamespace(project);
        var relative = space.StartsWith(root + ".", StringComparison.Ordinal) ? space[(root.Length + 1)..] : space;
        var folders = relative.Split('.');
        var tests = Array.IndexOf(folders, "Tests");
        if (tests >= 0)
            folders = folders[..(tests + 1)];
        return Path.Combine([project.Folder, .. folders, test.ClassName + ".cs"]);
    }

    // The suite's own namespace: its project file's RootNamespace, else the project's name.
    private static string RootNamespace(ScrapeProject project)
    {
        var file = Directory.EnumerateFiles(project.Folder, "*.csproj").FirstOrDefault();
        var declared = file is null ? null : RootNamespaceElement().Match(File.ReadAllText(file));
        return declared is { Success: true } ? declared.Groups[1].Value.Trim() : project.Name;
    }

    private static void Write(string file, string code)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, code);
    }

    private static bool SameFile(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex NamespaceLine();

    [GeneratedRegex(@"<RootNamespace>([^<]+)</RootNamespace>")]
    private static partial Regex RootNamespaceElement();

    // A file a loop wrote: the place it is of, its class, for a test the class it holds, and for a file that was there what it held.
    private sealed record Written(string File, string Place, string Class, bool IsTest)
    {
        public string? Of { get; init; }

        public string? Before { get; init; }

        // For what was added to a class that was there: the test it was added for. When that test goes, so does the addition.
        public string? For { get; init; }

        public bool PutBack { get; set; }
    }
}
