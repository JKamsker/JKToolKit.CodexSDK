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

        if (TryExtractFromCodeFence(text, out var fenced))
        {
            text = fenced.Trim();
        }

        if (TryExtractFirstJsonValue(text, out var extracted))
        {
            JsonDocument.Parse(extracted);
            return extracted;
        }

        // Fallback: if the entire string is valid JSON, accept it.
        JsonDocument.Parse(text);
        return text;
    }

    private static bool TryExtractFromCodeFence(string text, out string fenced)
    {
        fenced = string.Empty;

        var idx = IndexOfCodeFenceStart(text);
        if (idx < 0)
        {
            return false;
        }

        var afterFence = text.IndexOf('\n', idx);
        if (afterFence < 0)
        {
            return false;
        }

        var fenceEnd = text.IndexOf("```", afterFence + 1, StringComparison.Ordinal);
        if (fenceEnd < 0)
        {
            return false;
        }

        fenced = text.Substring(afterFence + 1, fenceEnd - (afterFence + 1));
        return true;
    }

    private static int IndexOfCodeFenceStart(string text)
    {
        var idx = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return idx;
        }

        return text.IndexOf("```", StringComparison.Ordinal);
    }

    private static bool TryExtractFirstJsonValue(string text, out string json)
    {
        json = string.Empty;

        var start = FindFirstJsonStart(text);
        if (start < 0)
        {
            return false;
        }

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
                    json = text.Substring(start, (i - start) + 1).Trim();
                    return true;
                }
            }
        }

        return false;
    }

    private static int FindFirstJsonStart(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '{' or '[')
            {
                return i;
            }
        }

        return -1;
    }
}
