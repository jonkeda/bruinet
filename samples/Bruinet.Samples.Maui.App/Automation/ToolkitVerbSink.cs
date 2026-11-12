using System.Collections.ObjectModel;
using System.Globalization;
using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;

namespace Bruinet.Samples.Maui.App.Automation;

/// <summary>
/// The app answering verbs for CommunityToolkit.Maui views that UI Automation cannot reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here, not in AppSupport.</b> AppSupport knows MAUI and nothing else; teaching it the toolkit
/// would make every app that uses the bridge take a toolkit dependency. An app that uses the
/// toolkit copies this sink instead, and it names exactly the controls that app has.
/// </para>
/// <para>
/// <b>Each verb passed the AD-008 checks (the trees are in <c>docs/controls/community-toolkit.md</c>).</b> Expander and
/// RatingView publish no pattern at all, and DrawingView is not even addressable by id, so there
/// is no UI Automation route to fall back on. Every state read here is something the user sees.
/// MediaElement is not here at all: its transport controls are in the tree and answer Invoke.
/// </para>
/// <para>
/// <b>It acts the way the gesture would.</b> A tap on a star sets the rating the toolkit's own tap
/// sets; a stroke raises <c>DrawingLineCompleted</c> through the view's own entry point, so the
/// page's handlers run as they would for a finger.
/// </para>
/// </remarks>
public sealed class ToolkitVerbSink : IBruinetGestureSink
{
    /// <summary>The one instance markup attaches.</summary>
    public static ToolkitVerbSink Instance { get; } = new();

    private ToolkitVerbSink()
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BruinetVerb> Verbs { get; } =
        [BruinetVerb.Tap, BruinetVerb.SelectIndex, BruinetVerb.Pan, BruinetVerb.GetState];

    /// <inheritdoc />
    public IReadOnlyCollection<string> StateNames { get; } =
        ["IsExpanded", "Rating", "MaximumRating", "IsReadOnly", "LineCount"];

    /// <inheritdoc />
    public int Invoke(VisualElement element, BruinetVerb verb, int arg1, int arg2)
        => (verb, element) switch
        {
            (BruinetVerb.Tap, Expander expander) => ToggleExpander(expander),
            (BruinetVerb.SelectIndex, RatingView rating) => TapStar(rating, arg1),
            (BruinetVerb.Pan, DrawingView drawing) => Stroke(drawing, arg1, arg2),
            _ => HResults.UIA_E_NOTSUPPORTED,
        };

    /// <inheritdoc />
    /// <remarks>
    /// A name with no case throws, which the client reports as "declares GetState but has no case
    /// for it" - the same answer the bridge gives for its own state reads.
    /// </remarks>
    public string Exchange(VisualElement element, BruinetVerb verb, string argument)
    {
        if (verb != BruinetVerb.GetState)
        {
            throw new NotSupportedException($"{nameof(ToolkitVerbSink)} exchanges only GetState, not {verb}.");
        }

        return (argument, element) switch
        {
            ("IsVisible", _) => element.IsVisible.ToString(),
            ("IsEnabled", _) => element.IsEnabled.ToString(),
            ("IsExpanded", Expander expander) => expander.IsExpanded.ToString(),
            ("Rating", RatingView rating) => rating.Rating.ToString(CultureInfo.InvariantCulture),
            ("MaximumRating", RatingView rating) => rating.MaximumRating.ToString(CultureInfo.InvariantCulture),
            ("IsReadOnly", RatingView rating) => rating.IsReadOnly.ToString(),
            ("LineCount", DrawingView drawing) => drawing.Lines.Count.ToString(CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException(
                $"{element.GetType().Name} '{element.AutomationId}' has no state named '{argument}'."),
        };
    }

    /// <summary>What tapping the header does: flips the expanded state.</summary>
    private static int ToggleExpander(Expander expander)
    {
        expander.IsExpanded = !expander.IsExpanded;
        return HResults.S_OK;
    }

    /// <summary>What tapping star <paramref name="index"/> does: sets the rating to index + 1.</summary>
    private static int TapStar(RatingView rating, int index)
    {
        if (index < 0)
        {
            return HResults.E_INVALIDARG;
        }

        if (index >= rating.MaximumRating)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (rating.IsReadOnly)
        {
            return HResults.BRUINET_E_DECLINED;
        }

        rating.Rating = index + 1;
        return HResults.S_OK;
    }

    /// <summary>
    /// One straight stroke from the view's centre by (<paramref name="dx"/>, <paramref name="dy"/>).
    /// </summary>
    private static int Stroke(DrawingView drawing, int dx, int dy)
    {
        var start = new PointF((float)(drawing.Width / 2), (float)(drawing.Height / 2));
        var end = new PointF(start.X + dx, start.Y + dy);
        var line = new DrawingLine
        {
            LineColor = drawing.LineColor,
            LineWidth = drawing.LineWidth,
            Points = new ObservableCollection<PointF> { start, end },
        };

        if (!drawing.IsMultiLineModeEnabled)
        {
            drawing.Lines.Clear();
        }

        drawing.Lines.Add(line);
        ((IDrawingView)drawing).OnDrawingLineCompleted(line);
        return HResults.S_OK;
    }
}
