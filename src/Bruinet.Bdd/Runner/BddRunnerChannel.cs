using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Bdd.Runner;

/// <summary>The protocol between a host and the runner process.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddRunnerProtocol
{
    /// <summary>The version this build speaks. Raised when a message changes its meaning.</summary>
    public const int Version = 2;

    /// <summary>The argument that names the pipe the runner connects to.</summary>
    public const string PipeArgument = "--pipe";

    /// <summary>Null when the runner speaks this build's protocol; otherwise what to do about it.</summary>
    /// <param name="runner">The version in the runner's <see cref="BddRunnerHello"/>.</param>
    public static string? Mismatch(int runner) => runner == Version
        ? null
        : runner < Version
            ? $"The suite was built with an older Bruinet.Uat (runner protocol {runner}, this host speaks {Version}). Rebuild the suite."
            : $"The suite was built with a newer Bruinet.Uat (runner protocol {runner}, this host speaks {Version}). Update this host.";
}

/// <summary>Messages over a stream, one JSON object per line.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddRunnerChannel : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _sending = new(1, 1);

    /// <summary>Creates the channel over a connected stream, which it owns.</summary>
    /// <param name="stream">The stream, both ways.</param>
    public BddRunnerChannel(Stream stream) : this(stream, stream)
    {
    }

    /// <summary>Creates the channel over two connected streams, which it owns.</summary>
    /// <param name="input">Read from.</param>
    /// <param name="output">Written to.</param>
    public BddRunnerChannel(Stream input, Stream output)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(input, encoding);
        _writer = new StreamWriter(output, encoding) { NewLine = "\n" };
    }

    /// <summary>A message as its line.</summary>
    /// <param name="message">The message.</param>
    public static string Write(BddRunnerMessage message) => JsonSerializer.Serialize(message, Json);

    /// <summary>A line as its message.</summary>
    /// <param name="line">The line.</param>
    /// <exception cref="InvalidDataException">The line is not a message this build knows.</exception>
    public static BddRunnerMessage Read(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<BddRunnerMessage>(line, Json)
                   ?? throw new InvalidDataException($"Not a runner message: {line}");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"Not a runner message: {line}", exception);
        }
    }

    /// <summary>Sends a message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    public async Task SendAsync(BddRunnerMessage message, CancellationToken cancellationToken = default)
    {
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(Write(message).AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <summary>The next message; null when the other side has closed.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<BddRunnerMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        string? line;
        try
        {
            line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A pipe whose other end died reads as closed.
            return null;
        }

        return line is null ? null : Read(line);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reader.Dispose();
        try
        {
            _writer.Dispose();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // The other side is gone, or never came; there is nothing left to flush to.
        }

        _sending.Dispose();
    }
}
