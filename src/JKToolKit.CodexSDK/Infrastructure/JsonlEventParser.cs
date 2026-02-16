using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Default implementation of JSONL event parser.
/// </summary>
/// <remarks>
/// Parses newline-delimited JSON (JSONL) events from Codex session logs,
/// mapping known event types to strongly-typed classes and preserving
/// unknown event types for forward compatibility.
/// </remarks>
public sealed class JsonlEventParser : IJsonlEventParser
{
    private readonly ILogger<JsonlEventParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonlEventParser"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public JsonlEventParser(ILogger<JsonlEventParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to parse a single JSONL line into a <see cref="CodexEvent"/> without throwing.
    /// </summary>
    public bool TryParseLine(string line, out CodexEvent? evt, out string? error)
    {
        evt = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Line is empty/whitespace.";
            return false;
        }

        try
        {
            evt = JsonlEventParserCore.ParseLine(line, _logger);
            if (evt == null)
            {
                error = "Line could not be parsed (missing required fields or unsupported shape).";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CodexEvent> ParseAsync(
        IAsyncEnumerable<string> lines,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines));

        await using var enumerator = lines.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                hasNext = false;
            }

            if (!hasNext)
            {
                yield break;
            }

            var line = enumerator.Current;

            if (string.IsNullOrWhiteSpace(line))
            {
                _logger.LogTrace("Skipping empty line");
                continue;
            }

            if (!TryParseLine(line, out var evt, out var error))
            {
                _logger.LogWarning("Error parsing line, skipping: {Error}. Line: {Line}", error, line);
                continue;
            }

            if (evt != null)
            {
                yield return evt;
            }
        }
    }
}
