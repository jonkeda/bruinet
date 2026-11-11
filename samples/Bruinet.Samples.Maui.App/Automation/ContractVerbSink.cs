using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Samples.Maui.App.ViewModels;
using Bruinet.Uia;

namespace Bruinet.Samples.Maui.App.Automation;

/// <summary>
/// The app answering the control contracts' double tap on their button (the Buttons view's <c>ContractButton</c>).
/// </summary>
/// <remarks>
/// A TapGestureRecognizer for two taps on a Button swallows the Button's own clicks on Android, so the contract's
/// button carries no recognizer: Windows and MauiHeadless double-tap it through this sink, which records what the
/// recognizer would have (R2-T49).
/// </remarks>
public sealed class ContractVerbSink : IBruinetGestureSink
{
    /// <summary>The one instance markup attaches.</summary>
    public static ContractVerbSink Instance { get; } = new();

    private ContractVerbSink()
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BruinetVerb> Verbs { get; } = [BruinetVerb.DoubleTap];

    /// <inheritdoc />
    public int Invoke(VisualElement element, BruinetVerb verb, int arg1, int arg2)
    {
        if (verb != BruinetVerb.DoubleTap || element.BindingContext is not ButtonsViewModel model)
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        model.ContractAction = "double-click";
        return HResults.S_OK;
    }

    /// <inheritdoc />
    public string Exchange(VisualElement element, BruinetVerb verb, string argument)
        => throw new NotSupportedException($"{nameof(ContractVerbSink)} exchanges nothing, not {verb}.");
}
