using System.Text.Json;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Represents an output schema configuration for Codex structured outputs.
/// </summary>
public sealed record CodexOutputSchema
{
    private CodexOutputSchema(CodexOutputSchemaKind kind, JsonElement? json, string? filePath)
    {
        Kind = kind;
        Json = json;
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the schema kind.
    /// </summary>
    public CodexOutputSchemaKind Kind { get; }

    /// <summary>
    /// Gets the raw JSON schema payload when <see cref="Kind"/> is <see cref="CodexOutputSchemaKind.Json"/>.
    /// </summary>
    public JsonElement? Json { get; }

    /// <summary>
    /// Gets the schema file path when <see cref="Kind"/> is <see cref="CodexOutputSchemaKind.File"/>.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Creates an output schema from an in-memory JSON Schema object.
    /// </summary>
    public static CodexOutputSchema FromJson(JsonElement schema) =>
        new(CodexOutputSchemaKind.Json, schema, filePath: null);

    /// <summary>
    /// Creates an output schema from an on-disk JSON Schema file path.
    /// </summary>
    public static CodexOutputSchema FromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Schema file path cannot be empty or whitespace.", nameof(filePath));
        }

        return new(CodexOutputSchemaKind.File, json: null, filePath);
    }
}

