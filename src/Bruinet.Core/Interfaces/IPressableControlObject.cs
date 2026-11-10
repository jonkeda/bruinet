namespace Bruinet.Core.Interfaces;

public interface IPressableControlObject<TScope> : IControlObject<TScope>
{
    /// <summary>
    /// Activates the button through keyboard input after focusing it.
    /// Useful for MAUI/WinUI button surfaces where UIA Invoke reports success
    /// without dispatching the app command.
    /// </summary>
    TScope Press(int? timeoutMs = null);
}
