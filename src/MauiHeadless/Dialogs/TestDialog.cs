using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;
using MauiHeadless.Interaction;

namespace MauiHeadless.Dialogs;

/// <summary>What kind of dialog the app raised.</summary>
public enum DialogKind
{
    /// <summary><c>DisplayAlertAsync</c>: a message and one or two buttons.</summary>
    Alert,

    /// <summary><c>DisplayPromptAsync</c>: a message, a field, accept and cancel.</summary>
    Prompt,

    /// <summary><c>DisplayActionSheetAsync</c>: a list of choices.</summary>
    ActionSheet,
}

/// <summary>
/// A dialog the app raised with <c>DisplayAlertAsync</c>, <c>DisplayPromptAsync</c> or
/// <c>DisplayActionSheetAsync</c>, shown as a platform shows it: over the page, with its title, its
/// message, its field and its buttons. Clicking a button answers MAUI's own request, so the app's
/// <c>await</c> resumes with what a user chose.
/// </summary>
/// <remarks>
/// Without a platform MAUI keeps the request on the page until one appears; MauiHeadless takes it from
/// there (<see cref="DialogRequests"/>) and puts this node first under the window.
/// </remarks>
public sealed class TestDialog : ContentView
{
    private readonly Action<object?> _answer;
    private readonly Entry? _input;

    internal TestDialog(Page owner, DialogKind kind, string? title, string? message, string? accept, string? cancel,
        IReadOnlyList<string> choices, string? placeholder, string? initialValue, Action<object?> answer)
    {
        Owner = owner;
        Kind = kind;
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        Accept = accept ?? string.Empty;
        Cancel = cancel ?? string.Empty;
        Choices = choices;
        _answer = answer;
        AutomationId = "MauiHeadlessDialog";

        var layout = new VerticalStackLayout();
        if (Title.Length > 0)
            layout.Add(new Label { AutomationId = "DialogTitle", Text = Title });
        if (Message.Length > 0)
            layout.Add(new Label { AutomationId = "DialogMessage", Text = Message });
        if (kind == DialogKind.Prompt)
        {
            _input = new Entry { AutomationId = "DialogInput", Placeholder = placeholder, Text = initialValue };
            layout.Add(_input);
        }

        foreach (var choice in choices)
            layout.Add(DialogButton(choice, () => Answer(choice)));
        if (Accept.Length > 0)
            layout.Add(DialogButton(Accept, () => Answer(kind switch { DialogKind.Alert => true, DialogKind.Prompt => _input!.Text ?? string.Empty, _ => Accept })));
        if (Cancel.Length > 0)
            layout.Add(DialogButton(Cancel, () => Answer(kind switch { DialogKind.Alert => false, DialogKind.Prompt => null, _ => Cancel })));
        Content = layout;
    }

    /// <summary>The page the app raised it on.</summary>
    public Page Owner { get; }

    /// <summary>What kind of dialog it is.</summary>
    public DialogKind Kind { get; }

    /// <summary>Its title; empty when it has none.</summary>
    public string Title { get; }

    /// <summary>Its message; empty when it has none.</summary>
    public string Message { get; }

    /// <summary>The accept button's text (or an action sheet's destruction choice); empty when it has none.</summary>
    public string Accept { get; }

    /// <summary>The cancel button's text; empty when it has none.</summary>
    public string Cancel { get; }

    /// <summary>An action sheet's choices; empty for an alert or a prompt.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>Whether it has been answered and is gone.</summary>
    public bool IsAnswered { get; private set; }

    /// <summary>Clicks the button showing <paramref name="text"/>.</summary>
    /// <exception cref="ElementNotInteractableException">The dialog has no such button.</exception>
    public TestDialog Choose(string text)
    {
        var button = ((VerticalStackLayout)Content).Children.OfType<Button>().FirstOrDefault(b => b.Text == text)
            ?? throw new ElementNotInteractableException($"The dialog '{Title}' has no button '{text}'.");
        button.Click();
        return this;
    }

    /// <summary>
    /// Closes it as a device's back key does, without a button: an alert answers false, a prompt
    /// null, an action sheet its cancel (null when it has none).
    /// </summary>
    public TestDialog Dismiss()
    {
        Answer(Kind switch { DialogKind.Alert => false, DialogKind.ActionSheet when Cancel.Length > 0 => Cancel, _ => null });
        return this;
    }

    /// <summary>Types into a prompt's field.</summary>
    /// <exception cref="ElementNotInteractableException">The dialog is no prompt.</exception>
    public TestDialog Type(string text)
    {
        if (_input is null)
            throw new ElementNotInteractableException($"The dialog '{Title}' has no field to type into.");
        _input.SetText(text);
        return this;
    }

    private Button DialogButton(string text, Action clicked)
    {
        var button = new Button { Text = text };
        button.Clicked += (_, _) => clicked();
        return button;
    }

    private void Answer(object? answer)
    {
        if (IsAnswered)
            return;

        IsAnswered = true;
        DialogRequests.Remove(this);
        _answer(answer);
    }

    internal static TestDialog For(Page owner, object request) => request switch
    {
        AlertArguments alert => new TestDialog(owner, DialogKind.Alert, alert.Title, alert.Message, alert.Accept, alert.Cancel,
            [], null, null, answer => alert.SetResult(answer is true)),
        PromptArguments prompt => new TestDialog(owner, DialogKind.Prompt, prompt.Title, prompt.Message, prompt.Accept, prompt.Cancel,
            [], prompt.Placeholder, prompt.InitialValue, answer => prompt.SetResult(answer as string)),
        ActionSheetArguments sheet => new TestDialog(owner, DialogKind.ActionSheet, sheet.Title, null, sheet.Destruction, sheet.Cancel,
            [.. sheet.Buttons ?? []], null, null, answer => sheet.SetResult(answer as string)),
        _ => throw new ArgumentException($"{request.GetType().Name} is no dialog request.", nameof(request)),
    };
}
