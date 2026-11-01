using Bruinet.Blazor.Controls;
using Bruinet.ControlContracts.Html.Pages;
using Xunit;

namespace Bruinet.ControlContracts.Html;

// Bruinet.Blazor's own controls on a head (.my/coverage/plan.md, C4): their members beyond the Core
// interfaces, on Pages/BlazorControls.razor. A head's test project derives a sealed class, and skips
// there what its head cannot do (script, media), with why.
public abstract class BlazorControlsBinding<THead> : IAsyncLifetime where THead : IContractHead, new()
{
    private readonly ContractSession<THead> _session = new(typeof(BlazorControls));

    public Task InitializeAsync() => _session.OpenAsync();

    public Task DisposeAsync() => _session.DisposeAsync().AsTask();

    private ImageControl<ContractPage> Image => new(_session.Page, ContractSession<THead>.Selector("image"));

    private NavMenuControl<ContractPage> Menu => new(_session.Page, ContractSession<THead>.Selector("menu"));

    private IFrameControl<ContractPage> Frame => new(_session.Page, ContractSession<THead>.Selector("frame"));

    private AudioControl<ContractPage> Audio => new(_session.Page, ContractSession<THead>.Selector("audio"));

    [Fact]
    public virtual void Image_SourceAndAltText_AreItsAttributes()
    {
        Assert.StartsWith("data:image/png;base64,", Image.GetSource());
        Image.AssertAltText("A red square");
    }

    [Fact]
    public virtual void Image_Loaded_WithItsOwnSize()
    {
        Image.AssertLoaded();
        Assert.Equal((3, 2), (Image.GetNaturalWidth(), Image.GetNaturalHeight()));
    }

    [Fact]
    public virtual void NavMenu_GivesItsItems_TheActiveOne_AndAnItemsHref()
    {
        Menu.AssertItems(["Apple", "Banana", "Cherry"]);
        Assert.Equal(3, Menu.GetItemCount());
        Menu.AssertActiveItem("Apple");
        Assert.Equal("#banana", Menu.GetItemHref("Banana"));
        Assert.Null(Menu.GetItemHref("Durian"));
    }

    [Fact]
    public virtual void NavMenu_NavigatesByText_AndByIndex()
    {
        Menu.NavigateTo("Cherry");
        Assert.True(Menu.WaitActiveItem("Cherry"));

        Menu.NavigateToIndex(1);
        Menu.AssertActiveItem("Banana");
        Assert.Throws<InvalidOperationException>(() => Menu.NavigateTo("Durian"));
    }

    [Fact]
    public virtual void IFrame_TitleAndName_AreItsAttributes()
    {
        Frame.AssertTitle("Inside");
        Frame.AssertName("inner");
        Assert.Null(Frame.GetSource());
    }

    [Fact]
    public virtual void IFrame_ReadsFillsAndClicksInside()
    {
        Frame.AssertElementInside("#go");
        Assert.False(Frame.IsElementInside("#nothing"));
        Frame.AssertTextInside("#said", "waiting");

        Frame.FillInside("#field", "typed");
        Frame.ClickInside("#go");

        Assert.True(Frame.WaitTextInside("#said", "gone"));
    }

    [Fact]
    public virtual void Audio_Source_IsItsSrc()
        => Audio.AssertSourceContains("media/tone.wav");

    [Fact]
    public virtual void Audio_PlaysPausesAndSeeks()
    {
        Audio.Mute();
        Audio.Play();
        Assert.True(Audio.WaitPlaying());

        Audio.Pause();
        Audio.AssertPaused();
        Audio.Seek(1);

        Assert.InRange(Audio.GetCurrentTime()!.Value, 0.5, 1.5);
        Assert.InRange(Audio.GetDuration()!.Value, 1.5, 2.5);
    }

    [Fact]
    public virtual void Audio_VolumeAndMute()
    {
        Audio.AssertMuted();
        Audio.SetVolume(0.25);
        Audio.AssertVolume(0.25);

        Audio.Unmute();

        Audio.AssertMuted(false);
    }
}
