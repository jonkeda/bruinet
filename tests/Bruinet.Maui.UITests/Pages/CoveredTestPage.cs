namespace Bruinet.Maui.UITests.Pages;

/// <summary>A page with a busy signal, which a pushed page and a modal cover.</summary>
public class CoveredTestPage : PageObjectBase<CoveredTestPage>
{
    public CoveredTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "CoveredPage";

    /// <inheritdoc />
    protected override BusySignalPolicy BusySignalPolicy => BusySignalPolicy.Required;

    /// <summary>"loading", then "loaded".</summary>
    public Label<CoveredTestPage> Status => new(this, "CoveredStatus");

    public Button<CoveredTestPage> PushDetailButton => new(this, "PushDetailButton");

    public Button<CoveredTestPage> OpenModalButton => new(this, "OpenModalButton");
}

/// <summary>A page over <see cref="CoveredTestPage"/>: the pushed detail or the modal.</summary>
public class CoveringTestPage : PageObjectBase<CoveringTestPage>
{
    private readonly string _name;

    public CoveringTestPage(IMauiTestContext context, string name)
        : base(context)
    {
        _name = name;
    }

    /// <inheritdoc />
    public override string Name => _name;

    public Label<CoveringTestPage> Title => new(this, $"{_name}_Title");

    public Button<CoveringTestPage> CloseButton => new(this, $"{_name}_Close");
}
