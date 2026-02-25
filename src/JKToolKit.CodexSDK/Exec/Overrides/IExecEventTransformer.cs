using System.Text.Json;

namespace JKToolKit.CodexSDK.Exec.Overrides;

/// <summary>
/// Transforms JSONL events (type + payload) before mapping.
/// </summary>
public interface IExecEventTransformer
{
    /// <summary>
    /// Transforms the event type and raw payload.
    /// </summary>
    /// <param name="type">The event type identifier.</param>
    /// <param name="rawPayload">The raw JSON payload (never null).</param>
    /// <returns>The transformed event type and raw payload.</returns>
    (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload);
}

