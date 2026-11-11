namespace Bruinet.Stride.Communication;

/// <summary>
/// Abstraction for communication between test process and game process.
/// </summary>
public interface IAutomationChannel : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<AutomationResponse> SendCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
