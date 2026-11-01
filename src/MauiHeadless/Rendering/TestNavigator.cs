using Microsoft.Maui.Controls;
using MauiHeadless.Assertions;
using MauiHeadless.Inspection;
using MauiHeadless.Snapshots;

namespace MauiHeadless.Rendering;

/// <summary>
/// A rendered window's navigation, read and driven as a user would: where it is, what is under it,
/// and back. MAUI's own <c>NavigationPage</c>, modal stack and Shell do the navigating; without a
/// platform their pushes and pops complete at once.
/// </summary>
public sealed class TestNavigator
{
    private readonly MauiTestContext _context;
    private readonly Window _window;

    internal TestNavigator(MauiTestContext context, Window window)
    {
        _context = context;
        _window = window;
    }

    /// <summary>The page the window shows now.</summary>
    public Page CurrentPage
    {
        get
        {
            _context.Dispatcher.RunPending();
            return Screen.CurrentPage(_window)
                ?? throw new InvalidOperationException("The window shows no page.");
        }
    }

    /// <summary>The pages of the navigation the current page is on, root first.</summary>
    public IReadOnlyList<Page> Stack
    {
        get
        {
            _context.Dispatcher.RunPending();
            return CurrentPage.Navigation.NavigationStack.ToList();
        }
    }

    /// <summary>The window's modal pages, the lowest first.</summary>
    public IReadOnlyList<Page> ModalStack
    {
        get
        {
            _context.Dispatcher.RunPending();
            return _window.Navigation.ModalStack.ToList();
        }
    }

    /// <summary>Shell's location, such as <c>//home/details</c>, when the window shows a Shell; else null.</summary>
    public string? Location => _window.Page is Shell shell ? shell.CurrentState?.Location?.ToString() : null;

    /// <summary>Goes back as the back button does: the top modal first, else one page.</summary>
    /// <exception cref="InvalidOperationException">There is nothing to go back to.</exception>
    public void GoBack()
    {
        if (ModalStack.Count > 0)
        {
            Await(() => _window.Navigation.PopModalAsync(), "pop the modal page");
            return;
        }

        if (Stack.Count <= 1)
            throw new InvalidOperationException($"There is nothing to go back to: {CurrentPage.GetType().Name} is the navigation's root.");

        Await(() => CurrentPage.Navigation.PopAsync(), "go back");
    }

    /// <summary>Goes to a Shell route, as <c>Shell.Current.GoToAsync</c> does.</summary>
    /// <exception cref="InvalidOperationException">The window shows no Shell.</exception>
    public void GoTo(string route)
    {
        if (_window.Page is not Shell shell)
            throw new InvalidOperationException("GoTo needs a Shell: render the app's Shell, or navigate through the page.");

        Await(() => shell.GoToAsync(route), $"go to '{route}'");
    }

    /// <summary>Asserts the window shows a <typeparamref name="TPage"/>, and returns it.</summary>
    /// <exception cref="MauiHeadlessAssertionException">It shows another page.</exception>
    public TPage ShouldBeOn<TPage>() where TPage : Page
    {
        var current = CurrentPage;
        if (current is TPage page)
            return page;

        throw new MauiHeadlessAssertionException(
            $"Expected the window to show {typeof(TPage).Name}, but it shows {current.GetType().Name}.\n\nStack: "
            + string.Join(" > ", Stack.Select(p => p.GetType().Name))
            + $"\n\nScreen:\n{TreeSnapshot.Format(ElementQueries.Read(_window), TreeSnapshot.ForMessages)}");
    }

    private void Await(Func<Task> navigate, string what)
    {
        Task? task = null;
        _context.Dispatcher.Run(() => { task = navigate(); });
        if (!_context.Dispatcher.WaitFor(() => task!.IsCompleted, _context.Options.WaitTimeout))
            throw new TimeoutException($"Could not {what} within {_context.Options.WaitTimeout.TotalMilliseconds:0} ms.");
        task!.GetAwaiter().GetResult();
    }
}
