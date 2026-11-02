namespace Bruinet.Core.Testing;

/// <summary>Declares a <c>Given</c> UAT step on the method it decorates.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BddGivenAttribute : BddStepAttribute
{
    /// <param name="phrase">The phrase, e.g. <c>the server also has todos</c>.</param>
    public BddGivenAttribute(string phrase)
        : base(BddEffectiveStepKeyword.Given, phrase)
    {
    }

    /// <summary>A <c>Given</c> step whose phrase is the method name, with parameters in capitals.</summary>
    public BddGivenAttribute()
        : base(BddEffectiveStepKeyword.Given, string.Empty)
    {
    }
}

/// <summary>Declares a <c>When</c> UAT step on the method it decorates.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BddWhenAttribute : BddStepAttribute
{
    /// <param name="phrase">The phrase, e.g. <c>I set {control} to {value}</c>.</param>
    public BddWhenAttribute(string phrase)
        : base(BddEffectiveStepKeyword.When, phrase)
    {
    }

    /// <summary>A <c>When</c> step whose phrase is the method name, with parameters in capitals.</summary>
    public BddWhenAttribute()
        : base(BddEffectiveStepKeyword.When, string.Empty)
    {
    }
}

/// <summary>
/// Declares a <c>Then</c> UAT step on the method it decorates. A check written after
/// <c>And</c> or <c>But</c> takes the keyword before it, so it is declared here too.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BddThenAttribute : BddStepAttribute
{
    /// <param name="phrase">The phrase, e.g. <c>{control} should be visible</c>.</param>
    public BddThenAttribute(string phrase)
        : base(BddEffectiveStepKeyword.Then, phrase)
    {
    }

    /// <summary>A <c>Then</c> step whose phrase is the method name, with parameters in capitals.</summary>
    public BddThenAttribute()
        : base(BddEffectiveStepKeyword.Then, string.Empty)
    {
    }
}

/// <summary>
/// Declares a state the app can be put in, on the method that puts it there through test data or the service head (AI10):
/// a <c>Given</c> a scenario names (<c>Given signed in as Alice</c>), and what <c>set_state</c> may run. Only a declared state
/// can be set that way, so an agent cannot invent one that writes elsewhere than the suite's own host and database.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BddStateAttribute : BddStepAttribute
{
    /// <param name="phrase">The state, e.g. <c>signed in as {user}</c>.</param>
    public BddStateAttribute(string phrase)
        : base(BddEffectiveStepKeyword.Given, phrase)
    {
    }
}
