using Bruinet.Bdd;
using System.Reflection;
using Bruinet.Core.Testing;

namespace Bruinet.Uat;

/// <summary>
/// What a page-scoped step needs from the page it is bound on: whether its control or page phrase
/// is there, and which page is current after it. The binder follows the page with these.
/// </summary>
internal static class UatPageScope
{
    private const string ControlParameter = "control";

    /// <summary>A control of the page by its UAT name; a step may put "the" before the name.</summary>
    public static UatControlModel? FindControl(UatPageModel page, string name)
        => page.Controls.FirstOrDefault(control => control.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
           ?? (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
               ? page.Controls.FirstOrDefault(control => control.Name.Equals(name[4..].TrimStart(), StringComparison.OrdinalIgnoreCase))
               : null);

    /// <summary>
    /// The control of <paramref name="page"/> the matched step runs on, or why there is none.
    /// </summary>
    public static UatControlModel? ControlFor(UatPageModel page, BddCommandMatch match, out string? problem)
    {
        var verb = match.Pattern.ControlVerb!;
        problem = null;
        if (verb.NamesControl)
        {
            var name = match.Arguments[ControlParameter];
            var control = FindControl(page, name);
            if (control is null)
            {
                problem = $"the {page.Name} page has no control '{name}'. Its controls are {string.Join(", ", page.Controls.Select(c => c.Name).Order(StringComparer.OrdinalIgnoreCase))}.";
                return null;
            }

            if (Method(control.ControlType, verb) is null)
            {
                problem = $"'{control.Name}' on the {page.Name} page cannot '{match.Pattern.Phrase}'.";
                return null;
            }

            return control;
        }

        var declaring = page.Controls.Where(control => Declares(control.ControlType, match.Pattern)).ToList();
        if (declaring.Count == 1)
        {
            return declaring[0];
        }

        problem = declaring.Count == 0
            ? $"no control on the {page.Name} page has '{match.Pattern.Phrase}'."
            : $"'{match.Pattern.Phrase}' is on several controls of the {page.Name} page ({string.Join(", ", declaring.Select(c => c.Name))}). Put {{control}} in the phrase.";
        return null;
    }

    /// <summary>
    /// The page after a control verb: for a verb that navigates, where the control says it goes;
    /// else the page its method returns; otherwise the page it ran on. Null when it may have left
    /// the page for one it does not say. A declared destination wins because a control's verb
    /// returns its own scope (a link's Click returns the page the link is on).
    /// </summary>
    public static string? PageAfter(UatPageModel page, UatControlModel control, BddControlVerb verb, IReadOnlyDictionary<string, UatPageModel> pages)
    {
        if (verb.Navigates && control.NavigatesTo is { } declared)
        {
            return declared;
        }

        if (Method(control.ControlType, verb)?.ReturnType is { } returned &&
            pages.Values.FirstOrDefault(candidate => candidate.PageType == returned) is { } returnedPage)
        {
            return returnedPage.Key;
        }

        if (!verb.Navigates)
        {
            return page.Key;
        }

        return control.NavigatesTo;
    }

    // The method the runtime would invoke: by name, taking the step's arguments.
    private static MethodInfo? Method(Type controlType, BddControlVerb verb)
        => controlType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == verb.MethodName &&
                                      method.GetParameters().Count(parameter => !parameter.IsOptional) <= verb.ArgumentCount &&
                                      method.GetParameters().Length >= verb.ArgumentCount);

    private static bool Declares(Type controlType, BddCommandPattern pattern)
    {
        for (Type? type = controlType; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Any(method => method.GetCustomAttributes<BddStepAttribute>(inherit: false)
                    .Any(attribute => attribute.Keyword == pattern.Keyword && attribute.Phrase == pattern.Phrase)))
            {
                return true;
            }
        }

        return false;
    }
}
