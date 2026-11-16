namespace Bruinet.Scraper.Explore;

/// <summary>The page could not be read because it went elsewhere, or redrew itself, while it was read.</summary>
/// <param name="message">What happened, with the page's address.</param>
public sealed class PageMovedException(string message) : InvalidOperationException(message);

/// <summary>Reading a page that may move under the reader: read again, a few times, before giving it up.</summary>
internal static class MovingPage
{
    /// <summary>How often a page is read before it is given up.</summary>
    public const int Attempts = 3;

    /// <summary>
    /// Reads the page; when it moved while it was read, pauses and reads again, up to <see cref="Attempts"/> times.
    /// </summary>
    /// <param name="read">Reads the page.</param>
    /// <param name="pause">Waits for the page to settle.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <typeparam name="T">What a read gives.</typeparam>
    /// <returns>What was read, or <see langword="null"/> when the page moved every time.</returns>
    public static async Task<T?> ReadAsync<T>(Func<CancellationToken, Task<T>> read, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await read(cancellationToken).ConfigureAwait(false);
            }
            catch (PageMovedException) when (attempt < Attempts)
            {
                await pause(cancellationToken).ConfigureAwait(false);
            }
            catch (PageMovedException)
            {
                return null;
            }
        }
    }
}
