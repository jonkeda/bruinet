using System.Text.Json;
using Bruinet.Stride.Automation;

namespace Bruinet.Stride.Tests;

/// <summary>
/// The driver over the pipe: the game answers a missing element as a success with <c>Exists = false</c>,
/// so a failed answer is a fault in the game and is raised, never read as absent or idle (AD-009).
/// </summary>
public sealed class StrideAutomationDriverTests
{
    private readonly Mock<IAutomationChannel> _channel = new();

    public StrideAutomationDriverTests() => _channel.Setup(c => c.IsConnected).Returns(true);

    private StrideAutomationDriver Driver => new(_channel.Object);

    private void Answer(AutomationResponse response)
        => _channel.Setup(c => c.SendCommandAsync(It.IsAny<AutomationCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

    [Fact]
    public void A_Missing_Element_IsAbsent()
    {
        Answer(AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { automationId = "Save", exists = false })));

        Assert.False(Driver.GetState("Save").Exists);
    }

    [Fact]
    public void A_Failed_State_Query_IsRaised_NotRead_AsAbsent()
    {
        Answer(AutomationResponse.Fail("Error: handler threw"));

        var error = Assert.Throws<InvalidOperationException>(() => Driver.GetState("Save"));
        Assert.Contains("GetState", error.Message, StringComparison.Ordinal);
        Assert.Contains("Save", error.Message, StringComparison.Ordinal);
        Assert.Contains("handler threw", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Failed_Find_IsRaised()
    {
        Answer(AutomationResponse.Fail("Error: handler threw"));

        Assert.Throws<InvalidOperationException>(() => Driver.Find(string.Empty, "Save"));
        Assert.Throws<InvalidOperationException>(() => Driver.FindAll(string.Empty, "Save"));
        Assert.Throws<InvalidOperationException>(() => Driver.GetChildren(string.Empty));
    }

    [Fact]
    public void A_Failed_Busy_Query_IsRaised_NotRead_AsIdle()
    {
        Answer(AutomationResponse.Fail("Game thread error: boom"));

        var error = Assert.Throws<InvalidOperationException>(() => Driver.IsGameBusy());
        Assert.Contains("IsBusy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Failed_Ready_Query_IsRaised()
    {
        Answer(AutomationResponse.Fail("Game thread error: boom"));

        Assert.Throws<InvalidOperationException>(() => Driver.IsGameReady());
    }

    [Fact]
    public void A_Game_Answering_Busy_IsBusy()
    {
        Answer(AutomationResponse.Ok(JsonSerializer.SerializeToElement(true)));

        Assert.True(Driver.IsGameBusy());
    }
}
