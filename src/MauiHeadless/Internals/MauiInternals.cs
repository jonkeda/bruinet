using System.Collections;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MauiHeadless.Internals;

/// <summary>
/// The only place MauiHeadless reaches into MAUI's internals, each by name and checked when first used:
/// a MAUI release that renames one fails here with that name, not somewhere in a test.
/// </summary>
internal static class MauiInternals
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // TapGestureRecognizer.SendTapped(View sender, Func<IElement?, Point?>? getPosition): the platform's
    // tap handler calls it; it runs the Command (when CanExecute) and raises Tapped.
    private static readonly Lazy<MethodInfo> SendTappedMethod = new(() =>
        typeof(TapGestureRecognizer).GetMethods(Instance)
            .SingleOrDefault(m => m.Name == "SendTapped" && m.GetParameters().Length == 2)
        ?? throw Missing("TapGestureRecognizer.SendTapped(View, Func<IElement, Point?>)"));

    // BindableObject.GetContext(BindableProperty) and its BindablePropertyContext.Bindings: where the
    // binding set on a property is kept.
    private static readonly Lazy<MethodInfo> GetContextMethod = new(() =>
        typeof(BindableObject).GetMethod("GetContext", Instance, [typeof(BindableProperty)])
        ?? throw Missing("BindableObject.GetContext(BindableProperty)"));

    // Application.AddWindow(Window) / RemoveWindow(Window): what IApplication.OpenWindow does once the
    // platform has made the window. It parents the window to the app and lists it in Windows, which
    // page lifecycle (SendAppearing) and Shell.Current need.
    private static readonly Lazy<MethodInfo> AddWindowMethod = new(() =>
        typeof(Application).GetMethod("AddWindow", Instance, [typeof(Window)])
        ?? throw Missing("Application.AddWindow(Window)"));

    private static readonly Lazy<MethodInfo> RemoveWindowMethod = new(() =>
        typeof(Application).GetMethod("RemoveWindow", Instance, [typeof(Window)])
        ?? throw Missing("Application.RemoveWindow(Window)"));

    /// <summary>Opens <paramref name="window"/> in <paramref name="application"/> without a platform.</summary>
    public static void AddWindow(Application application, Window window)
        => AddWindowMethod.Value.Invoke(application, [window]);

    /// <summary>Closes <paramref name="window"/> in <paramref name="application"/>.</summary>
    public static void RemoveWindow(Application application, Window window)
        => RemoveWindowMethod.Value.Invoke(application, [window]);

    // VisualElement.IsFocusedPropertyKey: IsFocused is read-only to everyone but the platform, whose
    // handler sets it through this key when focus lands or leaves; its change raises Focused and Unfocused.
    // VisualElement.IsPlatformEnabled: true once an element has a handler. A page with it set sends its alerts
    // to the window's alert manager instead of keeping them in its pending actions, where the dialogs are read.
    private static readonly Lazy<PropertyInfo> IsPlatformEnabledProperty = new(() =>
        typeof(VisualElement).GetProperty("IsPlatformEnabled", Instance)
        ?? throw Missing("VisualElement.IsPlatformEnabled"));

    private static readonly Lazy<BindablePropertyKey> IsFocusedKey = new(() =>
        typeof(VisualElement).GetField("IsFocusedPropertyKey", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(null) as BindablePropertyKey
        ?? throw Missing("VisualElement.IsFocusedPropertyKey"));

    // Page._pendingActions: what a page asks of its platform before it has one. DisplayAlertAsync,
    // DisplayPromptAsync and DisplayActionSheetAsync each add a closure that holds MAUI's own
    // (public) AlertArguments, PromptArguments or ActionSheetArguments, whose SetResult answers the app.
    private static readonly Lazy<FieldInfo> PendingActionsField = new(() =>
        typeof(Page).GetField("_pendingActions", Instance)
        ?? throw Missing("Page._pendingActions"));

    /// <summary>Takes the dialog requests waiting on <paramref name="page"/>, oldest first; other pending actions stay.</summary>
    public static IReadOnlyList<object> TakeDialogRequests(Page page)
    {
        if (PendingActionsField.Value.GetValue(page) is not List<Action> pending || pending.Count == 0)
            return [];

        var requests = new List<object>();
        foreach (var action in pending.ToList())
        {
            if (action.Target is not { } closure)
                continue;

            var request = closure.GetType().GetFields(Instance)
                .Select(field => field.GetValue(closure))
                .FirstOrDefault(value => value is Microsoft.Maui.Controls.Internals.AlertArguments
                    or Microsoft.Maui.Controls.Internals.PromptArguments
                    or Microsoft.Maui.Controls.Internals.ActionSheetArguments);
            if (request is null)
                continue;

            pending.Remove(action);
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>Marks <paramref name="element"/> as having no platform, though it has a handler.</summary>
    public static void ClearPlatformEnabled(VisualElement element)
        => IsPlatformEnabledProperty.Value.SetValue(element, false);

    /// <summary>Sets <see cref="VisualElement.IsFocused"/> as the platform does.</summary>
    public static void SetFocused(VisualElement element, bool focused)
        => element.SetValue(IsFocusedKey.Value, focused);

    /// <summary>Taps <paramref name="view"/> through <paramref name="recognizer"/>, as the platform does.</summary>
    public static void SendTapped(TapGestureRecognizer recognizer, View view)
        => SendTappedMethod.Value.Invoke(recognizer, [view, null]);

    /// <summary>The binding set on <paramref name="property"/> of <paramref name="bindable"/>, or null.</summary>
    public static BindingBase? GetBinding(BindableObject bindable, BindableProperty property)
    {
        var context = GetContextMethod.Value.Invoke(bindable, [property]);
        if (context is null)
            return null;

        var bindings = context.GetType().GetField("Bindings", Instance)?.GetValue(context)
            ?? throw Missing("BindablePropertyContext.Bindings");

        // A SetterSpecificityList<BindingBase>: the binding in force is its highest-specificity value.
        return bindings.GetType().GetMethod("GetValue", Instance, Type.EmptyTypes)?.Invoke(bindings, null) as BindingBase
            ?? (bindings as IEnumerable)?.Cast<object>().Select(Value).OfType<BindingBase>().LastOrDefault();

        static object? Value(object entry)
            => entry.GetType().GetProperty("Value")?.GetValue(entry) ?? entry;
    }

    private static MissingMemberException Missing(string member)
        => new($"MauiHeadless needs MAUI's internal {member}, which this MAUI version ({typeof(Element).Assembly.GetName().Version}) does not have. See MauiHeadless's 'MAUI internals' list.");

    // MainThread.SetCustomImplementation(Func<bool>, Action<Action>): MAUI's own hook for a platform
    // without a main thread; plain net10.0 throws from MainThread without it.
    private static readonly Lazy<MethodInfo> SetMainThreadMethod = new(() =>
        typeof(Microsoft.Maui.ApplicationModel.MainThread).GetMethod("SetCustomImplementation", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw Missing("MainThread.SetCustomImplementation(Func<bool>, Action<Action>)"));

    /// <summary>Routes <c>MainThread</c> to <paramref name="isMainThread"/> and <paramref name="beginInvoke"/>.</summary>
    public static void SetMainThread(Func<bool> isMainThread, Action<Action> beginInvoke)
        => SetMainThreadMethod.Value.Invoke(null, [isMainThread, beginInvoke]);

    /// <summary>
    /// Sets an Essentials static's implementation through its internal setter, such as
    /// <c>FileSystem.SetCurrent</c> or <c>Preferences.SetDefault</c>.
    /// </summary>
    public static void SetEssential(Type essential, string setter, object implementation)
    {
        var method = essential.GetMethod(setter, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw Missing($"{essential.Name}.{setter}");
        method.Invoke(null, [implementation]);
    }
}
