using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

/// <summary>What reading the app's scripts for routes gave.</summary>
/// <param name="Pages">The pages opened.</param>
/// <param name="Scripts">The scripts read: a page's own, not what every page loads.</param>
/// <param name="Routes">The routes they name.</param>
/// <param name="New">How many of those the list did not hold.</param>
/// <param name="Problem">What stopped it; null when nothing did.</param>
public sealed record RoutesRead(int Pages, int Scripts, int Routes, int New, string? Problem);

public sealed partial class ScraperService
{
    /// <summary>
    /// Adds to a list of places what is known without opening the app: the pages the project's recordings captured.
    /// </summary>
    /// <returns>How many places were new to the list.</returns>
    public int SeedPlaces(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return PlacesSeeds.FromRecordings(Project(projectName)).Count(list.Add);
    }

    /// <summary>
    /// Walks the app for its places (see <see cref="Walker"/>), from the list's frontier, as the project's saved
    /// sign-in or another account's beside it. The list is saved after every page, so a walk that stops loses one
    /// place. A sign-in that ran out is renewed by the config's <c>RenewSignIn</c>, once, and the walk goes on.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="listFile">The list's file; read when it is there, written as the walk goes.</param>
    /// <param name="options">How this walk goes.</param>
    /// <param name="account">The account to walk as (<c>Reader</c>); null for the project's own.</param>
    /// <param name="visible">Whether the browser shows.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <exception cref="ScraperProblem">A recording holds the profile, or the platform cannot be walked.</exception>
    public async Task<WalkResult> WalkPlacesAsync(string? projectName, string listFile, WalkOptions options, string? account, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var project = Project(projectName);
        var list = PlacesList.Load(listFile);
        var run = options with { Account = string.IsNullOrWhiteSpace(account) ? Path.GetFileNameWithoutExtension(project.SignInStatePath) ?? "own" : account };
        var result = await WalkOnceAsync(project, list, listFile, run, account, visible, cancellationToken).ConfigureAwait(false);
        if (result.Stop != WalkStop.SignIn || project.Config.Explore.RenewSignIn.Length == 0)
            return result;

        var command = RenewLine(project.Config.Explore.RenewSignIn, run.Account);
        if (await RunRenewAsync(command, project.Folder, cancellationToken).ConfigureAwait(false) is { } failed)
            return result with { Notes = [.. result.Notes, $"The saved sign-in ran out, and the command that renews it failed ({command}): {failed}"] };
        var again = await WalkOnceAsync(project, list, listFile, run with { MaxMinutes = Math.Max(1, run.MaxMinutes) }, account, visible, cancellationToken).ConfigureAwait(false);
        return again with
        {
            Opened = result.Opened + again.Opened,
            Found = result.Found + again.Found,
            Presses = result.Presses + again.Presses,
            Held = result.Held + again.Held,
            Notes = [.. result.Notes, .. again.Notes],
        };
    }

