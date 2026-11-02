using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace Bruinet.Bdd.VisualStudio;

/// <summary>
/// Sends a prompt into Copilot Chat's open conversation through Visual Studio's Copilot brokered service
/// (<c>Microsoft.VisualStudio.Copilot</c> 0.3): the active chat session's <c>ICopilotSession.SendRequestAsync</c>.
/// (<c>ICopilotService.SendRequestAsync</c> needs the Copilot backend mode, which is off by default.) Its contracts ship
/// with Visual Studio only, not as a package, so they are reached by reflection; any step that is missing
/// in this Visual Studio makes it answer false, and the caller puts the prompt on the clipboard instead.
/// </summary>
internal static class CopilotChatSender
{
    private const string Contracts = "Microsoft.VisualStudio.Copilot";
    private const string Namespace = "Microsoft.VisualStudio.Copilot.";

    // How long a send may take to fail before it counts as sent: the request runs as long as the agent does.
    private static readonly TimeSpan Accepted = TimeSpan.FromSeconds(3);

    /// <summary>Sends <paramref name="prompt"/> to the active chat session; false when it could not.</summary>
    /// <param name="package">The package, for the service broker.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="model">A model's family (<c>gpt-4.1</c>, as <see cref="TryListModelsAsync"/> gives); null for the chat's own.</param>
    /// <param name="log">Says each step, and why it stopped.</param>
    /// <param name="done">Called once the agent's answer is complete.</param>
    /// <param name="cancellationToken">Ends the send.</param>
    public static async Task<bool> TrySendAsync(AsyncPackage package, string prompt, string? model, Action<string> log, Action done, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(package, prompt, model, log, done, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log($"Copilot Chat: failed: {(exception as TargetInvocationException)?.InnerException ?? exception}");
            return false;
        }
    }

    /// <summary>The models the active chat session offers, as (family, display name); empty when there is no session yet.</summary>
    public static async Task<IReadOnlyList<(string Family, string Name)>> TryListModelsAsync(AsyncPackage package, Action<string> log, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await ConnectAsync(package, log, cancellationToken);
            if (connection is null)
            {
                return [];
            }

            using var proxy = connection.Proxy as IDisposable;
            var session = await AwaitAsync(connection.Service.GetMethod("GetActiveChatSessionAsync")!.Invoke(connection.Proxy, [cancellationToken]));
            if (session is null)
            {
                return [];
            }

            var models = await AwaitAsync(connection.Session.GetMethod("GetDefaultModelsAsync")!.Invoke(session, [cancellationToken])) as System.Collections.IEnumerable;
            return models?.Cast<object>()
                .Select(m => (Family: m.GetType().GetProperty("Family")?.GetValue(m) as string ?? "", Name: m.GetType().GetProperty("DisplayName")?.GetValue(m) as string ?? ""))
                .Where(m => m.Family.Length > 0)
                .GroupBy(m => m.Family, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray() ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log($"Copilot Chat: no list of models: {(exception as TargetInvocationException)?.InnerException?.Message ?? exception.Message}");
            return [];
        }
    }

    // .NET Framework has no records: a plain class.
    private sealed class Connection(object proxy, Type service, Type session, Assembly contracts)
    {
        public object Proxy { get; } = proxy;

        public Type Service { get; } = service;

        public Type Session { get; } = session;

        public Assembly Contracts { get; } = contracts;
    }

