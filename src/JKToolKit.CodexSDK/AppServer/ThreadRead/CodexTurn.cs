using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents a parsed turn returned by <c>thread/read</c> when turns are requested.
/// </summary>
public sealed record class CodexTurn
{
    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the upstream turn status.
    /// </summary>
    public CodexTurnStatus Status { get; }

    /// <summary>
    /// Gets the typed items associated with the turn.
    /// </summary>
    public IReadOnlyList<CodexThreadItem> Items { get; }

    /// <summary>
    /// Gets the optional turn error when the turn failed.
    /// </summary>
    public CodexTurnError? Error { get; }

    /// <summary>
    /// Gets the raw JSON payload for the turn.
    /// </summary>
    public JsonElement Raw { get; }

    internal CodexTurn(string id, CodexTurnStatus status, IReadOnlyList<CodexThreadItem> items, CodexTurnError? error, JsonElement raw)
    {
        Id = id;
        Status = status;
        Items = items;
        Error = error;
        Raw = raw;
    }

    internal static CodexTurn? TryParse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = CodexAppServerClientJson.ExtractTurnId(element) ?? string.Empty;
        var status = CodexTurnStatusExtensions.Parse(CodexAppServerClientJson.GetStringOrNull(element, "status"));
        var raw = element.Clone();
        var items = CodexThreadItemParser.ParseItems(element);
        var error = CodexTurnError.Parse(CodexAppServerClientJson.TryGetObject(element, "error"));

        return new CodexTurn(id, status, items, error, raw);
    }
}
