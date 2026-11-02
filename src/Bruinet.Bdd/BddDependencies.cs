namespace Bruinet.Bdd;

/// <summary>
/// One row of a suite's <c>## Dependencies</c> table: how its tests get one thing outside the app (AI11). A suite with no
/// table behaves as before.
/// </summary>
/// <param name="Name">The dependency's name, as the suite's host asks for it (<c>backend</c>, <c>db</c>).</param>
/// <param name="Kind"><c>http</c>, <c>sqlserver</c> or <c>sqlite</c>.</param>
/// <param name="Mode">How it is answered in this run: the table's, or <c>BRUINET_DEP_&lt;NAME&gt;</c>'s over it.</param>
/// <param name="Where">What the mode needs: a base URL, a preset, a stubs class, a seeded file, a connection's name.</param>
public sealed record BddDependency(string Name, string Kind, string Mode, string Where)
{
    /// <summary>The modes each kind takes (AI11's design): real, recorded and replayed, stubbed, or a seeded copy.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ModesByKind =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["http"] = ["real", "record", "replay", "refresh", "stub"],
            ["sqlserver"] = ["real", "record", "replay"],
            ["sqlite"] = ["real", "copy", "record", "replay"],
        };

    /// <summary>The environment variable that overrides this dependency's mode for a run: <c>BRUINET_DEP_BACKEND=real</c>.</summary>
    public string OverrideVariable => "BRUINET_DEP_" + new string([.. Name.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_')]);

    /// <summary>Whether its mode answers it inside the test process, so a hermetic run allows it.</summary>
    public bool IsHermetic => !string.Equals(Mode, "real", StringComparison.OrdinalIgnoreCase) && !string.Equals(Mode, "record", StringComparison.OrdinalIgnoreCase)
                              && !string.Equals(Mode, "refresh", StringComparison.OrdinalIgnoreCase);

    /// <summary>What is wrong with the row: an unknown kind, or a mode its kind does not take; null when nothing.</summary>
    public string? Problem
        => !ModesByKind.TryGetValue(Kind, out var modes)
            ? $"Dependency {Name}: kind '{Kind}' is not one of {string.Join(", ", ModesByKind.Keys)}."
            : !modes.Contains(Mode, StringComparer.OrdinalIgnoreCase)
                ? $"Dependency {Name}: a {Kind} dependency takes {string.Join(", ", modes)}, not '{Mode}'."
                : null;
}

/// <summary>The fields of <c>## Runtime</c> that set a suite's HTTP replay (AI11).</summary>
public static class BddReplayFields
{
    /// <summary><c>true</c>: every dependency answered in the process; one named <c>real</c> or <c>record</c> fails the run.</summary>
    public const string Hermetic = "Hermetic";

    /// <summary>Headers and fields never written to a recording, beside the defaults (comma-separated).</summary>
    public const string HttpRedact = "HttpRedact";

    /// <summary><c>none</c> (the default) or <c>recorded</c>: how a replayed streamed answer is paced.</summary>
    public const string HttpReplayPace = "HttpReplayPace";
}

/// <summary>A suite's dependencies as a run gets them.</summary>
public static class BddDependencies
{
    /// <summary>
    /// The table's rows with each run override applied (<see cref="BddDependency.OverrideVariable"/>), in table order.
    /// </summary>
    /// <param name="config">The suite's config.</param>
    /// <param name="environment">Reads an environment variable; the process's when null.</param>
    public static IReadOnlyList<BddDependency> Effective(BddConfig config, Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        environment ??= Environment.GetEnvironmentVariable;
        return [.. config.Dependencies.Select(dependency => environment(dependency.OverrideVariable) is { Length: > 0 } mode
            ? dependency with { Mode = mode.Trim().ToLowerInvariant() }
            : dependency)];
    }

    /// <summary>The dependency named <paramref name="name"/> as this run gets it; null when the suite names none.</summary>
    public static BddDependency? Find(BddConfig config, string name, Func<string, string?>? environment = null)
        => Effective(config, environment).FirstOrDefault(dependency => string.Equals(dependency.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the suite is hermetic (<c>| Hermetic | true |</c>).</summary>
    public static bool IsHermetic(BddConfig config)
        => config is not null && config.Runtime.TryGetValue(BddReplayFields.Hermetic, out var value) && bool.TryParse(value, out var hermetic) && hermetic;

    /// <summary>
    /// What stops this run: each row's <see cref="BddDependency.Problem"/>, and in a hermetic suite each dependency a run
    /// sends outside the process. Empty when the run may go.
    /// </summary>
    public static IReadOnlyList<string> Problems(BddConfig config, Func<string, string?>? environment = null)
    {
        var effective = Effective(config, environment);
        var problems = effective.Select(dependency => dependency.Problem).OfType<string>().ToList();
        if (IsHermetic(config))
        {
            problems.AddRange(effective.Where(dependency => dependency.Problem is null && !dependency.IsHermetic)
                .Select(dependency => $"Dependency {dependency.Name} is '{dependency.Mode}' in a hermetic suite: replay, stub or copy it, or set Hermetic to false."));
        }

        return problems;
    }
}
