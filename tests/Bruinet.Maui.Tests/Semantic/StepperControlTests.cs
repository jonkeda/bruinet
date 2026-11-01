using System.Diagnostics;
using Bruinet.Maui.Controls.Range;

namespace Bruinet.Maui.Tests.Semantic;

/// <summary>
/// <c>Stepper.SetValue</c> presses and watches: it reads the value, presses towards the target,
/// confirms the value moved, and repeats. The step is what one press changed.
/// </summary>
/// <remarks>
/// The Stepper here is shaped as Android shows it: one element
/// that publishes its value as a range, holding a − and a + button. Bounds are published only
/// where a test says so.
/// </remarks>
public class StepperControlTests : SemanticControlTestsBase
{
    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_PressesTowardsTheTarget_UntilItIsReached()
    {
        var stepper = new FakeStepper(this, value: 5, minimum: 0, maximum: 10);

        new Stepper<TestPage>(Page, "TestStepper").SetValue(8);

        Assert.Equal(8, stepper.Value);
        Assert.Equal(3, stepper.PlusPresses);
        Assert.Equal(0, stepper.MinusPresses);
    }

    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_TakesTheStepFromOnePress()
    {
        var stepper = new FakeStepper(this, value: 0, minimum: 0, maximum: 10, increment: 2.5);

        new Stepper<TestPage>(Page, "TestStepper").SetValue(7.5);

        Assert.Equal(7.5, stepper.Value);
        Assert.Equal(3, stepper.PlusPresses);
    }

    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_ClampsToPublishedBounds_WithoutPressingPastThem()
    {
        var stepper = new FakeStepper(this, value: 8, minimum: 0, maximum: 10);

        new Stepper<TestPage>(Page, "TestStepper").SetValue(25);

        Assert.Equal(10, stepper.Value);
        Assert.Equal(2, stepper.PlusPresses);
    }

    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_StopsAtAnUnpublishedBound()
    {
        // No bounds published: the app stops at 10, and the press after that changes nothing.
        var stepper = new FakeStepper(this, value: 8, minimum: 0, maximum: 10, publishBounds: false);

        new Stepper<TestPage>(Page, "TestStepper").SetValue(25);

        Assert.Equal(10, stepper.Value);
        Assert.Equal(3, stepper.PlusPresses);
    }

    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_OnAStepperThatIgnoresAPress_FailsAfterThatOnePress()
    {
        var stepper = new FakeStepper(this, value: 5, minimum: 0, maximum: 10, ignoresPresses: true);

        var error = Assert.ThrowsAny<Exception>(() => new Stepper<TestPage>(Page, "TestStepper").SetValue(8));

        Assert.Equal(1, stepper.PlusPresses);
        Assert.Contains("stayed at 5", Flatten(error));
    }

    [Fact]
    [Trait("Pin", "android-D3")]
    public void SetValue_IsBoundedByTheBudget()
    {
        // Every press lands, but only after 30 ms, and the target is 1000 presses away.
        var stepper = new FakeStepper(this, value: 0, minimum: 0, maximum: 1000, effectDelayMs: 30);
        var stopwatch = Stopwatch.StartNew();

        Assert.ThrowsAny<Exception>(() => new Stepper<TestPage>(Page, "TestStepper").SetValue(1000, 200));

        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"took {stopwatch.ElapsedMilliseconds} ms");
        Assert.InRange(stepper.PlusPresses, 1, 20);
    }

    private static string Flatten(Exception error)
        => error.InnerException is null ? error.Message : error.Message + " | " + Flatten(error.InnerException);

    /// <summary>
    /// A Stepper as Android shows it with AppSupport: a range value on the node, two buttons inside.
    /// </summary>
    private sealed class FakeStepper
    {
        private readonly double _minimum;
        private readonly double _maximum;
        private readonly double _increment;
        private readonly bool _ignoresPresses;
        private readonly int _effectDelayMs;
        private double _pending;
        private Stopwatch? _pendingSince;

        public FakeStepper(
            StepperControlTests test,
            double value,
            double minimum,
            double maximum,
            double increment = 1,
            bool publishBounds = true,
            bool ignoresPresses = false,
            int effectDelayMs = 0)
        {
            Value = value;
            _pending = value;
            _minimum = minimum;
            _maximum = maximum;
            _increment = increment;
            _ignoresPresses = ignoresPresses;
            _effectDelayMs = effectDelayMs;

            var minus = CreateInvokableElement("Minus", 0, 0, 40, 40, () => Press(-1));
            var plus = CreateInvokableElement("Plus", 60, 0, 40, 40, () => Press(+1));

            var root = CreateElement("TestStepper", 0, 0, 100, 40);
            root.Setup(e => e.RangeValue).Returns(() => Read());
            if (publishBounds)
            {
                root.Setup(e => e.RangeMinimum).Returns(minimum);
                root.Setup(e => e.RangeMaximum).Returns(maximum);
            }

            root.Setup(e => e.FindElements(It.Is<Locator>(l => l.Strategy == LocatorStrategy.ControlType)))
                .Returns([minus.Object, plus.Object]);
            root.Setup(e => e.FindElements(It.Is<Locator>(l => l.Strategy == LocatorStrategy.ClassName)))
                .Returns([]);

            test.Context
                .Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "TestStepper")))
                .Returns([root.Object]);
            test.Context
                .Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "TestStepper")))
                .Returns(root.Object);
            test.Context
                .Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "TestStepper")))
                .Returns(root.Object);

            // No bridge: the app element declares nothing, so the value comes from the node.
            test.Context.Setup(c => c.AppElement).Returns(new Mock<IMauiElement>().Object);
        }

        public double Value { get; private set; }

        public int PlusPresses { get; private set; }

        public int MinusPresses { get; private set; }

        private void Press(int direction)
        {
            if (direction > 0)
                PlusPresses++;
            else
                MinusPresses++;

            if (_ignoresPresses)
            {
                return;
            }

            _pending = Math.Clamp(Read() + (direction * _increment), _minimum, _maximum);
            _pendingSince = Stopwatch.StartNew();
            Read();
        }

        private double Read()
        {
            if (_pendingSince is { } since && since.ElapsedMilliseconds >= _effectDelayMs)
            {
                Value = _pending;
                _pendingSince = null;
            }

            return Value;
        }
    }
}
