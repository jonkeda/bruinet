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
        var owed = Owed.Of(list, suite).Groups.FirstOrDefault(each => each.Group == group);
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
                Write(testFile, test.Code);
                written.Add(new Written(testFile, key, test.ClassName, IsTest: true) { Of = exists ? null : form.ClassName });
            }
            catch (ScraperProblem problem)
            {
                reasons.Add(ReasonClassifier.OfDraft(problem.Message, key));
            }
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

    // Runs the loop's tests; what fails runs once more. A test that fails twice goes, with the class it holds.
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

    // Deletes a file the loop wrote, and with it what the loop wrote that names its class: a class nothing checks is not kept, and a test of nothing does not build.
    private static void PutBack(Written file, List<Written> written, List<Reason> reasons, Reason reason)
    {
        if (file.PutBack)
            return;
        file.PutBack = true;
        if (File.Exists(file.File))
            File.Delete(file.File);
        reasons.Add(reason);
        foreach (var other in written.Where(each => !each.PutBack && (each.Of == file.Class || file.Of == each.Class)).ToList())
        {
            other.PutBack = true;
            if (File.Exists(other.File))
                File.Delete(other.File);
        }
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

    // A file a loop wrote: the place it is of, its class, and for a test the class it holds.
    private sealed record Written(string File, string Place, string Class, bool IsTest)
    {
        public string? Of { get; init; }

        public bool PutBack { get; set; }
    }
}
