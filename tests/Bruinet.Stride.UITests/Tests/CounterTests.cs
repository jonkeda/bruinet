using Bruinet.Stride.UITests.PageObjects;

namespace Bruinet.Stride.UITests.Tests;

public class CounterTests : StrideUITestBase
{
    public CounterTests(StrideAppFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    [Fact]
    public void Counter_InitialValue_IsZero()
    {
        var mainPage = new MainPage(Context);
        mainPage.AssertLoaded(true);
        mainPage.ResetCounter();

        Assert.Equal(0, mainPage.GetCounterValue());
        mainPage.CounterDisplay.AssertText("Count: 0");
    }

    [Fact]
    public void IncrementButton_Click_IncreasesCounter()
    {
        var mainPage = new MainPage(Context);
        mainPage.AssertLoaded(true);
        // The game is shared by the suite: start from zero, whatever the tests before left.
        mainPage.ResetCounter();

        mainPage.IncrementCounter();

        Assert.Equal(1, mainPage.GetCounterValue());
    }

    [Fact]
    public void DecrementButton_Click_DecreasesCounter()
    {
        var mainPage = new MainPage(Context);
        mainPage.AssertLoaded(true);
        mainPage.ResetCounter();
        mainPage.IncrementCounter();
        mainPage.IncrementCounter();

        mainPage.DecrementCounter();

        Assert.Equal(1, mainPage.GetCounterValue());
    }

    [Fact]
    public void ResetButton_Click_ResetsToZero()
    {
        var mainPage = new MainPage(Context);
        mainPage.AssertLoaded(true);
        mainPage.IncrementCounter();
        mainPage.IncrementCounter();
        mainPage.IncrementCounter();

        mainPage.ResetCounter();

        Assert.Equal(0, mainPage.GetCounterValue());
    }

    [Fact]
    public void Counter_MultipleIncrements_Accumulates()
    {
        var mainPage = new MainPage(Context);
        mainPage.AssertLoaded(true);
        mainPage.ResetCounter();

        for (int i = 0; i < 5; i++)
            mainPage.IncrementCounter();

        Assert.Equal(5, mainPage.GetCounterValue());
    }
}
