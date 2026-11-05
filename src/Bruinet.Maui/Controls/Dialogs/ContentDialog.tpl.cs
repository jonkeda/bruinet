using Bruinet.Core.Testing;
using Bruinet.Maui.Containers;
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Text;

namespace Bruinet.Maui.Controls.Dialogs;

/// <summary>
/// MAUI ContentDialog control for WinUI3 popups produced by DisplayAlert and
/// DisplayPromptAsync.
/// </summary>
/// <typeparam name="TParent">The parent scope type.</typeparam>
public partial class ContentDialog<TParent> : ContainerObjectBase<TParent, ContentDialog<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>
    /// Creates a ContentDialog control in the current scope.
    /// </summary>
    /// <param name="parentScope">The parent scope that owns the dialog interaction.</param>
    public ContentDialog(IMauiScope<TParent> parentScope)
        : base(parentScope, Locator.ByClassName("ContentDialog"))
    {
    }

    /// <inheritdoc />
    protected override bool CacheContainerRoot => false;

    /// <inheritdoc />
    protected override bool AsksParent => false;

    /// <inheritdoc />
    protected override IMauiElement FindContainerRootElement()
    {
        return Context.AppElement.TryFindActiveDialog()
            ?? throw new ElementNotFoundException("No active content dialog was found.");
    }

    #region Parts

    /// <summary>
    /// Finds a dialog button by visible text.
    /// </summary>
    public Button<ContentDialog<TParent>> DialogButton(string buttonText)
        => new(this, Locator.ByName(buttonText));

    /// <summary>
    /// The text input field inside a DisplayPromptAsync dialog.
    /// </summary>
    public Entry<ContentDialog<TParent>> PromptInput
        => new(this, Locator.ByControlType("entry"));

    #endregion

    #region Steps

    /// <summary>Asserts that a dialog is shown with this title, waiting for it.</summary>
    /// <param name="title">The title the dialog asks.</param>
    /// <param name="timeoutMs">How long to wait; the default wait when null.</param>
    /// <returns>The dialog.</returns>
    [BddThen("{control} should ask {value}", CommandId = "Dialog.AssertAsks")]
    public ContentDialog<TParent> AssertAsks(string title, int? timeoutMs = null)
        => AssertExists(true, timeoutMs: timeoutMs).AssertTitle(title, timeoutMs: timeoutMs);

    /// <summary>Presses the dialog's button with this text.</summary>
    /// <param name="buttonText">The button's visible text.</param>
    /// <returns>The dialog.</returns>
    [BddWhen("I choose {value} in {control}", CommandId = "Dialog.Choose", Navigates = true)]
    [BddWhen("{user} chooses {value} in {control}", CommandId = "Dialog.Choose", Navigates = true)]
    public ContentDialog<TParent> Choose(string buttonText)
    {
        DialogButton(buttonText).Click();
        return this;
    }

    /// <summary>Asserts that no dialog is shown, waiting for one to close.</summary>
    /// <param name="timeoutMs">How long to wait; the default wait when null.</param>
    /// <returns>The dialog.</returns>
    [BddThen("no {control} should be shown", CommandId = "Dialog.AssertNone")]
    public ContentDialog<TParent> AssertNotShown(int? timeoutMs = null)
        => AssertExists(false, timeoutMs: timeoutMs);

    #endregion

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// The dialog's title, as the platform publishes it.
    /// </summary>
    /// <param name="element">The dialog root.</param>
    /// <returns>The title.</returns>
    protected virtual string? GetTitleCore(IMauiElement? element)
    {
        if (element == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(element.Name))
        {
            return element.Name;
        }

        // Android publishes no name on the alert's root; the title is its alertTitle text, under
        // the app's package for MAUI's AppCompat alert and android: for a framework one.
        return element.TryFindElement(AndroidAlertTitle)?.Text;
    }

    /// <summary>An Android alert's title, whichever package its ids carry.</summary>
    private static readonly Locator AndroidAlertTitle = Locator.ByXPath(".//*[contains(@resource-id, ':id/alertTitle')]");

    /// <summary>
    /// The text on every button the dialog is offering.
    /// </summary>
    /// <param name="element">The dialog root.</param>
    /// <returns>
    /// The button texts in the order MAUI was given them - accept, then cancel - which is tree
    /// order on Windows. Android lays an alert's buttons out the other way round, so there they
    /// are read by role: positive (<c>button1</c>), negative (<c>button2</c>), neutral
    /// (<c>button3</c>). iOS shows the cancel action of a two-button alert first, so there the
    /// pair is read right to left.
    /// </returns>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetButtonTextsCore(IMauiElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var byRole = (Context.Driver?.AlertButtons ?? [])
            .Select(role => element.TryFindElement(role)?.Text)
            .OfType<string>()
            .ToList();

        if (byRole.Count > 0)
        {
            return byRole;
        }

        var texts = element.FindElements(Locator.ByControlType("button"))
            .Select(button => button.Name ?? string.Empty)
            .ToList();

        // UIKit always puts the cancel action of a two-button alert on the left.
        if (Context.Driver?.Platform == MauiPlatform.iOS && texts.Count == 2)
        {
            texts.Reverse();
        }

        return texts;
    }

    /// <summary>
    /// The question the dialog is asking, as the app phrased it.
    /// </summary>
    /// <param name="element">The dialog root.</param>
    /// <returns>The message.</returns>
    /// <exception cref="NotSupportedException">
    /// The app under test does not report its alerts.
    /// </exception>
    protected virtual string? GetMessageCore(IMauiElement? element)
        => Context.AppElement.ReadAlert()?.Message
           ?? throw new NotSupportedException(
               "The app under test does not report what its alerts ask. Raise them through "
               + "BruinetAlerts in the app - one line per call site - or assert on the title and "
               + "the buttons, which the platform publishes without any cooperation.");

    #endregion
}
