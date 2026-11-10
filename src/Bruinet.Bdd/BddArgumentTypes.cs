using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Bdd;

/// <summary>
/// A user named in a step, from the config's <c>Apps</c> table: <c>When I assign "Buy milk" to Eva</c>.
/// A phrase parameter of this type is checked against the table when the step is bound.
/// </summary>
/// <param name="Name">The user's name, as the <c>User</c> column (or the app's name) has it.</param>
/// <param name="Role">The <c>Role</c> column of the user's first app, when the table has one.</param>
/// <param name="Apps">The user's apps.</param>
public sealed record BddUserRole(string Name, string? Role, IReadOnlyList<BddAppConfig> Apps)
{
    /// <summary>The user called <paramref name="name"/>, with their apps from <paramref name="apps"/>.</summary>
    public static BddUserRole Of(string name, IReadOnlyList<BddAppConfig> apps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(apps);

        var owned = apps.Where(app => app.User.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var role = owned.Select(app => app.Values.TryGetValue("Role", out var value) ? value.Trim() : null).FirstOrDefault(value => value is { Length: > 0 });
        return new BddUserRole(owned.Length > 0 ? owned[0].User : name, role, owned);
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Marks a type that a scenario can write inline; it is read with its static <c>Parse(string)</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class BddArgumentTypeAttribute : Attribute
{
}

/// <summary>Reads a <typeparamref name="T"/> from the text of a step argument or a table cell.</summary>
/// <remarks>Found in the fixture's scan; created from the scenario's services, so it can look things up.</remarks>
public interface IBddArgumentConverter<out T>
{
    /// <summary>The value <paramref name="text"/> stands for.</summary>
    /// <exception cref="FormatException">The text does not stand for a value; the message says why.</exception>
    T Read(string text);
}

/// <summary>The app's own argument types: <see cref="BddArgumentTypeAttribute"/> types and converters.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddArgumentTypes
{
    private readonly Dictionary<Type, MethodInfo> _parse = [];
    private readonly Dictionary<Type, Type> _converters = [];

    /// <summary>No app types.</summary>
    public static BddArgumentTypes Empty { get; } = new();

    /// <summary>The argument types among <paramref name="types"/>.</summary>
    public static BddArgumentTypes Discover(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var result = new BddArgumentTypes();
        foreach (var type in types.Distinct())
        {
            result.Add(type);
        }

        return result;
    }

    /// <summary>The types registered with an attribute or a converter.</summary>
    public IEnumerable<Type> Registered => _parse.Keys.Concat(_converters.Keys);

    /// <summary>Whether <paramref name="type"/> is read by one of these.</summary>
    public bool IsRegistered(Type type) => _parse.ContainsKey(type) || _converters.ContainsKey(type) || ParseOf(type) is not null;

    /// <summary>Reads <paramref name="text"/> as <paramref name="type"/>.</summary>
    /// <param name="text">The value, without quotes.</param>
    /// <param name="type">A registered type.</param>
    /// <param name="services">The scenario's services; at bind time null, when a converter that needs them is skipped.</param>
    /// <param name="value">The value.</param>
    /// <param name="problem">Why it could not be read.</param>
    public bool TryRead(string text, Type type, IServiceProvider? services, out object? value, out BddValueProblem? problem)
    {
        value = null;
        problem = null;
        try
        {
            if ((_parse.TryGetValue(type, out var parse) ? parse : ParseOf(type)) is { } parseMethod)
            {
                parse = parseMethod;
                value = parse.Invoke(null, [text]);
                return true;
            }

            var converterType = _converters[type];
            var converter = services is not null
                ? ActivatorUtilities.CreateInstance(services, converterType)
                : converterType.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(converterType) : null;
            if (converter is null)
            {
                // A converter that needs services is checked when the step runs.
                return true;
            }

            value = converterType.GetInterfaceMap(typeof(IBddArgumentConverter<>).MakeGenericType(type)).TargetMethods[0].Invoke(converter, [text]);
            return true;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is FormatException or ArgumentException or OverflowException or KeyNotFoundException or InvalidOperationException)
        {
            problem = new BddValueProblem("UATA001", $"'{text}' is not a {type.Name}: {exception.InnerException.Message.TrimEnd('.')}.");
            return false;
        }
    }

    // A [BddArgumentType] type is found from the parameter itself, scanned or not.
    private static MethodInfo? ParseOf(Type type)
        => type.GetCustomAttribute<BddArgumentTypeAttribute>() is not null
            ? type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)])
            : null;

    private void Add(Type type)
    {
        if (ParseOf(type) is { } parse)
        {
            _parse[type] = parse;
        }

        if (type is { IsAbstract: false, IsClass: true })
        {
            foreach (var converted in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBddArgumentConverter<>))
                         .Select(i => i.GetGenericArguments()[0]))
            {
                _converters[converted] = type;
            }
        }
    }
}
