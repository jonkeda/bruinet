using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Bruinet.FlaUI;

/// <summary>
/// How UI Automation reports an element that is no longer in the tree, and a route the element does not have.
/// </summary>
public static class FlaUIErrors
{
    /// <summary>UIA_E_ELEMENTNOTAVAILABLE.</summary>
    public const int ElementNotAvailable = unchecked((int)0x80040201);

    /// <summary>UIA_E_NOTSUPPORTED.</summary>
    public const int NotSupported = unchecked((int)0x80040204);

    /// <summary>Whether <paramref name="error"/> says the element is no longer in the tree.</summary>
    public static bool IsElementGone(Exception error) => error switch
    {
        global::FlaUI.Core.Exceptions.ElementNotAvailableException => true,
        COMException com => com.HResult == ElementNotAvailable,
        Win32Exception win32 => win32.NativeErrorCode == ElementNotAvailable || win32.HResult == ElementNotAvailable,
        _ => false
    };

    /// <summary>
    /// Whether <paramref name="error"/> says the element has no such pattern, property or method: the route is
    /// not there, and the caller may try another or answer "none". Anything else - a timeout, the app's own
    /// handler throwing, the app going away - is a fault and is not this (AD-009).
    /// </summary>
    public static bool IsNotSupported(Exception error) => error switch
    {
        global::FlaUI.Core.Exceptions.PatternNotSupportedException => true,
        global::FlaUI.Core.Exceptions.PropertyNotSupportedException => true,
        global::FlaUI.Core.Exceptions.MethodNotSupportedException => true,
        global::FlaUI.Core.Exceptions.NotSupportedByFrameworkException => true,
        COMException com => com.HResult == NotSupported,
        _ => false
    };
}
