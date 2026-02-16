namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Exception raised when structured output extraction or deserialization fails.
/// </summary>
public sealed class CodexStructuredOutputParseException : Exception
{
    /// <summary>
    /// Gets the raw text captured from Codex.
    /// </summary>
    public string RawText { get; }

    /// <summary>
    /// Gets the extracted JSON text when extraction succeeded but deserialization failed.
    /// </summary>
    public string? ExtractedJson { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexStructuredOutputParseException"/> class.
    /// </summary>
    public CodexStructuredOutputParseException(string message, string rawText, string? extractedJson, Exception innerException)
        : base(message, innerException)
    {
        RawText = rawText;
        ExtractedJson = extractedJson;
    }
}

