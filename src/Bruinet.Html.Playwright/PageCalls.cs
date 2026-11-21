using Bruinet.Core.Api;
using Bruinet.Core.Calls;
using Microsoft.Playwright;

namespace Bruinet.Html.Playwright;

/// <summary>
/// The calls a page makes to its back end (fetch and XHR), kept as exchanges from the moment a test first asks for
/// them: a page object's save gives the write it caused, so a test has what was sent and the new record's id without
/// reading the screen. The bodies are masked as an API session's are; with <c>BRUINET_API_EXCHANGES</c> set they are
/// appended to that file too.
/// </summary>
public sealed class PageCalls
{
    private readonly List<ApiExchange> _calls = [];
    private readonly string? _log = ApiExchangeLog.Current;

    internal PageCalls(IPage page)
    {
        // Raised on Playwright's own loop; the body is read there, and the exchange kept when it has come.
        page.RequestFinished += async (_, request) =>
        {
            if (!IsCall(request))
            {
                return;
            }

            Keep(await ReadAsync(request).ConfigureAwait(false));
        };
        page.RequestFailed += (_, request) =>
        {
            if (!IsCall(request))
            {
                return;
            }

            Keep(new ApiExchange(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, request.Method, request.Url, null, null, null)
            {
                RequestBody = ApiMasks.Mask(request.PostData, ApiMasks.DefaultFields),
                Error = request.Failure,
                Source = "page",
            });
        };
    }

    /// <summary>Where the list stands now: take it before the action whose calls are wanted.</summary>
    public int Mark()
    {
        lock (_calls)
        {
            return _calls.Count;
        }
    }

    /// <summary>The calls answered since a mark, oldest first.</summary>
    /// <param name="mark">A <see cref="Mark"/>.</param>
    public IReadOnlyList<ApiExchange> Since(int mark)
    {
        lock (_calls)
        {
            return [.. _calls.Skip(mark)];
        }
    }

    /// <summary>
    /// The write an action caused: the first call since a mark that is not a read and that the back end took, waited
    /// for until it is answered. Null when none came in time.
    /// </summary>
    /// <param name="mark">The <see cref="Mark"/> taken before the action.</param>
    /// <param name="timeoutMs">How long to wait for it.</param>
    /// <param name="match">Which write, when the action causes several; the first when null.</param>
    public ApiExchange? WaitForWrite(int mark, int timeoutMs = 15_000, Func<ApiExchange, bool>? match = null)
        => Settle.Read(
            () => Since(mark).FirstOrDefault(call => IsWrite(call) && call.Succeeded && (match is null || match(call))),
            found => found is not null,
            timeoutMs);

    /// <summary>
    /// Runs an action and gives the write it caused: <see cref="Mark"/>, the action, <see cref="WaitForWrite"/>.
    /// </summary>
    /// <param name="action">The action, such as a page object's save.</param>
    /// <param name="timeoutMs">How long to wait for the write.</param>
    /// <param name="match">Which write, when the action causes several.</param>
    public ApiExchange? WriteOf(Action action, int timeoutMs = 15_000, Func<ApiExchange, bool>? match = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var mark = Mark();
        action();
        return WaitForWrite(mark, timeoutMs, match);
    }

    /// <summary>Whether a call is one that writes: not a GET, HEAD or OPTIONS.</summary>
    /// <param name="call">The call.</param>
    public static bool IsWrite(ApiExchange call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Method.ToUpperInvariant() is not ("GET" or "HEAD" or "OPTIONS");
    }

    private static bool IsCall(IRequest request) => request.ResourceType is "fetch" or "xhr";

    private static async Task<ApiExchange> ReadAsync(IRequest request)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var response = await request.ResponseAsync().ConfigureAwait(false);
            string? body = null;
            string? type = null;
            if (response is not null)
            {
                type = (await response.HeaderValueAsync("content-type").ConfigureAwait(false))?.Split(';')[0].Trim();
                // Only what reads as text: a download's bytes are not a record.
                if (type is null || type.Contains("json", StringComparison.OrdinalIgnoreCase) || type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || type.Contains("xml", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        body = await response.TextAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightException)
                    {
                        // The page went on (a navigation) before the body was read: the status is known, the body is not.
                    }
                }
            }

            return new ApiExchange(Guid.NewGuid().ToString("N"), started, request.Method, request.Url, response?.Status, type, ApiMasks.Mask(body, ApiMasks.DefaultFields))
            {
                RequestBody = ApiMasks.Mask(request.PostData, ApiMasks.DefaultFields),
                RequestContentType = request.PostData is null ? null : (await request.HeaderValueAsync("content-type").ConfigureAwait(false))?.Split(';')[0].Trim(),
                DurationMs = (long)request.Timing.ResponseEnd,
                Source = "page",
            };
        }
        catch (PlaywrightException error)
        {
            // The page closed before the answer was read: the call was made, its answer is not known.
            return new ApiExchange(Guid.NewGuid().ToString("N"), started, request.Method, request.Url, null, null, null)
            {
                RequestBody = ApiMasks.Mask(request.PostData, ApiMasks.DefaultFields),
                Error = error.Message,
                Source = "page",
            };
        }
    }

    private void Keep(ApiExchange exchange)
    {
        lock (_calls)
        {
            _calls.Add(exchange);
        }

        if (_log is not null)
        {
            ApiExchangeLog.Append(_log, exchange);
        }
    }
}
