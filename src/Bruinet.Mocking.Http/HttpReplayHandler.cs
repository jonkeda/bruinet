using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Bruinet.Mocking.Http;

/// <summary>A connection a hermetic run refused: something called the network past the replay.</summary>
public sealed class HermeticViolationException(string message) : HttpRequestException(message);

/// <summary>
/// The app's calls through a <see cref="HttpReplaySession"/>: in replay answered from the recording, the inner handler
/// never called; otherwise sent on and recorded. An Azure SDK client takes it as
/// <c>new HttpClientTransport(new HttpClient(handler))</c>.
/// </summary>
public sealed class HttpReplayHandler : DelegatingHandler
{
    /// <summary>A handler with no inner one yet, for a chain that sets it (<c>IHttpClientFactory</c>).</summary>
    public HttpReplayHandler(HttpReplaySession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>A handler over <paramref name="inner"/>.</summary>
    public HttpReplayHandler(HttpReplaySession session, HttpMessageHandler inner)
        : base(inner)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public HttpReplaySession Session { get; }

    /// <summary>A handler over the network; over <see cref="HermeticGuard.Handler"/> in a hermetic replay.</summary>
    public static HttpReplayHandler Create(HttpReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Decompressed, so a gzipped answer is recorded as text: redacted and scanned like any other.
        return new HttpReplayHandler(session, session.Mode == HttpReplayMode.Replay && session.Options.Hermetic
            ? HermeticGuard.Handler()
            : new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Session.SendAsync(request, base.SendAsync, cancellationToken);
}

/// <summary>
/// A hermetic run's last line: a handler whose every connection fails by name, for the clients the replay does not wrap.
/// A call that slipped past the factory fails at once, naming the host, not by a timeout.
/// </summary>
public static class HermeticGuard
{
    /// <summary>A handler that opens no connection.</summary>
    public static SocketsHttpHandler Handler() => new()
    {
        ConnectCallback = (context, _) => throw new HermeticViolationException(
            $"Hermetic run: a call to {context.DnsEndPoint.Host}:{context.DnsEndPoint.Port} reached the network. Answer it from a recording or a stub, or name the dependency real."),
    };

    /// <summary>Whether <paramref name="exception"/> is a hermetic refusal, however the client wrapped it.</summary>
    public static bool IsViolation(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HermeticViolationException)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>The replay on every client an app makes through <c>IHttpClientFactory</c>.</summary>
public static class HttpReplayServiceCollectionExtensions
{
    /// <summary>
    /// Puts <paramref name="session"/> on every <c>IHttpClientFactory</c> client, after the app's own handlers (so it sees
    /// the request as sent); in a hermetic replay their primary handler opens no connection.
    /// </summary>
    public static IServiceCollection AddBruinetHttpReplay(this IServiceCollection services, HttpReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        services.AddSingleton(session);
        services.AddHttpClient();
        services.PostConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            builder.AdditionalHandlers.Add(new HttpReplayHandler(session));
            if (session.Mode == HttpReplayMode.Replay && session.Options.Hermetic)
            {
                builder.PrimaryHandler = HermeticGuard.Handler();
            }
        }));
        return services;
    }
}
