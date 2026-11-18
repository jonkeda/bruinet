namespace Bruinet.Bdd;

/// <summary>
/// The name a step uses for a page object, in <c>Given I am on the {page} page</c>: <c>[BddPage("Login")]</c> on its type,
/// or on a fixture's property that returns it. Without it the name is inferred (<c>LoginPage</c> is <c>Login</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class BddPageAttribute : Attribute
{
    /// <param name="name">The page's name in a step.</param>
    public BddPageAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>The page's name in a step.</summary>
    public string Name { get; }
}

/// <summary>
/// The words a step uses for a control, the text that fills <c>{control}</c>. On a property it is the control's name
/// (<c>[BddControl("Role")]</c>); without it the name is inferred (<c>RoleCombo</c> is <c>Role</c>). On a method of a
/// page or component it may hold placeholders that are the method's parameters
/// (<c>[BddControl("the filter above {column}")]</c>), and the method returns the control the words name.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
public sealed class BddControlAttribute : Attribute
{
    /// <param name="name">The control's words in a step, with <c>{parameter}</c> placeholders on a method.</param>
    public BddControlAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>The control's words in a step.</summary>
    public string Name { get; }
}

/// <summary>
/// The word for a kind of control, on its type: <c>[BddKind("field")]</c> on a form's row, <c>[BddKind("tab")]</c> on a
/// tab. A step may then name a control of that kind by its name alone (<c>Name</c>) or with the kind before or after it
/// (<c>the field Name</c>, <c>the Name field</c>); a property of that type needs no <c>[BddControl]</c>, its name is its
/// member's (<c>HourlyRate</c> is <c>Hourly rate</c>). The kind is also how a step tells two controls of one name
/// apart (<c>the tab Financial</c>, <c>the part Financial</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class BddKindAttribute : Attribute
{
    /// <param name="kind">The kind's word in a step, such as <c>field</c>.</param>
    public BddKindAttribute(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind.Trim();
    }

    /// <summary>The kind's word in a step.</summary>
    public string Kind { get; }
}

/// <summary>
/// On a page's control property: tapping the control always goes to this page, so the binder knows the page after it
/// without an <c>I should be on ...</c> step.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BddNavigatesToAttribute : Attribute
{
    /// <param name="page">The page object type the control goes to.</param>
    public BddNavigatesToAttribute(Type page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Page = page;
    }

    /// <summary>The page object type the control goes to.</summary>
    public Type Page { get; }
}
