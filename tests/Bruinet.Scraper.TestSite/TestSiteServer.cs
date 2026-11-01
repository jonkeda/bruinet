using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace Bruinet.Scraper.TestSite;

/// <summary>
/// Serves the test site (copied beside the tests) on a free port, on <c>localhost</c> and on
/// <c>127.0.0.1</c>: two origins, for cross-origin frames. Every request is logged; <c>/api/…</c>
/// answers any method, so a test can prove which requests reached the site; <c>pages/account.html</c>
/// sends each harmful action to <c>/api/counter/&lt;category&gt;</c>, and <c>/ws</c> counts the messages
/// <c>pages/live.html</c> sends over its websocket. <c>/_counters</c> gives the counters as JSON, for the
/// loop (<c>tools/Scripts/scrape-loop.ps1</c>), which runs the site on its own.
/// </summary>
public sealed class TestSiteServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "TestSite");
    private readonly ConcurrentQueue<(string Method, string Path)> _requests = new();
    private int _socketMessages;

    /// <summary>Serves on <paramref name="port"/>, or a free one when 0.</summary>
    public TestSiteServer(int port = 0)
    {
        Port = port > 0 ? port : FreePort();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public int Port { get; }

    /// <summary>The requests that reached the site, in order: the method and the path with its query.</summary>
    public IReadOnlyList<(string Method, string Path)> Requests => _requests.ToArray();

    /// <summary>How many requests with this method reached a path starting with <paramref name="path"/>.</summary>
    public int Count(string method, string path) => Requests.Count(r => r.Method == method && r.Path.StartsWith(path, StringComparison.Ordinal));

    /// <summary>How many times the dangerous page's actions of a category (sign-out, delete, update, send, pay, approve, bulk) reached the site, by any method.</summary>
    public int Counter(string category) => Requests.Count(r => r.Path.StartsWith($"/api/counter/{category}", StringComparison.Ordinal));

    /// <summary>Every category's counter and the websocket messages, as JSON: <c>{"sign-out":0,…,"websocket":0}</c>.</summary>
    public string CountersJson()
    {
        string[] categories = ["sign-out", "delete", "update", "send", "pay", "approve", "bulk"];
        var counts = categories.ToDictionary(c => c, Counter);
        counts["websocket"] = SocketMessages;
        return System.Text.Json.JsonSerializer.Serialize(counts);
    }

    /// <summary>How many messages the live page sent over its websocket.</summary>
    public int SocketMessages => Volatile.Read(ref _socketMessages);

    /// <summary>A page's address on the first origin.</summary>
    public string Url(string path) => $"http://localhost:{Port}/{path}";

    /// <summary>A page's address on the second origin.</summary>
    public string OtherOriginUrl(string path) => $"http://127.0.0.1:{Port}/{path}";

    public void Dispose() => _listener.Close();

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            var context = await TryNextAsync();
            if (context is null)
                return;
            if (context.Request.Url!.AbsolutePath != "/_counters")
                _requests.Enqueue((context.Request.HttpMethod, context.Request.Url!.PathAndQuery));
            if (context.Request.IsWebSocketRequest)
            {
                _ = CountMessagesAsync(context);
                continue;
            }
            if (context.Request.Url!.AbsolutePath == "/_counters")
            {
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(CountersJson()));
                context.Response.Close();
                continue;
            }
            if (context.Request.Url!.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync("{\"ok\":true}"u8.ToArray());
                context.Response.Close();
                continue;
            }
            var file = Path.Combine(_root, context.Request.Url!.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
            {
                context.Response.ContentType = Path.GetExtension(file) switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".css" => "text/css",
                    ".js" => "text/javascript",
                    _ => "application/octet-stream",
                };
                await context.Response.OutputStream.WriteAsync(await File.ReadAllBytesAsync(file));
            }
            else
            {
                context.Response.StatusCode = 404;
            }
            context.Response.Close();
        }
    }

    private async Task CountMessagesAsync(HttpListenerContext context)
    {
        var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (received.MessageType == WebSocketMessageType.Close)
                    return;
                if (received.EndOfMessage)
                    Interlocked.Increment(ref _socketMessages);
            }
        }
        catch (Exception e) when (e is WebSocketException or HttpListenerException or ObjectDisposedException)
        {
            // the page went away or the site stopped
        }
    }

    private async Task<HttpListenerContext?> TryNextAsync()
    {
        try
        {
            return await _listener.GetContextAsync();
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
        {
            return null; // stopped
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
