using System.Text.Json;
using Bruinet.Stride.Communication;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace Bruinet.Stride.Automation.Tests;

/// <summary>
/// The protocol the call model needs of the game (upgrade plan U5): every read is one attempt, an
/// element has an identity, a command can address it by that identity, and a scope can be searched.
/// Over a Stride UI tree built in the test, without the engine.
/// </summary>
public sealed class StrideUIHandlerTests
{
    private readonly StackPanel _root;
    private readonly StackPanel _list;
    private readonly Button _save;
    private readonly StrideUIHandler _handler;

    public StrideUIHandlerTests()
    {
        _save = new Button { Name = "Save", Content = new TextBlock { Text = "Save" } };
        _list = new StackPanel { Name = "Rows" };
        _list.Children.Add(new TextBlock { Text = "Apples" });
        _list.Children.Add(new TextBlock { Text = "Pears" });
        _root = new StackPanel { Name = "Root" };
        _root.Children.Add(_save);
        _root.Children.Add(_list);
        _handler = new StrideUIHandler(() => _root);
    }

    private ElementState State(AutomationCommand command)
        => Result<ElementState>(command);

    private T Result<T>(AutomationCommand command)
    {
        var response = _handler.HandleCommandAsync(command).GetAwaiter().GetResult();
        Assert.True(response.Success, response.Error);
        // Over the pipe the result is JSON; read it the way the client does.
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(response.Result))!;
    }

    [Fact]
    public void A_State_CarriesTheElementsIdentity()
    {
        var state = State(AutomationCommand.Query("GetState", "Save"));

        Assert.True(state.Exists);
        Assert.Equal(StrideUIHandler.KeyOf(_save), state.InstanceKey);
        Assert.Equal("Button", state.ControlType);
    }

    [Fact]
    public void A_Replaced_Element_HasANewIdentity()
    {
        var before = State(AutomationCommand.Query("GetState", "Save")).InstanceKey;
        _root.Children.Remove(_save);
        _root.Children.Add(new Button { Name = "Save" });

        var after = State(AutomationCommand.Query("GetState", "Save")).InstanceKey;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void A_Target_OfHashKey_AddressesTheElementByItsIdentity()
    {
        var key = StrideUIHandler.KeyOf(_list.Children[1]);

        var state = State(AutomationCommand.Query("GetState", StrideUIHandler.KeyPrefix + key));

        Assert.True(state.Exists);
        Assert.Equal("Pears", state.Text);
    }

    [Fact]
    public void GetChildren_ListsAScopesChildren_EachWithItsIdentity()
    {
        var rows = Result<List<ElementState>>(AutomationCommand.Query("GetChildren", "Rows"));

        Assert.Equal(["Apples", "Pears"], rows.Select(row => row.Text));
        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(row.InstanceKey)));
    }

    [Fact]
    public void Find_LooksOnlyInsideItsScope()
    {
        Assert.True(State(AutomationCommand.Query("Find", "", "Save")).Exists);
        Assert.False(State(AutomationCommand.Query("Find", "Rows", "Save")).Exists);
    }

    [Fact]
    public void A_Missing_Element_IsAnAnswer_NotAWait()
    {
        var state = State(AutomationCommand.Query("GetState", "Nothing"));

        Assert.False(state.Exists);
    }

    [Fact]
    public async Task An_Action_CanTargetAnElementByItsIdentity()
    {
        var clicks = 0;
        _save.Click += (_, _) => clicks++;

        var response = await _handler.HandleCommandAsync(
            AutomationCommand.Action("Click", StrideUIHandler.KeyPrefix + StrideUIHandler.KeyOf(_save)));

        Assert.True(response.Success, response.Error);
        Assert.Equal(1, clicks);
    }
}
