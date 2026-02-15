namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Specifies how an output schema is provided to Codex.
/// </summary>
public enum CodexOutputSchemaKind
{
    /// <summary>
    /// Schema provided as an in-memory JSON element (materialized to a temp file for exec-mode).
    /// </summary>
    Json,

    /// <summary>
    /// Schema provided as a file path.
    /// </summary>
    File
}

