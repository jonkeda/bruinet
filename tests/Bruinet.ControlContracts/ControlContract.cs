using System.Diagnostics;
using Bruinet.Core.Exceptions;
using Xunit;

namespace Bruinet.ControlContracts;

/// <summary>
/// What every control contract shares. A head derives one class per contract, gives it a fresh page
/// with the controls in the state the contract names, and gets every test of the contract. A test the
/// head cannot keep is overridden with <c>[Fact(Skip = "why")]</c>, so a difference between heads is
/// written down rather than missing. The contracts follow the Core interfaces' documentation: a null
/// argument skips, a missing element answers null, an assert that fails throws
/// <see cref="ControlAssertionException"/>, and every action returns the control's scope.
/// </summary>
/// <typeparam name="TScope">The page the controls' calls return.</typeparam>
public abstract class ControlContract<TScope> where TScope : class
{
    /// <summary>The page the controls' calls return, the same instance each time.</summary>
    protected abstract TScope Scope { get; }

    /// <summary>
    /// How long a wait or assert that must fail may take, in milliseconds. A shared build agent can spend most of a
    /// short budget before the first attempt, and a call that never got to ask fails differently.
    /// </summary>
    protected virtual int ShortTimeoutMs => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null ? 300 : 1500;

    /// <summary>How long the page may take to show an action's effect, in milliseconds.</summary>
    protected virtual int EffectTimeoutMs => 3000;

    /// <summary>The call returned the control's scope, for fluent chaining.</summary>
    protected void ReturnsScope(TScope returned) => Assert.Same(Scope, returned);

    /// <summary>The assert failed the Bruinet way.</summary>
    protected static void Fails(Action assert) => Assert.ThrowsAny<ControlAssertionException>(assert);

    /// <summary>Reads until the page shows <paramref name="expected"/>, then asserts it.</summary>
    protected void Shows<T>(T expected, Func<T> read)
    {
        var watch = Stopwatch.StartNew();
        var actual = read();
        while (!EqualityComparer<T>.Default.Equals(actual, expected) && watch.ElapsedMilliseconds < EffectTimeoutMs)
        {
            Thread.Sleep(20);
            actual = read();
        }

        Assert.Equal(expected, actual);
    }
}
