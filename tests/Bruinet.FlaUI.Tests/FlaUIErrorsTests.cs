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

    // What a route that is not there looks like, against a fault (review 05, findings 2 to 4; AD-009).

    [Fact]
    public void A_Pattern_ThePlatformDoesNotHave_IsNotSupported()
        => Assert.True(FlaUIErrors.IsNotSupported(new global::FlaUI.Core.Exceptions.PatternNotSupportedException()));

    [Fact]
    public void A_Property_ThePlatformDoesNotHave_IsNotSupported()
        => Assert.True(FlaUIErrors.IsNotSupported((Exception)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::FlaUI.Core.Exceptions.PropertyNotSupportedException))));

    [Fact]
    public void A_Method_ThePlatformDoesNotHave_IsNotSupported()
        => Assert.True(FlaUIErrors.IsNotSupported(new global::FlaUI.Core.Exceptions.MethodNotSupportedException()));

    [Fact]
    public void UiaNotSupported_IsNotSupported()
        => Assert.True(FlaUIErrors.IsNotSupported(new COMException("not supported", unchecked((int)0x80040204))));

    [Theory]
    [InlineData(unchecked((int)0x80040201))] // UIA_E_ELEMENTNOTAVAILABLE: gone, not unsupported
    [InlineData(unchecked((int)0x80131505))] // UIA_E_TIMEOUT
    [InlineData(unchecked((int)0x800706BA))] // RPC_S_SERVER_UNAVAILABLE: the app is going
    [InlineData(unchecked((int)0x80004005))] // E_FAIL: the app's handler threw
    public void Other_ComErrors_AreNotNotSupported(int hresult)
        => Assert.False(FlaUIErrors.IsNotSupported(new COMException("fault", hresult)));

    [Fact]
    public void An_InvalidOperation_IsNotNotSupported()
        => Assert.False(FlaUIErrors.IsNotSupported(new InvalidOperationException("refused")));
}