    // The Copilot service's proxy, through Visual Studio's service broker; null when this Visual Studio has none.
    private static async Task<Connection?> ConnectAsync(AsyncPackage package, Action<string> log, CancellationToken cancellationToken)
    {
        var contracts = LoadContracts();
        var serviceType = contracts?.GetType(Namespace + "ICopilotService");
        var sessionType = contracts?.GetType(Namespace + "ICopilotSession");
        var descriptor = contracts?.GetType(Namespace + "CopilotDescriptors")?.GetProperty("CopilotService")?.GetValue(null) as ServiceRpcDescriptor;
        if (contracts is null || serviceType is null || sessionType is null || descriptor is null)
        {
            log($"Copilot Chat: contracts {contracts?.Location ?? "not found"}; service {serviceType is not null}, session {sessionType is not null}, descriptor {descriptor is not null}.");
            return null;
        }

        if (await package.GetServiceAsync(typeof(SVsBrokeredServiceContainer)) is not IBrokeredServiceContainer container)
        {
            log("Copilot Chat: no brokered service container.");
            return null;
        }

        var broker = container.GetFullAccessServiceBroker();
        var getProxy = typeof(IServiceBroker).GetMethod(nameof(IServiceBroker.GetProxyAsync))!.MakeGenericMethod(serviceType);
        var proxy = await AwaitAsync(getProxy.Invoke(broker, [descriptor, default(ServiceActivationOptions), cancellationToken]));
        if (proxy is null)
        {
            log($"Copilot Chat: the broker has no {descriptor.Moniker}.");
            return null;
        }

        return new Connection(proxy, serviceType, sessionType, contracts);
    }

    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    private static async Task<bool> SendAsync(AsyncPackage package, string prompt, string? model, Action<string> log, Action done, CancellationToken cancellationToken)
    {
        var connection = await ConnectAsync(package, log, cancellationToken);
        var requestType = connection?.Contracts.GetType(Namespace + "CopilotRequest");
        if (connection is null || requestType is null)
        {
            return false;
        }

        var proxy = connection.Proxy;
        var sending = false;
        try
        {
            // The chat window was just opened: its session can take a moment to become the active one.
            object? session = null;
            for (var attempt = 0; attempt < 10 && session is null; attempt++)
            {
                session = await AwaitAsync(connection.Service.GetMethod("GetActiveChatSessionAsync")!.Invoke(proxy, [cancellationToken]));
                if (session is null)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            if (session is null)
            {
                log("Copilot Chat: no active chat session.");
                return false;
            }

            var info = await AwaitAsync(connection.Session.GetMethod("GetSessionInfoAsync")!.Invoke(session, [cancellationToken]));
            log($"Copilot Chat: session {info?.GetType().GetProperty("Id")?.GetValue(info)}.");

            // In the mode the chat window is in (Ask or Agent); the model when one was chosen.
            var message = Activator.CreateInstance(requestType, prompt)!;
            if (model is { Length: > 0 } && connection.Contracts.GetType(Namespace + "CopilotModelRequest") is { } modelType)
            {
                requestType.GetProperty("Model")?.SetValue(message, Activator.CreateInstance(modelType, model));
                log($"Copilot Chat: model {model}.");
            }

            var send = connection.Session.GetMethods().Single(m => m.Name == "SendRequestAsync" && m.GetParameters().Length == 2);
            var request = AwaitAsync(send.Invoke(session, [message, cancellationToken]));
            if (await Task.WhenAny(request, Task.Delay(Accepted, cancellationToken)) == request)
            {
                // Answered at once: an error, or a short answer; either way the chat shows it.
                var response = await request;
                var status = response?.GetType().GetProperty("Status")?.GetValue(response)?.ToString();
                log($"Copilot Chat: answered at once: status {status ?? "none"}.");
                done();
                return status is "Success" or "Unknown";
            }

            // Still running: the chat has it. The proxy stays until the agent is done.
            log("Copilot Chat: sent; the request is running.");
            sending = true;
            _ = request.ContinueWith(
                finished =>
                {
                    log(finished.IsFaulted
                        ? $"Copilot Chat: the request failed: {finished.Exception?.InnerException}"
                        : $"Copilot Chat: the request is done: status {finished.Result?.GetType().GetProperty("Status")?.GetValue(finished.Result)}.");

                    (proxy as IDisposable)?.Dispose();
                    done();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return true;
        }
        finally
        {
            if (!sending)
            {
                (proxy as IDisposable)?.Dispose();
            }
        }
    }

    // The contracts assembly as Visual Studio loads it, else from its folder beside devenv.exe.
    private static Assembly? LoadContracts()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == Contracts && a.GetType(Namespace + "ICopilotService") is not null);
        if (loaded is not null)
        {
            return loaded;
        }

        var ide = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
        var path = Path.Combine(ide, "CommonExtensions", "Microsoft", "Microsoft.VisualStudio.Copilot.Contracts", Contracts + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    // Awaits a Task, Task<T> or ValueTask<T> returned by a method called by reflection, and gives its result.
    private static async Task<object?> AwaitAsync(object? awaitable)
    {
        if (awaitable is null)
        {
            return null;
        }

        var task = awaitable as Task ?? (Task)awaitable.GetType().GetMethod("AsTask")!.Invoke(awaitable, null)!;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }
}
