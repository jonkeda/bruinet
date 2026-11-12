using System.ComponentModel;
using System.Runtime.InteropServices;
using Bruinet.FlaUI.Windowing;
using VirtualKeyShort = FlaUI.Core.WindowsAPI.VirtualKeyShort;

namespace Bruinet.FlaUI;

/// <summary>
/// One key sent to a control's own window as the messages a key press makes (down, the character for a printable
/// key, up), so it reaches that control whichever window is in front (R2-T117).
/// </summary>
internal static class KeyMessages
{
    private const uint TimeoutMs = 5_000;

    /// <summary>Sends <paramref name="key"/>: one character, or <paramref name="virtualKey"/> when it names a key.</summary>
    /// <param name="window">The control's window.</param>
    /// <param name="key">The key as the caller wrote it.</param>
    /// <param name="virtualKey">The key it names, when <paramref name="key"/> is longer than one character.</param>
    /// <param name="element">The element, for the message.</param>
    /// <exception cref="ActionRefusedException">The window did not take a message (gone or hung).</exception>
    public static void Send(IntPtr window, string key, VirtualKeyShort virtualKey, object element)
    {
        if (key.Length == 1)
        {
            var character = key[0];
            var scanned = NativeMethods.VkKeyScan(character);
            var code = scanned == -1 ? 0u : (uint)(scanned & 0xFF);
            if (code != 0)
                Post(window, NativeMethods.WmKeyDown, code, Down(code), key, element);
            Post(window, NativeMethods.WmChar, character, Down(code), key, element);
            if (code != 0)
                Post(window, NativeMethods.WmKeyUp, code, Up(code), key, element);
            return;
        }

        Post(window, NativeMethods.WmKeyDown, (uint)virtualKey, Down((uint)virtualKey), key, element);
        Post(window, NativeMethods.WmKeyUp, (uint)virtualKey, Up((uint)virtualKey), key, element);
    }

    // lParam: repeat count 1, the scan code in bits 16-23; a key up also sets the previous-state and transition bits.
    private static nint Down(uint code) => 1 | ((nint)NativeMethods.MapVirtualKey(code, NativeMethods.MapVkToScanCode) << 16);

    private static nint Up(uint code) => (nint)((long)Down(code) | 0xC0000000L);

    private static void Post(IntPtr window, uint message, uint wParam, nint lParam, string key, object element)
    {
        if (NativeMethods.SendMessageTimeout(window, message, (nint)wParam, lParam, NativeMethods.SmtoAbortIfHung, TimeoutMs, out _) == 0)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new ActionRefusedException($"The window of {element} did not take the key '{key}' ({error.Message}).", error);
        }
    }
}
