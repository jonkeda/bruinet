using System.ComponentModel;
using System.Runtime.InteropServices;
using Bruinet.FlaUI;

namespace Bruinet.FlaUI.Tests;

/// <summary>
/// How the FlaUI stacks recognize an element that UI Automation has removed.
/// </summary>
/// <remarks>
/// No app and no fixture: these pin the classification that turns UIA's
/// <c>UIA_E_ELEMENTNOTAVAILABLE</c> into <c>StaleElementException</c> (<c>UiaElements.Live</c>), for
/// MAUI on Windows, WPF and WinForms alike.
/// </remarks>
public class FlaUIErrorsTests
{
    private const int ElementNotAvailable = unchecked((int)0x80040201);

    [Fact]
    public void ComException_WithElementNotAvailable_IsElementGone()
        => Assert.True(FlaUIErrors.IsElementGone(new COMException("gone", ElementNotAvailable)));

    [Fact]
    public void FlaUIsOwnException_IsElementGone()
        => Assert.True(FlaUIErrors.IsElementGone(new global::FlaUI.Core.Exceptions.ElementNotAvailableException()));

    [Fact]
    public void Win32Exception_WithElementNotAvailable_IsElementGone()
        => Assert.True(FlaUIErrors.IsElementGone(new Win32Exception(ElementNotAvailable)));

    [Theory]
    [InlineData(unchecked((int)0x80070005))] // E_ACCESSDENIED: not a removed element
    [InlineData(unchecked((int)0x80004005))] // E_FAIL
    public void ComException_WithAnotherHResult_IsNotElementGone(int hresult)
        => Assert.False(FlaUIErrors.IsElementGone(new COMException("other", hresult)));

    [Fact]
    public void AnUnrelatedException_IsNotElementGone()
        => Assert.False(FlaUIErrors.IsElementGone(new InvalidOperationException("not supported")));
}