    private async Task<WalkResult> WalkOnceAsync(ScrapeProject project, PlacesList list, string listFile, WalkOptions options, string? account, bool visible, CancellationToken cancellationToken)
    {
        var (source, guard) = await StartGuardedAsync(project, account, visible, cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var walker = new Walker(project, source, source, guard, options, source.CaptureAsync, changed => changed.Save(listFile));
            return await walker.RunAsync(list, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the routes the app's own scripts name into a list of places: each page of the list is opened, the scripts
    /// only it (and a few like it) loads are read as text with the browser's session, and the route tables in them
    /// become known pages (<see cref="PlacesSeeds.FromScript"/>). Nothing is pressed.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="list">The list: its pages are opened, the routes are added to it.</param>
    /// <param name="account">The account to read as; null for the project's own.</param>
    /// <param name="visible">Whether the browser shows.</param>
    /// <param name="progress">Told what is being read.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    public async Task<RoutesRead> ReadRoutesAsync(string? projectName, PlacesList list, string? account, bool visible, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        // A single-page app's routes hang under a page of the server: one visit per such page.
        var pages = list.Places.Where(place => place.Kind == PlaceKind.Page && place.Address is not null && place.State != PlaceState.Refused)
            .Select(place => place.Address!.Split('#')[0].Split('?')[0])
            .Where(path => !path.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        var loadedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var (source, guard) = await StartGuardedAsync(project, account, visible, cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            // Nothing is pressed here: what a page asks for while it opens is a read.
            guard.Learning = true;
            var opened = 0;
            var again = new List<string>();
            // A page that did not open comes once more at the end: the first pages of a run wait for the app's scripts.
            for (var at = 0; at < pages.Count + again.Count; at++)
            {
                var page = at < pages.Count ? pages[at] : again[at - pages.Count];
                cancellationToken.ThrowIfCancellationRequested();
                _ = source.TakeScripts();
                progress?.Report($"opening {page}");
                var address = new Uri(new Uri(project.Config.StartUrl), page).AbsoluteUri;
                // A shared environment stops answering for a while: once more before the page is given up.
                if (!await source.GoToAsync(address, cancellationToken).ConfigureAwait(false) && !await source.GoToAsync(address, cancellationToken).ConfigureAwait(false))
                {
                    progress?.Report($"{page} did not open: {source.LastOpenProblem}");
                    if (at < pages.Count)
                        again.Add(page);
                    continue;
                }
                // The scripts are what is wanted, not the page: it gets a moment to ask for them, and is not read.
                await Task.Delay(Math.Max(project.Config.Explore.DelayMs, 1000), cancellationToken).ConfigureAwait(false);
                if (!project.IsOwnPage(source.Location) || (project.Config.Explore.LoginUrl.Length > 0 && source.Location.Contains(project.Config.Explore.LoginUrl, StringComparison.OrdinalIgnoreCase)))
                    return new RoutesRead(opened, 0, 0, 0, $"A sign-in page came ({source.Location}): the saved sign-in ran out. Run a test that signs in, then read the routes again.");
                opened++;
                foreach (var script in source.TakeScripts().Where(project.IsOwnPage).Distinct(StringComparer.Ordinal))
                {
                    if (!loadedBy.TryGetValue(script, out var by))
                        loadedBy[script] = by = [];
                    by.Add(page);
                }
            }

            // What every page loads (the framework, the shared parts) holds no page's routes.
            var few = Math.Max(3, opened / 4);
            var read = 0;
            var routes = 0;
            var fresh = 0;
            foreach (var (script, by) in loadedBy.Where(pair => pair.Value.Count <= few).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await source.ReadTextAsync(script, cancellationToken).ConfigureAwait(false) is not { } text)
                    continue;
                read++;
                foreach (var place in by.SelectMany(page => PlacesSeeds.FromScript(page, text)))
                {
                    routes++;
                    if (list.Add(place))
                        fresh++;
                }
            }

            return new RoutesRead(opened, read, routes, fresh, null);
        }
    }

    // The browser, started as the account's saved sign-in in its own profile, with the project's guards on.
    private async Task<(PlaywrightScrapeSource Source, RequestGuard Guard)> StartGuardedAsync(ScrapeProject project, string? account, bool visible, CancellationToken cancellationToken)
    {
        if (_recording is not null)
            throw new ScraperProblem("A recording is in progress, and it holds the project's browser profile: stop it first.");
        if (_sourceFor(project.Config.Platform) is not PlaywrightScrapeSource source)
            throw new ScraperProblem($"{project.Config.Platform} apps cannot be walked yet: html and blazor can.");
        if (!Uri.TryCreate(project.Config.StartUrl, UriKind.Absolute, out var start))
            throw new ScraperProblem($"{project.Name} has no StartUrl in its {ScraperConfig.FileName}.");
        if (RequestGuard.RefuseHost(start, project.Config.Explore) is { } refused)
            throw new ScraperProblem(refused);
        var guard = new RequestGuard(project.Config.Explore) { HoldWrites = true };
        try
        {
            await source.StartAsync(new ScrapeStart("about:blank", ProfileFor(project.ProfileFolder, account), visible) { SignInState = SignInFor(project.SignInStatePath, account) }, cancellationToken).ConfigureAwait(false);
            await source.GuardAsync(guard).ConfigureAwait(false);
        }
        catch
        {
            await source.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return (source, guard);
    }
}
