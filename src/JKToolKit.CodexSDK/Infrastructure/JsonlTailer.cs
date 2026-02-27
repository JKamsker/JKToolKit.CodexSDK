using System.Runtime.CompilerServices;
using System.Text;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Default implementation of JSONL file tailer.
/// </summary>
/// <remarks>
/// This implementation provides file tailing functionality similar to 'tail -f',
/// reading new lines as they are written to a file with support for concurrent
/// read access while another process is writing.
/// </remarks>
public sealed class JsonlTailer : IJsonlTailer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<JsonlTailer> _logger;
    private readonly CodexClientOptions _options;
    private const int ReadBufferBytes = 4096;
    private const int ReadBufferChars = 4096;
    private const int MaxLineLength = 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonlTailer"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The client options for polling configuration.</param>
    public JsonlTailer(
        IFileSystem fileSystem,
        ILogger<JsonlTailer> logger,
        IOptions<CodexClientOptions> options)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> TailAsync(
        string filePath,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath), "File path cannot be null or whitespace.");

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (!_fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException(
                $"The JSONL file does not exist: {filePath}",
                filePath);
        }

        _logger.LogDebug("Starting to tail file: {FilePath}", filePath);

        await foreach (var line in TailCoreAsync(filePath, options, cancellationToken))
        {
            yield return line;
        }
    }

    private async IAsyncEnumerable<string> TailCoreAsync(
        string filePath,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Open the file with shared read/write access to allow concurrent writing.
        // Include FileShare.Delete so log rotation (rename/replace) doesn't fail on Windows.
        FileStream? fileStream = null;
        StreamReader? reader = null;

        var pollInterval = _options.TailPollInterval;
        var buffer = new StringBuilder();
        long lastKnownPathLength = 0;
        DateTime lastKnownCreationTimeUtc = default;

        try
        {
            async Task OpenAsync(bool applyStartingPosition)
            {
                reader?.Dispose();
                fileStream?.Dispose();

                fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: ReadBufferBytes,
                    useAsync: true);

                reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                lastKnownPathLength = fileStream.Length;
                try
                {
                    lastKnownCreationTimeUtc = _fileSystem.GetFileCreationTimeUtc(filePath);
                }
                catch (Exception ex)
                {
                    lastKnownCreationTimeUtc = default;
                    _logger.LogTrace(ex, "Failed to read file creation time for {FilePath} (best-effort).", filePath);
                }
                buffer.Clear();

                if (applyStartingPosition)
                {
                    await ApplyStartingPositionAsync(reader, fileStream, options, cancellationToken).ConfigureAwait(false);
                    lastKnownPathLength = fileStream.Position;
                }
            }

            await OpenAsync(applyStartingPosition: true).ConfigureAwait(false);

            var readChars = new char[ReadBufferChars];

            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await reader!.ReadAsync(readChars.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    buffer.Append(readChars, 0, read);
                    if (buffer.Length > MaxLineLength)
                    {
                        _logger.LogError(
                            "JSONL tail buffer exceeded MaxLineLength={MaxLineLength} for {FilePath}; aborting.",
                            MaxLineLength,
                            filePath);
                        throw new InvalidDataException($"JSONL line exceeded MaxLineLength={MaxLineLength} characters.");
                    }

                    while (TryDequeueLine(buffer, out var line))
                    {
                        yield return line;
                    }

                    lastKnownPathLength = Math.Max(lastKnownPathLength, fileStream!.Position);
                    continue;
                }

                if (!options.Follow)
                {
                    _logger.LogTrace("Follow disabled; stopping tail for {FilePath}", filePath);
                    if (buffer.Length > 0)
                    {
                        var raw = buffer.ToString();
                        buffer.Clear();

                        var line = raw.EndsWith('\r') ? raw[..^1] : raw;
                        yield return line;
                    }
                    yield break;
                }

                // Reached EOF - check for growth/truncation/rotation.
                long currentPathLength;
                DateTime currentCreationTimeUtc;
                try
                {
                    currentPathLength = _fileSystem.GetFileSize(filePath);
                    currentCreationTimeUtc = _fileSystem.GetFileCreationTimeUtc(filePath);
                }
                catch (Exception ex)
                {
                    // Best-effort: path might temporarily disappear during rotation.
                    currentPathLength = -1;
                    currentCreationTimeUtc = default;
                    _logger.LogTrace(ex, "Failed to read file size/creation time for {FilePath} (best-effort).", filePath);
                }

                var handleLength = fileStream!.Length;
                var handlePosition = fileStream.Position;

                var pathMissing = currentPathLength < 0;
                var creationChanged =
                    !pathMissing &&
                    lastKnownCreationTimeUtc != default &&
                    currentCreationTimeUtc != default &&
                    currentCreationTimeUtc != lastKnownCreationTimeUtc;
                var truncatedOrReplaced =
                    !pathMissing &&
                    (creationChanged ||
                     currentPathLength < lastKnownPathLength ||
                     currentPathLength < handlePosition ||
                     currentPathLength != handleLength);

                if (pathMissing)
                {
                    await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (truncatedOrReplaced)
                {
                    _logger.LogWarning(
                        "Detected truncation/rotation for {FilePath} (pathLen={PathLen}, handleLen={HandleLen}, pos={Pos}, last={Last}, creationChanged={CreationChanged}); reopening.",
                        filePath,
                        currentPathLength,
                        handleLength,
                        handlePosition,
                        lastKnownPathLength,
                        creationChanged);

                    // Reopen to re-detect UTF-8 BOM and to pick up a replaced file.
                    await OpenAsync(applyStartingPosition: false).ConfigureAwait(false);
                    continue;
                }

                if (currentPathLength > lastKnownPathLength)
                {
                    _logger.LogTrace(
                        "File grew from {OldSize} to {NewSize} bytes",
                        lastKnownPathLength,
                        currentPathLength);

                    reader!.DiscardBufferedData();
                    lastKnownPathLength = currentPathLength;
                    continue;
                }

                try
                {
                    await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }
        finally
        {
            reader?.Dispose();
            fileStream?.Dispose();
            _logger.LogDebug("Stopped tailing file: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Applies the starting position to the stream based on EventStreamOptions.
    /// </summary>
    private async Task ApplyStartingPositionAsync(
        StreamReader reader,
        FileStream fileStream,
        EventStreamOptions options,
        CancellationToken cancellationToken)
    {
        // Handle byte offset
        if (options.FromByteOffset.HasValue)
        {
            var offset = options.FromByteOffset.Value;
            _logger.LogDebug("Seeking to byte offset: {Offset}", offset);

            fileStream.Seek(offset, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            // If the caller gives a mid-line offset, resync to the next newline to avoid yielding fragments.
            if (offset > 0 && !IsLineBoundary(fileStream, offset))
            {
                await ResyncToNextNewlineAsync(fileStream, cancellationToken).ConfigureAwait(false);
                reader.DiscardBufferedData();
            }
            return;
        }

        // Handle timestamp filter
        if (options.AfterTimestamp.HasValue)
        {
            _logger.LogDebug("Filtering events after timestamp: {Timestamp}", options.AfterTimestamp.Value);
            // Note: Timestamp filtering happens after parsing at the event pipeline level.
            // The tailer doesn't seek by timestamp.
            return;
        }

        // FromBeginning is the default - no seek needed
        if (!options.FromBeginning)
        {
            // If not from beginning and no other option specified, seek to end
            _logger.LogDebug("Seeking to end of file");
            fileStream.Seek(0, SeekOrigin.End);
            reader.DiscardBufferedData();
        }
    }

    private static async Task ResyncToNextNewlineAsync(FileStream fileStream, CancellationToken cancellationToken)
    {
        // Scan bytes until '\n' so subsequent line reads start at a line boundary.
        var buffer = new byte[ReadBufferBytes];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = await fileStream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    fileStream.Seek(i - read + 1, SeekOrigin.Current);
                    return;
                }
            }
        }
    }

    private static bool IsLineBoundary(FileStream fileStream, long offset)
    {
        if (offset <= 0)
            return true;

        // Best-effort: treat offset as a boundary if the previous byte is '\n' (LF).
        // This covers both '\n' and '\r\n' line endings where offset points at the first byte after the newline.
        var originalPosition = fileStream.Position;
        try
        {
            fileStream.Seek(offset - 1, SeekOrigin.Begin);
            var previousByte = fileStream.ReadByte();
            return previousByte == '\n';
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                fileStream.Seek(originalPosition, SeekOrigin.Begin);
            }
            catch
            {
                // Best-effort
            }
        }
    }

    private static bool TryDequeueLine(StringBuilder buffer, out string line)
    {
        line = string.Empty;

        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != '\n')
            {
                continue;
            }

            var raw = buffer.ToString(0, i);
            buffer.Remove(0, i + 1);

            line = raw.EndsWith('\r') ? raw[..^1] : raw;
            return true;
        }

        return false;
    }

    // Intentionally no ReadLineAsync-based tailing: StreamReader.ReadLineAsync emits the final unterminated line at EOF,
    // which can yield partial JSON fragments while another process is still writing.
}
