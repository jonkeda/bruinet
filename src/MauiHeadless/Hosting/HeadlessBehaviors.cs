using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace MauiHeadless.Hosting;

/// <summary>
/// Removes platform behaviors (<see cref="PlatformBehavior{TView, TPlatformView}"/>, such as the
/// toolkit's TouchBehavior) from an app's views: without a platform view they cannot attach. A
/// behavior with a <c>Command</c> becomes a <see cref="TapGestureRecognizer"/> with that command, so
/// the tap it stood for still reaches the app.
/// </summary>
/// <remarks>
/// Views made later are stripped by an implicit style on the host types, applied to derived types;
/// rows MauiHeadless realizes are stripped as they are made. An app's own implicit style for a derived
/// type hides this one, so a fixture names such a type as an extra host.
/// </remarks>
public static class HeadlessBehaviors
{
    /// <summary>The types styled by default: pages, content views and list views.</summary>
#pragma warning disable CS0618 // ListView is obsolete, but apps still show it, and its cells need the same stripping.
    public static IReadOnlyList<Type> DefaultHosts { get; } = [typeof(Page), typeof(ContentView), typeof(ListView)];
#pragma warning restore CS0618

    /// <summary>Set by the implicit style: strips the element now and again just before it is parented.</summary>
    public static readonly BindableProperty StripProperty = BindableProperty.CreateAttached(
        "Strip", typeof(bool), typeof(HeadlessBehaviors), false,
        propertyChanged: (bindable, _, value) =>
        {
            if (value is not true || bindable is not Element element)
                return;

#pragma warning disable CS0618 // ListView is obsolete, but apps still show it, and its cells need the same stripping.
            if (element is ListView list)
                PrepareListView(list);
#pragma warning restore CS0618
            Strip(element);
            // The style can apply during InitializeComponent, before XAML adds the behaviors.
            element.ParentChanging += (_, _) => Strip(element);
        });

    private static readonly ConditionalWeakTable<Application, HashSet<Type>> Installed = new();

    /// <summary>
    /// Styles <see cref="DefaultHosts"/> and <paramref name="extraHosts"/> in <paramref name="application"/>,
    /// and again when its resources are replaced. Call it before the app builds its pages; repeating it
    /// adds only new hosts.
    /// </summary>
    public static void Install(Application application, IEnumerable<Type>? extraHosts = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        var first = !Installed.TryGetValue(application, out var hosts);
        hosts ??= Installed.GetValue(application, _ => []);
        foreach (var host in DefaultHosts.Concat(extraHosts ?? []))
            hosts.Add(host);

        AddStyles(application, hosts);
        if (first)
        {
            application.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Application.Resources))
                    AddStyles(application, hosts);
            };
        }
    }

    /// <summary>Strips <paramref name="element"/> and every logical descendant.</summary>
    public static void Strip(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is VisualElement visual)
        {
            foreach (var behavior in visual.Behaviors.Where(IsPlatformBehavior).ToList())
            {
                visual.Behaviors.Remove(behavior);
                if (visual is View view && CommandOf(behavior) is { } command)
                    view.GestureRecognizers.Add(new TapGestureRecognizer { Command = command, CommandParameter = ParameterOf(behavior) });
            }
        }

        foreach (var child in ((IElementController)element).LogicalChildren.ToList())
            Strip(child);
    }

    // A theme change may clear Resources but leaves merged dictionaries, so the styles live in one.
    private static void AddStyles(Application application, IEnumerable<Type> hosts)
    {
        var merged = application.Resources.MergedDictionaries;
        if (merged.OfType<StripDictionary>().FirstOrDefault() is not { } dictionary)
        {
            dictionary = new StripDictionary();
            merged.Add(dictionary);
        }

        foreach (var host in hosts)
        {
            if (dictionary.ContainsKey(host.FullName!))
                continue;

            var style = new Style(host) { ApplyToDerivedTypes = true };
            style.Setters.Add(new Setter { Property = StripProperty, Value = true });
            dictionary.Add(style);
        }
    }

    private sealed class StripDictionary : ResourceDictionary;

    private static bool IsPlatformBehavior(Behavior behavior)
    {
        for (var type = behavior.GetType(); type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PlatformBehavior<,>))
                return true;
        }

        return false;
    }

    private static ICommand? CommandOf(Behavior behavior)
        => behavior.GetType().GetProperty("Command")?.GetValue(behavior) as ICommand;

    private static object? ParameterOf(Behavior behavior)
        => behavior.GetType().GetProperty("CommandParameter")?.GetValue(behavior);

    #region ListView cells
#pragma warning disable CS0618 // ListView is obsolete, but apps still show it, and its cells need the same stripping.

    private static readonly ConditionalWeakTable<DataTemplate, DataTemplate> Wrapped = new();
    private static readonly ConditionalWeakTable<DataTemplate, object> Wrappers = new();
    private static readonly ConditionalWeakTable<ListView, object> Prepared = new();

    // Wraps a ListView's item template so each new cell is stripped just before it joins the list.
    // MAUI drops a template set inside the same property's change, so it is wrapped when the items or
    // the binding context arrive, before any cell is made.
    internal static void PrepareListView(ListView list)
    {
        WrapTemplate(list);
        if (Prepared.TryGetValue(list, out _))
            return;

        Prepared.Add(list, new object());
        list.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ListView.ItemsSource) or nameof(BindableObject.BindingContext))
                WrapTemplate(list);
        };
    }

    private static void WrapTemplate(ListView list)
    {
        switch (list.ItemTemplate)
        {
            // MAUI ignores a new template on a RetainElement list with a selector; its cells' views
            // are stripped by the host styles instead.
            case DataTemplateSelector when list.CachingStrategy == ListViewCachingStrategy.RetainElement:
                break;
            case DataTemplateSelector selector when selector is not StrippingSelector:
                list.ItemTemplate = new StrippingSelector(selector);
                break;
            case { } template when template is not DataTemplateSelector && !Wrappers.TryGetValue(template, out _):
                list.ItemTemplate = Wrap(template);
                break;
        }
    }

    private static DataTemplate Wrap(DataTemplate template) => Wrapped.GetValue(template, original =>
    {
        var wrapper = new DataTemplate(() =>
        {
            var content = original.CreateContent();
            // A cell may build its view on binding, which comes before it is parented.
            if (content is ViewCell cell)
                cell.ParentChanging += (_, _) => { if (cell.View is { } view) Strip(view); };
            return content;
        });
        Wrappers.Add(wrapper, original);
        return wrapper;
    });

    private sealed class StrippingSelector(DataTemplateSelector inner) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => Wrap(inner.SelectTemplate(item, container));
    }

    #pragma warning restore CS0618
    #endregion
}
