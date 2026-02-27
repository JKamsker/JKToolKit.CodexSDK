using System.Text.Json;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Best-effort utilities for extracting a top-level JSON object/array from model output.
/// </summary>
public static class CodexStructuredJsonExtractor
{
    /// <summary>
    /// Extracts a JSON object/array payload from <paramref name="rawText"/>.
    /// </summary>
    public static string ExtractJson(string rawText, bool tolerant)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        var text = rawText.Trim();
        if (text.Length == 0)
        {
            throw new InvalidOperationException("Codex returned an empty response; expected JSON.");
        }

        if (!tolerant)
        {
            JsonDocument.Parse(text);
            return text;
        }

        if (TryExtractFromAnyCodeFence(text, out var fenced))
        {
            text = fenced.Trim();
        }

        if (TryExtractLastJsonValue(text, out var extracted))
        {
            JsonDocument.Parse(extracted);
            return extracted;
        }

        // Fallback: if the entire string is valid JSON, accept it.
        JsonDocument.Parse(text);
        return text;
    }

    private static bool TryExtractFromAnyCodeFence(string text, out string fenced)
    {
        fenced = string.Empty;

        string? bestJsonFence = null;
        string? bestAnyFence = null;

        var idx = 0;
        while (true)
        {
            var fenceStart = text.IndexOf("```", idx, StringComparison.Ordinal);
            if (fenceStart < 0)
            {
                break;
            }

            var headerEnd = text.IndexOf('\n', fenceStart + 3);
            if (headerEnd < 0)
            {
                break;
            }

            var language = text.Substring(fenceStart + 3, headerEnd - (fenceStart + 3)).Trim();
            var isJsonLanguage = language.StartsWith("json", StringComparison.OrdinalIgnoreCase);

            var fenceEnd = text.IndexOf("```", headerEnd + 1, StringComparison.Ordinal);
            if (fenceEnd < 0)
            {
                break;
            }

            var body = text.Substring(headerEnd + 1, fenceEnd - (headerEnd + 1)).Trim();
            if (body.Length > 0 && body[0] is '{' or '[' && TryParseJson(body))
            {
                bestAnyFence = body;
                if (isJsonLanguage)
                {
                    bestJsonFence = body;
                }
            }

            idx = fenceEnd + 3;
        }

        fenced = bestJsonFence ?? bestAnyFence ?? string.Empty;
        return fenced.Length > 0;
    }

    private static bool TryExtractLastJsonValue(string text, out string json)
    {
        json = string.Empty;

        string? last = null;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not '{' and not '[')
            {
                continue;
            }

            if (!TryExtractJsonValueAt(text, i, out var endIndex, out var candidate))
            {
                continue;
            }

            if (TryParseJson(candidate))
            {
                last = candidate;

                // Avoid selecting nested values inside a valid outer JSON object/array (e.g., arrays inside objects).
                i = endIndex;
            }
        }

        if (last is null)
        {
            return false;
        }

        json = last;
        return true;
    }

    private static bool TryExtractJsonValueAt(string text, int start, out int endIndex, out string json)
    {
        endIndex = -1;
        json = string.Empty;

        var open = text[start];
        var close = open == '{' ? '}' : ']';

        var inString = false;
        var escaped = false;
        var depth = 0;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == open)
            {
                depth++;
                continue;
            }

            if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    endIndex = i;
                    json = text.Substring(start, (i - start) + 1).Trim();
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseJson(string candidate)
    {
        try
        {
            JsonDocument.Parse(candidate);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
