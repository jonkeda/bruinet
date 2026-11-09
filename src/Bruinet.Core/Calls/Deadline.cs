using System.Diagnostics;

namespace Bruinet.Core.Calls;

/// <summary>
/// The point in time a phase of a call must finish by. Created once per phase and passed down,
/// so nothing inside the phase starts a budget of its own.
/// </summary>
public readonly struct Deadline
{
    private readonly Stopwatch _clock;
    private readonly int _budgetMs;

    private Deadline(int budgetMs)
    {
        _budgetMs = Math.Max(0, budgetMs);
        _clock = Stopwatch.StartNew();
    }

    /// <summary>A deadline <paramref name="milliseconds"/> from now.</summary>
    public static Deadline In(int milliseconds) => new(milliseconds);

    /// <summary>The whole budget this deadline was made with.</summary>
    public int BudgetMs => _budgetMs;

    /// <summary>How long the phase has run.</summary>
    public long ElapsedMs => _clock?.ElapsedMilliseconds ?? 0;

    /// <summary>What is left, never negative.</summary>
    public int RemainingMs => (int)Math.Max(0, _budgetMs - ElapsedMs);

    /// <summary>Whether the budget is spent.</summary>
    public bool IsPassed => ElapsedMs >= _budgetMs;
}
