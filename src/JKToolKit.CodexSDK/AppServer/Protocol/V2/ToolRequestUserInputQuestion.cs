using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire shape for a <c>item/tool/requestUserInput</c> question (v2 protocol).
/// </summary>
public sealed record class ToolRequestUserInputQuestion
{
    /// <summary>
    /// Gets the question id (used as a key in the response).
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets the short header for the question.
    /// </summary>
    [JsonPropertyName("header")]
    public required string Header { get; init; }

    /// <summary>
    /// Gets the full question prompt.
    /// </summary>
    [JsonPropertyName("question")]
    public required string Question { get; init; }

    /// <summary>
    /// Gets a value indicating whether the question includes an "Other" free-form option.
    /// </summary>
    [JsonPropertyName("isOther")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsOther { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer should be treated as secret input.
    /// </summary>
    [JsonPropertyName("isSecret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsSecret { get; init; }

    /// <summary>
    /// Gets optional pre-defined options.
    /// </summary>
    [JsonPropertyName("options")]
    public IReadOnlyList<ToolRequestUserInputOption>? Options { get; init; }
}
