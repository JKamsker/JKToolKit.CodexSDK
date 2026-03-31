using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// JSON converter for <see cref="CollabReceiverStatus"/> that maps null or unknown values to <see cref="CollabReceiverStatus.Unknown"/>.
/// </summary>
public sealed class CollabReceiverStatusJsonConverter : JsonConverter<CollabReceiverStatus>
{
    /// <inheritdoc />
    public override CollabReceiverStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return CollabReceiverStatus.Unknown;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            using var _ = JsonDocument.ParseValue(ref reader);
            return CollabReceiverStatus.Unknown;
        }

        return ParseOrUnknown(reader.GetString());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CollabReceiverStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(ToWireValue(value));
    }

    /// <summary>
    /// Parses a wire status value into a <see cref="CollabReceiverStatus"/>.
    /// </summary>
    public static CollabReceiverStatus ParseOrUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CollabReceiverStatus.Unknown;
        }

        value = value.Trim();

        if (value.Equals("pendingInit", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("pending_init", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("pending-init", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.PendingInit;
        }

        if (value.Equals("running", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.Running;
        }

        if (value.Equals("interrupted", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.Interrupted;
        }

        if (value.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.Completed;
        }

        if (value.Equals("errored", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.Errored;
        }

        if (value.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.Shutdown;
        }

        if (value.Equals("notFound", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("not_found", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("not-found", StringComparison.OrdinalIgnoreCase))
        {
            return CollabReceiverStatus.NotFound;
        }

        return CollabReceiverStatus.Unknown;
    }

    /// <summary>
    /// Converts a <see cref="CollabReceiverStatus"/> to its wire value.
    /// </summary>
    public static string ToWireValue(CollabReceiverStatus value) =>
        value switch
        {
            CollabReceiverStatus.PendingInit => "pendingInit",
            CollabReceiverStatus.Running => "running",
            CollabReceiverStatus.Interrupted => "interrupted",
            CollabReceiverStatus.Completed => "completed",
            CollabReceiverStatus.Errored => "errored",
            CollabReceiverStatus.Shutdown => "shutdown",
            CollabReceiverStatus.NotFound => "notFound",
            _ => "unknown"
        };
}
