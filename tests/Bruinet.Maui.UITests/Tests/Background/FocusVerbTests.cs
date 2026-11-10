using Bruinet.Maui.Testing;
using System.Runtime.InteropServices;
using Bruinet.Core;
using Bruinet.Core.Locators;
using Bruinet.Core.Exceptions;
using Bruinet.Maui.FlaUI;
using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Background;

/// <summary>
/// Step 13: focus without taking the machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Focus was the linchpin of the whole programme.</b> Every physical action in the driver
/// used to call <c>SetForeground</c> first, because without it keystrokes meant for the app land
/// in whatever the person at the keyboard is doing. Foreground is a desktop-global resource and
/// there is exactly one of it; <c>VisualElement.Focus</c> is not, and costs nobody anything.
/// </para>
/// <para>
/// <b>How these tests prove it.</b> Asserting that focus landed says nothing about how - the old
/// path would pass the same assertion, having stolen the foreground on the way. That path is now
/// gone from the driver: <c>Focus</c> is the app's verb or an exception, so a test that passes
/// did not touch the foreground. These tests used to show it by running inside a refused
/// physical-input policy; <see cref="ActionsWithNoSemanticRoute_ThrowInsteadOfUsingRealInput"/>
/// now pins that nothing falls back.
/// </para>
/// <para>
/// The foreground assertions are then belt and braces, and cheap enough to keep - they name the
/// symptom a person would actually notice.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GestureBridge")]
[Trait("Stage", "Background")]
public class FocusVerbTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FocusVerbTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Getting to the page is the arrangement; what is under test is what happens once there.
        _fixture.Open(SamplePage.Text);
    }

    private TextTestPage Page => new(_fixture.Context);

    /// <summary>
    /// The control group: the app publishes the focus verbs on the fields under test.
    /// </summary>
    /// <remarks>
    /// If this fails, everything below fails for a reason that has nothing to do with focus -
    /// the app was built without the automation sources, or the declaration in
    /// <c>TextView.xaml</c> is missing.
    /// </remarks>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Bridge_PublishesTheFocusVerbs()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;

        Assert.True(
            driver.HasGestureBridge(),
            "The app under test publishes no Bruinet bridge, so no verb can reach it.\n"
            + driver.DescribeGestureBridge());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Focus lands, with no physical input of any kind.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Focus_LandsWithoutPhysicalInput()
    {
        var page = Page;

        page.TestEntry.Focus();

        Assert.True(
            page.TestEntry.IsFocused(),
            "The entry did not report focus after the Focus verb.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// The foreground window is the same after focusing as before.
    /// </summary>
    /// <remarks>
    /// The assertion a person would make by watching. It holds whether or not the app happens to
    /// be in front when the test runs: if it was, it still is, and if it was not, it did not
    /// become so.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Focus_LeavesTheForegroundWindowAlone()
    {
        var page = Page;
        var before = GetForegroundWindow();

        page.TestEditor.Focus();

        var after = GetForegroundWindow();

        _output.WriteLine($"foreground before 0x{before:X}, after 0x{after:X}");

        Assert.Equal(before, after);
        Assert.True(page.TestEditor.IsFocused(), "The editor did not report focus.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Focus moves between fields, and each reports it.
    /// </summary>
    /// <remarks>
    /// A single field reporting focus could be reporting a state it was already in. Moving
    /// focus and watching the previous holder give it up is what makes the reading mean
    /// something.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Focus_MovesBetweenFields()
    {
        var page = Page;

        page.TestEntry.Focus();
        Assert.True(page.TestEntry.IsFocused(), "The entry did not take focus.");

        page.TestSearchBar.Focus();

        Assert.True(page.TestSearchBar.IsFocused(), "The search bar did not take focus.");
        page.TestEntry.AssertFocused(false, "The entry still reports focus.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Blur removes focus rather than passing it to the next control.
    /// </summary>
    /// <remarks>
    /// The physical route for this is a Tab keystroke, which moves focus on instead of removing
    /// it - so the field after this one would receive it, along with whatever that field does
    /// on focus. The verb does what the method is named after.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Blur_ClearsFocusWithoutPhysicalInput()
    {
        var page = Page;

        page.TestEntry.Focus();
        Assert.True(page.TestEntry.IsFocused(), "The entry did not take focus.");

        page.TestEntry.Blur();

        page.TestEntry.AssertFocused(false, "The entry still reports focus after Blur.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// The guard: actions with no semantic route throw rather than reach for real input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These used to be refused by a policy, which a test proved by expecting
    /// <c>PhysicalInputRefusedException</c>. The driver no longer has a physical fallback
    /// to refuse, so the proof is that each of these fails outright, naming what to use instead.
    /// </para>
    /// <para>
    /// The entry declares text and focus verbs but no Tap, so a raw click has no route; typing key
    /// by key, right-clicking and hovering have none on any element.
    /// </para>
    /// </remarks>
    [DeviceFact("it checks the Windows driver's rule against physical input", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ActionsWithNoSemanticRoute_ThrowInsteadOfUsingRealInput()
    {
        var element = _fixture.Context.TryFindElement(Locator.ByAutomationId("TestEntry"));
        Assert.NotNull(element);

        var click = Assert.Throws<NotSupportedException>(() => element.Click());
        Assert.Contains("Invoke", click.Message);

        var typing = Assert.Throws<NotSupportedException>(
            () => element.SendKeys("x", TextInputMethod.Keys));
        Assert.Contains("SetValue", typing.Message);

        Assert.Throws<NotSupportedException>(() => element.RightClick());
        Assert.Throws<NotSupportedException>(() => element.Hover());

        return Task.CompletedTask;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
