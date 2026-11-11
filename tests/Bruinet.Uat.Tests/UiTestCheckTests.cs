using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>A fixture that is an <see cref="IUiTestCheck"/> is told before each <see cref="UiFactAttribute"/> test.</summary>
public sealed class UiTestCheckTests(UiTestCheckTests.CountingCheck check) : IClassFixture<UiTestCheckTests.CountingCheck>
{
    [UiFact]
    public void BeginTest_RunsBeforeTheTest()
        => Assert.True(check.Begun > check.Checked);

    /// <summary>Counts the calls the invoker makes.</summary>
    public sealed class CountingCheck : IUiTestCheck
    {
        /// <summary>How often a test began.</summary>
        public int Begun { get; private set; }

        /// <summary>How often a passed test was checked.</summary>
        public int Checked { get; private set; }

        /// <inheritdoc />
        public void BeginTest() => Begun++;

        /// <inheritdoc />
        public void CheckPassedTest() => Checked++;
    }
}
