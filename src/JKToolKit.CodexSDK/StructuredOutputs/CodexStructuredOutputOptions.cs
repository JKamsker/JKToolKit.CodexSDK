using System.Text.Json;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Controls how structured outputs are generated and parsed.
/// </summary>
public sealed record CodexStructuredOutputOptions
{
    /// <summary>
    /// Gets JSON serializer options used for schema generation and DTO deserialization.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether JSON extraction should tolerate surrounding text/code fences.
    /// </summary>
    public bool TolerantJsonExtraction { get; init; } = true;
}

