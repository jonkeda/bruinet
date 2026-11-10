using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// Declares verbs in code for every element that matches a rule, instead of on each element in XAML.
/// </summary>
/// <remarks>
/// <para>
/// <b>Still declared, not inferred.</b> The app writes each rule, so the published surface is
/// in one reviewable place, as it is with <see cref="GestureAutomation.VerbsProperty"/> in markup.
/// Use a rule when one control type or opt-in attached property should always answer a verb,
/// for example every pull-to-refresh list answering <c>SwipeDown</c>.
/// </para>
/// <para>
/// A rule never overrides verbs that are already set, from XAML or from an earlier rule. Register
/// rules in <c>CreateMauiApp</c>, before the first page loads.
/// </para>
/// <example>
/// <code>
/// BruinetVerbRules.Publish("Submit", e =&gt; e is Entry entry &amp;&amp; MyAutomation.GetSubmit(entry));
/// </code>
/// </example>
/// </remarks>
public static class BruinetVerbRules
{
    /// <summary>
    /// Publishes <paramref name="verbs"/> on every element for which <paramref name="applies"/>
    /// returns <c>true</c> when its handler connects.
    /// </summary>
    /// <param name="verbs">The verbs, as in <see cref="GestureAutomation.SetVerbs"/>.</param>
    /// <param name="applies">Whether an element answers the verbs.</param>
    /// <param name="recheckOnLoaded">
    /// Also test the element once when it loads. Use this when the rule reads state that is set
    /// after the handler connects, such as gesture recognizers added in code.
    /// </param>
    public static void Publish(string verbs, Func<VisualElement, bool> applies, bool recheckOnLoaded = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verbs);
        ArgumentNullException.ThrowIfNull(applies);

        ViewHandler.ViewMapper.AppendToMapping($"BruinetVerbRule:{verbs}", (_, view) =>
        {
            if (view is not VisualElement element || TryApply(element, verbs, applies) || !recheckOnLoaded)
                return;

            element.Loaded += OnLoaded;

            void OnLoaded(object? sender, EventArgs e)
            {
                element.Loaded -= OnLoaded;
                TryApply(element, verbs, applies);
            }
        });
    }

    private static bool TryApply(VisualElement element, string verbs, Func<VisualElement, bool> applies)
    {
        if (GestureAutomation.GetVerbs(element) is not null)
            return true;
        if (!applies(element))
            return false;

        GestureAutomation.SetVerbs(element, verbs);
        return true;
    }
}
