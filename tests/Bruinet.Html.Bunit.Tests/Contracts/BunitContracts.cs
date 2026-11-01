using Bruinet.ControlContracts.Html;

namespace Bruinet.Html.Bunit.Tests.Contracts;

// The control contracts on the bUnit head (.my/coverage/plan.md, C2): each page rendered by bUnit.

/// <summary>Renders a contract page with bUnit.</summary>
public sealed class BunitHead : IContractHead
{
    private BunitTestContext? _context;

    public Task<IHtmlTestContext> OpenAsync(Type component, string path)
    {
        _context = new BunitTestContext(new BunitTestContextOptions { RootComponent = component, BaseUrl = "http://localhost" + path });
        return Task.FromResult<IHtmlTestContext>(_context);
    }

    public ValueTask DisposeAsync() => _context?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public sealed class BunitElementContract : HtmlElementBinding<BunitHead>;

public sealed class BunitClickableContract : HtmlClickableBinding<BunitHead>
{
    private const string NoPointer = "bUnit has no pointer: BrowserOnlyException; Chromium runs it (C3).";

    [Fact(Skip = NoPointer)]
    public override void Hover_HoversIt() => base.Hover_HoversIt();

    [Fact(Skip = NoPointer)]
    public override void LongPress_PressesIt() => base.LongPress_PressesIt();
}

public sealed class BunitFocusableContract : HtmlFocusableBinding<BunitHead>
{
    private const string NoScript = "bUnit has no script to read the focus with: BrowserOnlyException; Chromium runs it (C3).";

    [Fact(Skip = NoScript)]
    public override void IsFocused_FalseAtTheStart() => base.IsFocused_FalseAtTheStart();

    [Fact(Skip = NoScript)]
    public override void Focus_GivesItTheFocus() => base.Focus_GivesItTheFocus();

    [Fact(Skip = NoScript)]
    public override void Blur_TakesTheFocusAway() => base.Blur_TakesTheFocusAway();

    [Fact(Skip = NoScript)]
    public override void WaitFocused_FalseWhenItDoesNotHold() => base.WaitFocused_FalseWhenItDoesNotHold();

    [Fact(Skip = NoScript)]
    public override void AssertFocused_PassesWhenItHolds_FailsWhenItDoesNot() => base.AssertFocused_PassesWhenItHolds_FailsWhenItDoesNot();
}

public sealed class BunitTextContract : HtmlTextBinding<BunitHead>;

public sealed class BunitTextInputContract : HtmlTextInputBinding<BunitHead>;

public sealed class BunitTextAreaContract : HtmlTextAreaBinding<BunitHead>;

public sealed class BunitCheckBoxContract : HtmlCheckBoxBinding<BunitHead>;

public sealed class BunitRangeInputContract : HtmlRangeInputBinding<BunitHead>;

public sealed class BunitSelectContract : HtmlSelectBinding<BunitHead>;

public sealed class BunitRadioGroupContract : HtmlRadioGroupBinding<BunitHead>;
